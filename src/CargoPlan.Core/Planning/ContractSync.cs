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
    /// <summary>The plan after <see cref="Sync"/>, without what it did with gone contracts.</summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship, or a capture makes no valid mission.</exception>
    public static SavedPlan Apply(
        SavedPlan saved, Ship ship, IReadOnlyList<ContractCapture> captures, IReadOnlySet<long>? present, bool databaseIsNew = false,
        ContractGonePolicy onGone = ContractGonePolicy.Keep) =>
        Sync(saved, ship, captures, present, databaseIsNew, onGone).Saved;

    /// <summary>
    /// One sync. A mission ContractTracker no longer lists is marked gone and stays as it is (D81); with
    /// <see cref="ContractGonePolicy.Remove"/> its planned and delivered deliveries leave when it goes, its loaded ones
    /// stay, and a mission gone before leaves once nothing of it is planned or loaded. Nothing leaves when every contract
    /// goes in one read (at least two) or the database is new. A mission listed again loses its mark. Waiting missions
    /// still listed are placed where they fit now; new captures above the watermark are placed in id order
    /// (<see cref="LoadPlanner.PlaceInto"/>), and the watermark rises to the highest id taken. Without a change the result
    /// holds <paramref name="saved"/> itself, so a file event without news changes nothing (D38: diff, don't react).
    /// </summary>
    /// <param name="present">The ids of the plan's captures still in ContractTracker; null if it wasn't read, then
    /// nothing counts as gone or back.</param>
    /// <param name="databaseIsNew">ContractTracker's ids started again at 1 (a new file, D44): the watermark goes back to 0,
    /// so the new file's captures come in, and the plan's captures are detached, so none of the new ids matches them.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship, or a capture makes no valid mission.</exception>
    public static SyncResult Sync(
        SavedPlan saved, Ship ship, IReadOnlyList<ContractCapture> captures, IReadOnlySet<long>? present, bool databaseIsNew = false,
        ContractGonePolicy onGone = ContractGonePolicy.Keep)
    {
        if (databaseIsNew && saved.Watermark > 0)
        {
            saved = new SavedPlan(saved.Missions, saved.Plan, saved.CrateCap);
        }

        var current = saved;
        List<GoneReport> gone = [];
        Mission[] back = [];
        var allAtOnce = false;
        if (present is not null)
        {
            back = [.. saved.Missions.Where(m => m.Contract is { Gone: true, Detached: false } contract && contract.ContractIds.Any(present.Contains))];
            Mission[] goneBefore = [.. saved.Missions.Where(m => m.Contract is { Gone: true } && !back.Contains(m))];
            Mission[] listed = [.. saved.Missions.Where(m => m.Contract is { Gone: false })];
            Mission[] going = [.. listed.Where(m => !m.Contract!.ContractIds.Any(present.Contains))];
            allAtOnce = databaseIsNew || (going.Length >= 2 && going.Length == listed.Length + back.Length);
            var removing = onGone == ContractGonePolicy.Remove && !allAtOnce;

            current = Marked(current, back, gone: false);
            foreach (var mission in going)
            {
                (current, var report) = removing ? Going(current, ship, mission) : (Marked(current, [mission], gone: true), new GoneReport(mission, 0, 0, 0, false));
                gone.Add(report);
            }

            if (removing)
            {
                foreach (var mission in goneBefore.Where(m => !Aboard(current, m.Id)))
                {
                    gone.Add(new GoneReport(mission, 0, Deliveries(current, mission.Id).Count, 0, true));
                    current = LoadPlanner.RemoveMission(current, ship, mission.Id);
                }
            }

            if (databaseIsNew)
            {
                current = Detached(current);
            }
        }

        current = LoadPlanner.RetryUnplaced(current, ship);
        foreach (var capture in captures.Where(c => c.Source.ContractIds.Min() > saved.Watermark).OrderBy(c => c.Source.ContractIds.Min()))
        {
            var placed = LoadPlanner.PlaceInto(current, ship, capture.Stops, capture.Source);
            current = new SavedPlan(placed.Missions, placed.Plan, placed.CrateCap, Math.Max(current.Watermark, capture.Source.ContractIds.Max()));
        }

        var order = saved.Missions.Select((m, index) => (m.Id, index)).ToDictionary();
        return new SyncResult(current, [.. gone.OrderBy(g => order[g.Mission.Id])], back, allAtOnce);
    }

    /// <summary>
    /// The sync that follows an undo or redo (K2.4, Martin: restore the snapshot, then sync incrementally): the captures
    /// <paramref name="current"/> holds that neither <paramref name="target"/> nor <paramref name="reference"/> (the plan
    /// the step left) knew came in by a sync since, and are placed into <paramref name="target"/> again by
    /// <see cref="Apply"/>; with <paramref name="present"/> missions gone from ContractTracker are marked or leave as in a
    /// sync (D81). The database isn't read: what the plan knows is enough, and an undo never takes in a contract the player
    /// hasn't synced yet. A capture the step itself removed or added is known to one of the two and stays as the snapshot
    /// has it. A detached capture never came in by a sync.
    /// </summary>
    /// <param name="present">The capture ids ContractTracker had at the last read; null if it wasn't read.</param>
    public static SavedPlan Resync(
        SavedPlan target, SavedPlan reference, SavedPlan current, Ship ship, IReadOnlySet<long>? present,
        ContractGonePolicy onGone = ContractGonePolicy.Keep)
    {
        HashSet<long> known = [.. target.Missions.Concat(reference.Missions).SelectMany(m => m.Contract?.ContractIds ?? [])];
        ContractCapture[] since =
        [
            .. current.Missions
                .Where(m => m.Contract is { Detached: false } contract && !contract.ContractIds.Any(known.Contains))
                .Select(m => new ContractCapture(m.Stops, m.Contract!)),
        ];
        return Apply(target, ship, since, present, onGone: onGone);
    }

    /// <summary>
    /// A read without new captures that is no news (D38, D43): every capture still listed is in <paramref name="present"/>,
    /// and no gone one is back (D81). Such a read changes nothing, so a waiting mission waits for the button.
    /// </summary>
    public static bool Quiet(SavedPlan saved, IReadOnlySet<long> present) =>
        saved.Missions.All(m => m.Contract is not { Detached: false } contract || contract.Gone != contract.ContractIds.Any(present.Contains));

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

    /// <summary>
    /// A mission whose contract goes now, with <see cref="ContractGonePolicy.Remove"/>: its deliveries with a loaded crate
    /// stay and it is marked gone; the planned and the delivered ones leave, and with them the whole mission if nothing
    /// is loaded (a waiting one too).
    /// </summary>
    private static (SavedPlan Saved, GoneReport Report) Going(SavedPlan saved, Ship ship, Mission mission)
    {
        var deliveries = Deliveries(saved, mission.Id);
        HashSet<string> loaded = [.. deliveries.Where(d => d.Value.Any(c => c.Loaded)).Select(d => d.Key)];
        var planned = deliveries.Count(d => !loaded.Contains(d.Key) && d.Value.Any(c => !c.Delivered));
        var delivered = deliveries.Count - loaded.Count - planned;
        if (loaded.Count == 0)
        {
            return (LoadPlanner.RemoveMission(saved, ship, mission.Id), new GoneReport(mission, planned, delivered, 0, true));
        }

        HashSet<string> leaving = [.. deliveries.Keys.Where(d => !loaded.Contains(d))];
        var kept = leaving.Count == 0 ? saved : LoadPlanner.RemoveDeliveries(saved, ship, mission.Id, leaving);
        return (Marked(kept, [mission], gone: true), new GoneReport(mission, planned, delivered, loaded.Count, false));
    }

    /// <summary>A mission's crates by destination: its deliveries that have crates (none while it waits).</summary>
    private static Dictionary<string, Crate[]> Deliveries(SavedPlan saved, int missionId) =>
        saved.Plan.Crates.Where(c => c.Mission == missionId).GroupBy(c => c.Destination).ToDictionary(g => g.Key, g => g.ToArray());

    /// <summary>Something of the mission is on board, planned or loaded.</summary>
    private static bool Aboard(SavedPlan saved, int missionId) => saved.Plan.Crates.Any(c => c.Mission == missionId && !c.Delivered);

    /// <summary>The plan with these missions' captures marked gone or not; itself if there are none.</summary>
    private static SavedPlan Marked(SavedPlan saved, IReadOnlyCollection<Mission> missions, bool gone)
    {
        if (missions.Count == 0)
        {
            return saved;
        }

        HashSet<int> ids = [.. missions.Select(m => m.Id)];
        return new SavedPlan(
            [.. saved.Missions.Select(m => ids.Contains(m.Id) ? m with { Contract = m.Contract! with { Gone = gone } } : m)],
            saved.Plan,
            saved.CrateCap,
            saved.Watermark);
    }

    /// <summary>The plan with every gone capture detached (D81); itself if none is left to detach.</summary>
    private static SavedPlan Detached(SavedPlan saved) =>
        saved.Missions.Any(m => m.Contract is { Gone: true, Detached: false })
            ? new SavedPlan(
                [.. saved.Missions.Select(m => m.Contract is { Gone: true, Detached: false } contract ? m with { Contract = contract.AsDetached() } : m)],
                saved.Plan,
                saved.CrateCap,
                saved.Watermark)
            : saved;
}
