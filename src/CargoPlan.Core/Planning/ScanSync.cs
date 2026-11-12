using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Planning;

/// <summary>
/// A scanned contract into the plan (T10.2b2, D103), matched by its content key (D101): a new mission, an update of the
/// mission with that key, or nothing new. The game rolls the amounts again on accepting (S10.0), so a scan of the accepted
/// view decides the amounts and ends the estimate; a scan of the offers view never changes a contract already accepted.
/// </summary>
public static class ScanSync
{
    /// <summary>How <see cref="ScanMerge.Cleared"/> names a delivery the scan changed (T10.3; the editor's are "changed in the editor").</summary>
    public const string ChangedByScan = "changed by scan";

    /// <param name="capture">The scan as the import hands it over; its number is ignored for a contract already in the plan.</param>
    /// <param name="nextNo">The number a new mission gets (the caller counts above every number given, D102).</param>
    /// <exception cref="ArgumentException">The capture is no scan, or the plan belongs to another ship.</exception>
    public static ScanMerge Apply(SavedPlan saved, Ship ship, ContractCapture capture, int nextNo)
    {
        var scan = capture.Source.Scan ?? throw new ArgumentException("The capture is no scan.", nameof(capture));
        if (saved.Missions.FirstOrDefault(m => m.Contract?.Scan?.Key == scan.Key) is not { } known)
        {
            var added = capture.Source with { Scan = new ScanIdentity(nextNo, scan.Key, scan.QuantitiesEstimated) };
            return new(LoadPlanner.PlaceInto(saved, ship, capture.Stops, added), ScanChange.Added, nextNo);
        }

        var mine = known.Contract!.Scan!;
        if (scan.QuantitiesEstimated && !mine.QuantitiesEstimated)
        {
            // An offer after the accepted contract: the accepted amounts stand.
            return new(saved, ScanChange.AlreadyInPlan, mine.No);
        }

        var source = known.Contract with
        {
            Scan = new ScanIdentity(mine.No, mine.Key, scan.QuantitiesEstimated),
            CrateSizeAssumed = capture.Source.CrateSizeAssumed,
            ListedDestinations = capture.Source.ListedDestinations,
            Reward = capture.Source.Reward,
        };
        if (SameAmounts(capture.Stops, known))
        {
            return mine.QuantitiesEstimated && !scan.QuantitiesEstimated
                ? new(WithContract(saved, known.Id, source), ScanChange.Updated, mine.No)
                : new(saved, ScanChange.AlreadyInPlan, mine.No);
        }

        var stops = WithManualItems(capture.Stops, known);
        if (!saved.Plan.Crates.Any(c => c.Mission == known.Id && (c.Loaded || c.Delivered)))
        {
            return new(LoadPlanner.ReplaceMission(saved, ship, known.Id, stops, source), ScanChange.Updated, mine.No, QuantitiesChanged: true);
        }

        // Cargo of it on board or delivered: as the editor's Plan does (Martin), the crates that still match stay.
        Mission[] missions = [.. saved.Missions.Select(m => m.Id == known.Id ? new Mission(known.Id, stops, source) : m)];
        string[] order = [.. saved.Stops.Select(s => s.Destination).Concat(stops.Select(s => s.Destination)).Distinct(StringComparer.OrdinalIgnoreCase)];
        try
        {
            var (planned, cleared) = LoadPlanner.PlanKeeping(
                saved, ship, missions, order, saved.Plan.FillOrder, saved.Plan.FillRows, saved.Plan.Grouping, saved.Plan.SectionGap, ChangedByScan);
            return new(planned, ScanChange.Updated, mine.No, QuantitiesChanged: true, cleared);
        }
        catch (PlanException ex)
        {
            return new(saved, ScanChange.Refused, mine.No, Refusal: ex.Message);
        }
    }

    /// <summary>The SCU per destination and commodity of the scan and of the mission's scanned items (hand-added ones aside).</summary>
    private static bool SameAmounts(IReadOnlyList<MissionStop> scanned, Mission known)
    {
        static Dictionary<(string, string), int> Amounts(IEnumerable<(string Destination, MissionItem Item)> items) =>
            items.GroupBy(x => (x.Destination.ToUpperInvariant(), x.Item.Commodity.ToUpperInvariant()))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Item.Scu));

        var scan = Amounts(scanned.SelectMany(s => s.Items.Select(i => (s.Destination, i))));
        var plan = Amounts(known.Stops.SelectMany(s => s.Items.Where(i => !i.Manual).Select(i => (s.Destination, i))));
        return scan.Count == plan.Count && scan.All(x => plan.TryGetValue(x.Key, out var scu) && scu == x.Value);
    }

    /// <summary>The scanned stops with the items the player added by hand (K6.5): they are the player's and stay.</summary>
    private static IReadOnlyList<MissionStop> WithManualItems(IReadOnlyList<MissionStop> scanned, Mission known)
    {
        var manual = known.Stops.SelectMany(s => s.Items.Where(i => i.Manual).Select(i => (s.Destination, Item: i))).ToList();
        List<MissionStop> stops = [.. scanned.Select(s => s with
        {
            Items = [.. s.Items, .. manual.Where(m => string.Equals(m.Destination, s.Destination, StringComparison.OrdinalIgnoreCase)).Select(m => m.Item)],
        })];
        stops.AddRange(manual.Where(m => !scanned.Any(s => string.Equals(s.Destination, m.Destination, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(m => m.Destination, StringComparer.OrdinalIgnoreCase)
            .Select((g, index) => new MissionStop(g.Key, scanned.Count + index + 1, [.. g.Select(m => m.Item)])));
        return stops;
    }

    private static SavedPlan WithContract(SavedPlan saved, int missionId, ContractSource contract) => new(
        [.. saved.Missions.Select(m => m.Id == missionId ? m with { Contract = contract } : m)], saved.Plan, saved.CrateCap);
}

/// <summary>What a scan did to the plan (D103).</summary>
public enum ScanChange
{
    /// <summary>A new mission with the next scan number.</summary>
    Added,

    /// <summary>The mission with that key: amounts, the estimate mark, reward or listed count changed.</summary>
    Updated,

    /// <summary>The mission with that key is as the scan says, or an offer came after the accepted contract.</summary>
    AlreadyInPlan,

    /// <summary>The update finds no room (a mission loaded in part, K6.4); the plan stays as it was.</summary>
    Refused,
}

/// <param name="ScanNo">The mission's scan number: new for <see cref="ScanChange.Added"/>, else the one it has.</param>
/// <param name="Cleared">Deliveries no longer loaded or ticked after an update (as the editor's Plan names them).</param>
/// <param name="Refusal">Why the update was refused.</param>
public sealed record ScanMerge(
    SavedPlan Saved, ScanChange Change, int ScanNo, bool QuantitiesChanged = false, IReadOnlyList<string>? Cleared = null, string? Refusal = null)
{
    public IReadOnlyList<string> Cleared { get; } = Cleared ?? [];
}
