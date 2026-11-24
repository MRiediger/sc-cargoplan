using System.Globalization;
using CargoPlan.Core.Text;
using static CargoPlan.Core.Json.EnumWords;

namespace CargoPlan.Core.Routing;

/// <summary>Which template an observation is (D122): exactly one, several the scan can't tell apart, or none and why.</summary>
public abstract record MatchResult
{
    private MatchResult()
    {
    }

    public sealed record Matched(HaulTemplate Template) : MatchResult;

    /// <param name="Candidates">At least two, in the order of the data.</param>
    public sealed record Ambiguous(IReadOnlyList<HaulTemplate> Candidates) : MatchResult;

    /// <param name="Reason">For the log: the first test no template passed, with the values that failed it.</param>
    public sealed record None(string Reason) : MatchResult;
}

/// <summary>
/// An observation to its template (T13.3, D122), after the names of one place are folded (B1) and the places resolved
/// against the positions. Never by the title's text: the Red Wind card shows another title than the data (issue #22),
/// and the parts that matter (rank, size, direct) are in the key already.
/// <list type="number">
/// <item>Candidates: every template a scan of the contractor may be (<see cref="Board.Matchable"/>) of the key's rank and
/// size, and direct where the title tells it.</item>
/// <item>Covalex: the reward, fixed per variant (design §7.4). Red Wind: never, the data have none.</item>
/// <item>Commodities: each one read is one of the template's legs, as OCR may read it.</item>
/// <item>SCU per commodity: no delivery line below the smallest leg of it, none above the sum of its legs (a line may
/// carry several legs: one Red Wind drop-off of 200 SCU Carbon from two pickups of 25–175 each).</item>
/// <item>Stops: the many side of the form names as many places as the template draws, <c>min(stops, pool)</c> (a pool of
/// three repeats a station for four stops, design §2); several scans together at least that many.</item>
/// <item>Places: each in the template's pool, or the board's hub on an empty side (D121); a side without a pool and
/// hub (the system-wide templates) takes any place.</item>
/// </list>
/// </summary>
public static class TemplateMatch
{
    public static MatchResult Find(Observation observation, IReadOnlyList<HaulTemplate> templates, Positions positions)
    {
        var key = observation.Key;
        var system = key.System;
        var once = observation.Count == 1;
        var pickups = Resolved(observation.Pickups, system, positions);
        var destinations = Resolved(observation.Destinations, system, positions);

        IEnumerable<HaulTemplate> candidates = Board.Matchable(templates, key.Contractor)
            .Where(t => t.Rank == key.Rank && t.Size == key.Size && (key.Direct is not { } direct || t.Direct == direct));
        (Func<HaulTemplate, bool> Passes, Func<string> Reason)[] tests =
        [
            (_ => true, () => $"no {Word(key.Contractor)} template of rank {Word(key.Rank)}, size {Word(key.Size)}{(key.Direct is { } d ? d ? ", direct" : ", not direct" : "")}"),
            (t => key.Contractor != HaulContractor.Covalex || t.Reward == key.Reward, () => $"no such template pays {key.Reward.ToString("N0", CultureInfo.InvariantCulture)} aUEC"),
            (t => observation.Scu.All(b => LegsOf(t, b.Commodity).Any()), () => $"no such template carries {string.Join(", ", observation.Scu.Select(b => b.Commodity))}"),
            (t => observation.Scu.All(b => InBand(LegsOf(t, b.Commodity).ToList(), b)), () => $"SCU outside the template's bands: {string.Join(", ", observation.Scu.Select(b => $"{b.Commodity} {b.Min}–{b.Max}"))}"),
            (t => FitsStops(t, pickups.Count, destinations.Count, once), () => $"{pickups.Count} pickups and {destinations.Count} drop-offs fit no such template's stops"),
            (t => InPools(BoardHubs.Pickups(t).Places, pickups) && InPools(BoardHubs.Dropoffs(t).Places, destinations),
                () => $"places outside the template's pools: {string.Join(", ", observation.Pickups.Concat(observation.Destinations))}"),
        ];

        foreach (var (passes, reason) in tests)
        {
            var left = candidates.Where(passes).ToList();
            if (left.Count == 0)
            {
                return new MatchResult.None(reason());
            }

            candidates = left;
        }

        var found = candidates.ToList();
        return found.Count == 1 ? new MatchResult.Matched(found[0]) : new MatchResult.Ambiguous(found);
    }

    /// <summary>The places a list names, each once: its position's name, or the name as read where no position answers to it.</summary>
    private static List<Place> Resolved(IEnumerable<string> places, StarSystem system, Positions positions) =>
        [.. places.Select(p => positions.Resolve(system, p) is { } position ? new Place(position.Name, true) : new Place(p, false)).Distinct()];

    private static IEnumerable<HaulLeg> LegsOf(HaulTemplate template, string commodity) =>
        template.Legs.Where(l => OcrText.AnchorForm(l.Commodity) == OcrText.AnchorForm(commodity));

    private static bool InBand(List<HaulLeg> legs, ScuBand band) =>
        legs.Count > 0 && band.Min >= legs.Min(l => l.MinScu) && band.Max <= legs.Sum(l => l.MaxScu);

    private static bool FitsStops(HaulTemplate template, int pickups, int destinations, bool once)
    {
        var (many, single, pool) = template.Shape switch
        {
            HaulShape.MultiToSingle => (pickups, destinations, template.PickupPool.Count),
            HaulShape.SingleToMulti => (destinations, pickups, template.DropoffPool.Count),
            _ => (destinations, pickups, 0),
        };
        var drawn = template.Shape == HaulShape.AToB ? 1 : pool > 0 ? Math.Min(template.Stops, pool) : template.Stops;

        // Several scans draw anew each time: their places together are at least one instance's.
        return once ? many == drawn && single <= 1 : many >= drawn;
    }

    // A side with places (pool or hub) takes only them; a place no position answers to is none of them.
    private static bool InPools(IReadOnlyList<string> pool, List<Place> places) =>
        pool.Count == 0 || places.All(place => place.Known && pool.Contains(place.Name, StringComparer.Ordinal));

    /// <param name="Known">A position answers to it, and <paramref name="Name"/> is that position's.</param>
    private sealed record Place(string Name, bool Known);
}
