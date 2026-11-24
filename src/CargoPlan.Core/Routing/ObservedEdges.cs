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

    private ObservedEdges(Dictionary<string, List<ObservedEdge>> edges, Dictionary<string, (long, DateTime)> rewards, int matched, int ambiguous, int none)
    {
        _edges = edges;
        _rewards = rewards;
        Matched = matched;
        Ambiguous = ambiguous;
        None = none;
    }

    public static ObservedEdges Empty { get; } = new([], [], 0, 0, 0);

    /// <summary>Observations matched to one template, or to faction twins.</summary>
    public int Matched { get; }

    /// <summary>Observations several templates answer to that aren't faction twins; they add nothing.</summary>
    public int Ambiguous { get; }

    /// <summary>Observations no template answers to (<see cref="MatchResult.None"/>); they add nothing.</summary>
    public int None { get; }

    /// <summary>The edges seen of <paramref name="template"/>, one per pair; empty if none.</summary>
    public IReadOnlyList<ObservedEdge> Of(HaulTemplate template) => _edges.GetValueOrDefault(template.Uuid) ?? [];

    /// <summary>The reward a scan showed for a template the data give none (Red Wind): the last one seen; else null.</summary>
    public long? RewardOf(HaulTemplate template) =>
        template.Reward is null && _rewards.TryGetValue(template.Uuid, out var seen) ? seen.Reward : null;

    public static ObservedEdges From(Observations observations, IReadOnlyList<HaulTemplate> templates, Positions positions)
    {
        Dictionary<string, Dictionary<(string, string), ObservedEdge>> edges = [];
        Dictionary<string, (long, DateTime)> rewards = [];
        var (matched, ambiguous, none) = (0, 0, 0);
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
                default:
                    none++;
                    continue;
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

                if (template.Reward is null && (!rewards.TryGetValue(template.Uuid, out var seen) || observation.LastUtc > seen.Item2))
                {
                    rewards[template.Uuid] = (observation.Key.Reward, observation.LastUtc);
                }
            }
        }

        return new(edges.ToDictionary(e => e.Key, e => e.Value.Values.ToList()), rewards, matched, ambiguous, none);
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

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;
}
