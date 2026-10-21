using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Planning;

/// <summary>
/// Takes ContractTracker captures into the plan (D39 addendum, D41) and finds what the legend marks as a possible
/// duplicate. Every capture becomes a mission of its own, nothing is replaced; what is uncertain is marked, not decided.
/// </summary>
public static class ContractSync
{
    /// <summary>
    /// One sync: missions deleted in ContractTracker leave if nothing of them is on board (all ticked, or never placed);
    /// waiting missions are placed where they fit now; new captures above the watermark are placed in id order
    /// (<see cref="LoadPlanner.PlaceInto"/>), and the watermark rises to the highest id taken. Without a change it
    /// returns <paramref name="saved"/> itself, so a file event without news changes nothing (D38: diff, don't react).
    /// </summary>
    /// <param name="present">The ids of the plan's captures still in ContractTracker; null if it wasn't read, then
    /// nothing counts as deleted.</param>
    /// <param name="databaseIsNew">ContractTracker's ids started again at 1 (a new file, D44): the watermark goes back to 0,
    /// so the new file's captures come in.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship, or a capture makes no valid mission.</exception>
    public static SavedPlan Apply(
        SavedPlan saved, Ship ship, IReadOnlyList<ContractCapture> captures, IReadOnlySet<long>? present, bool databaseIsNew = false)
    {
        if (databaseIsNew && saved.Watermark > 0)
        {
            saved = new SavedPlan(saved.Missions, saved.Plan);
        }

        var current = saved;
        if (present is not null)
        {
            foreach (var gone in Gone(saved, present).Where(m => !OnBoard(saved, m.Id)))
            {
                current = LoadPlanner.RemoveMission(current, ship, gone.Id);
            }
        }

        current = LoadPlanner.RetryUnplaced(current, ship);
        foreach (var capture in captures.Where(c => c.Source.ContractIds.Min() > saved.Watermark).OrderBy(c => c.Source.ContractIds.Min()))
        {
            var placed = LoadPlanner.PlaceInto(current, ship, capture.Stops, capture.Source);
            current = new SavedPlan(placed.Missions, placed.Plan, Math.Max(current.Watermark, capture.Source.ContractIds.Max()));
        }

        return current;
    }

    /// <summary>
    /// Ids of the missions deleted in ContractTracker that still have crates on board: the plan keeps them, the player
    /// ticks them or deletes them in the editor (D39 addendum).
    /// </summary>
    public static IReadOnlyList<int> Conflicts(SavedPlan saved, IReadOnlySet<long> present) =>
        [.. Gone(saved, present).Where(m => OnBoard(saved, m.Id)).Select(m => m.Id)];

    /// <summary>
    /// The missions that may be a second capture of another one (D39 addendum): of two missions without any ticked crate,
    /// the later one, if the destinations and commodities it delivers are all delivered by the other, or the other way
    /// round. Overlapping alone is no duplicate: two real contracts can share destinations (36 and 37/38). Different
    /// rewards are two contracts too (47 and 48, T5.3b); an unknown reward (typed, older capture) doesn't rule one out.
    /// Derived, not stored, so it goes with the first tick on either and comes back with ticking back. Names compare
    /// ignoring case (D40).
    /// </summary>
    public static IReadOnlySet<int> Duplicates(SavedPlan saved)
    {
        HashSet<int> ticked = [.. saved.Plan.Crates.Where(c => c.Delivered).Select(c => c.Mission)];
        var candidates = saved.Missions
            .Where(m => !ticked.Contains(m.Id))
            .Select(m => (
                m.Id,
                m.Contract?.Reward,
                Deliveries: m.Deliveries.Select(d => (d.Destination.ToUpperInvariant(), d.Commodity.ToUpperInvariant())).ToHashSet()))
            .ToArray();

        HashSet<int> duplicates = [];
        for (var later = 1; later < candidates.Length; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                var (first, second) = (candidates[earlier], candidates[later]);
                if ((first.Reward is null || second.Reward is null || first.Reward == second.Reward)
                    && (second.Deliveries.IsSubsetOf(first.Deliveries) || first.Deliveries.IsSubsetOf(second.Deliveries)))
                {
                    duplicates.Add(candidates[later].Id);
                }
            }
        }

        return duplicates;
    }

    /// <summary>Captures every id of which is gone from ContractTracker; typed missions never are.</summary>
    private static IEnumerable<Mission> Gone(SavedPlan saved, IReadOnlySet<long> present) =>
        saved.Missions.Where(m => m.Contract is { } contract && !contract.ContractIds.Any(present.Contains));

    private static bool OnBoard(SavedPlan saved, int missionId) => saved.Plan.Crates.Any(c => c.Mission == missionId && !c.Delivered);
}
