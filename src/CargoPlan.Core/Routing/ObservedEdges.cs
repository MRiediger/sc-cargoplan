using CargoPlan.Core.Text;

namespace CargoPlan.Core.Routing;

/// <summary>
/// A pickup → drop-off a template was seen to serve (design §8.3, T13.3): the scans of its variant named both places.
/// </summary>
/// <param name="From">The pickup's position name, as the matrix rows name it.</param>
/// <param name="To">The drop-off's position name.</param>
/// <param name="Count">Scans of the variant, not of this pair: the observations keep the places of all scans together.</param>
/// <param name="LastUtc">The variant's last scan.</param>
public sealed record ObservedEdge(string From, string To, int Count, DateTime LastUtc);

/// <summary>
/// What the observations add to the templates (T13.3, D122): edges for the templates whose pools are empty (the 75
/// system-wide Covalex ones) or hold more, and the reward of a Red Wind template, which the data don't know. Only
/// observations matched to a template count, and those ambiguous between twins that differ only in their faction:
/// the scan can't tell the sides apart and every Red Wind scan is such a pair; the faction filter shows the player's.
/// </summary>
public sealed class ObservedEdges
{
    private readonly Dictionary<string, List<ObservedEdge>> _edges;
    private readonly Dictionary<string, (long Reward, DateTime LastUtc)> _rewards;
    private readonly Dictionary<string, IReadOnlyList<ScuBand>> _bands;

    private ObservedEdges(
        Dictionary<string, List<ObservedEdge>> edges,
        Dictionary<string, (long, DateTime)> rewards,
        Dictionary<string, IReadOnlyList<ScuBand>> bands,
        int matched,
        int ambiguous,
        IReadOnlyList<(ObservationKey Key, string Reason)> unmatched)
    {
        _edges = edges;
        _rewards = rewards;
        _bands = bands;
        Unmatched = unmatched;
        Matched = matched;
        Ambiguous = ambiguous;
    }

    public static ObservedEdges Empty { get; } = new([], [], [], 0, 0, []);

    /// <summary>Observations matched to one template, or to faction twins.</summary>
    public int Matched { get; }

    /// <summary>Observations several templates answer to that aren't faction twins; they add nothing.</summary>
    public int Ambiguous { get; }

    /// <summary>Observations no template answers to (<see cref="MatchResult.None"/>); they add nothing.</summary>
    public int None => Unmatched.Count;

    /// <summary>Each observation no template answers to with the reason (the first test that left none), for the log.</summary>
    public IReadOnlyList<(ObservationKey Key, string Reason)> Unmatched { get; }

    /// <summary>The edges seen of <paramref name="template"/>, one per pair; empty if none.</summary>
    public IReadOnlyList<ObservedEdge> Of(HaulTemplate template) => _edges.GetValueOrDefault(template.Uuid) ?? [];

    /// <summary>The reward a scan showed for a template the data give none (Red Wind): the last one seen; else null.</summary>
    public long? RewardOf(HaulTemplate template) =>
        template.Reward is null && _rewards.TryGetValue(template.Uuid, out var seen) ? seen.Reward : null;

    /// <summary>
    /// The SCU per commodity the scans of <paramref name="template"/> saw, one band per commodity over its delivery lines
    /// (T13.4: a loop's crates take the largest seen instead of the data's band); null if no scan was matched to it.
    /// </summary>
    public IReadOnlyList<ScuBand>? ScuOf(HaulTemplate template) => _bands.GetValueOrDefault(template.Uuid);

    public static ObservedEdges From(Observations observations, IReadOnlyList<HaulTemplate> templates, Positions positions)
    {
        Dictionary<string, Dictionary<(string, string), ObservedEdge>> edges = [];
        Dictionary<string, (long, DateTime)> rewards = [];
        Dictionary<string, IReadOnlyList<ScuBand>> bands = [];
        var (matched, ambiguous) = (0, 0);
        List<(ObservationKey, string)> unmatched = [];
        foreach (var observation in observations.Entries)
        {
            IReadOnlyList<HaulTemplate> those;
            switch (TemplateMatch.Find(observation, templates, positions))
            {
                case MatchResult.Matched m:
                    those = [m.Template];
                    break;
                case MatchResult.Ambiguous a when FactionTwins(a.Candidates):
                    those = a.Candidates;
                    break;
                case MatchResult.Ambiguous:
                    ambiguous++;
                    continue;
                case MatchResult.None n:
                    unmatched.Add((observation.Key, n.Reason));
                    continue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(observations), "Not a match result.");
            }

            matched++;
            var system = observation.Key.System;
            string[] from = [.. Resolved(observation.Pickups, system, positions)];
            string[] to = [.. Resolved(observation.Destinations, system, positions)];
            foreach (var template in those)
            {
                if (!edges.TryGetValue(template.Uuid, out var pairs))
                {
                    edges[template.Uuid] = pairs = [];
                }

                foreach (var pair in from.SelectMany(f => to.Where(t => t != f).Select(t => (f, t))))
                {
                    pairs[pair] = pairs.TryGetValue(pair, out var known)
                        ? known with { Count = known.Count + observation.Count, LastUtc = Later(known.LastUtc, observation.LastUtc) }
                        : new ObservedEdge(pair.f, pair.t, observation.Count, observation.LastUtc);
                }

                bands[template.Uuid] = Widened(bands.GetValueOrDefault(template.Uuid) ?? [], observation.Scu);
                if (template.Reward is null && (!rewards.TryGetValue(template.Uuid, out var seen) || observation.LastUtc > seen.Item2))
                {
                    rewards[template.Uuid] = (observation.Key.Reward, observation.LastUtc);
                }
            }
        }

        return new(edges.ToDictionary(e => e.Key, e => e.Value.Values.ToList()), rewards, bands, matched, ambiguous, unmatched);
    }

    /// <summary>Templates equal but for their faction (and identity): the two sides of one Red Wind variant.</summary>
    public static bool FactionTwins(IReadOnlyList<HaulTemplate> candidates) =>
        candidates.Select(c => c.Faction).Distinct().Count() == candidates.Count
        && candidates.Skip(1).All(c => Same(c, candidates[0]));

    private static bool Same(HaulTemplate a, HaulTemplate b) =>
        a.Contractor == b.Contractor && a.Rank == b.Rank && a.Size == b.Size && a.Direct == b.Direct && a.Shape == b.Shape
        && a.Stops == b.Stops && a.System == b.System && a.AreaKey == b.AreaKey && a.MissionType == b.MissionType && a.Reward == b.Reward
        && a.Legs.SequenceEqual(b.Legs) && a.PickupPool.SequenceEqual(b.PickupPool) && a.DropoffPool.SequenceEqual(b.DropoffPool);

    private static IEnumerable<string> Resolved(IEnumerable<string> places, StarSystem system, Positions positions) =>
        places.Select(p => positions.Resolve(system, p)?.Name).OfType<string>().Distinct(StringComparer.Ordinal);

    // One band per commodity as OCR may read it, as Observation keeps them: two observations of one template join.
    private static ScuBand[] Widened(IEnumerable<ScuBand> known, IEnumerable<ScuBand> seen) =>
    [
        .. known.Concat(seen)
            .GroupBy(b => OcrText.AnchorForm(b.Commodity), StringComparer.Ordinal)
            .Select(g => new ScuBand(g.First().Commodity, g.Min(b => b.Min), g.Max(b => b.Max)))
            .OrderBy(b => b.Commodity, StringComparer.Ordinal),
    ];

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;
}
