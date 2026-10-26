using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// What an undo or redo restores (K2.4): a snapshot of the plan, then what happened since the step that isn't on the
/// stack: captures a sync brought in since the step come in again (Martin: restore, then sync incrementally). Ticks are
/// steps since K6.4 (D74 addendum): the snapshot's state of every delivery is restored as it is, so nothing carries over.
/// </summary>
public class UndoRestoreTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static ContractCapture Capture(long id, string destination) =>
        new([new MissionStop(destination, 1, [new MissionItem("Stims", 4, CrateSize.Four)])], new ContractSource([id]));

    private static SavedPlan Synced(SavedPlan saved, params ContractCapture[] captures) =>
        ContractSync.Apply(saved, HullA, captures, null);

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    private static SavedPlan Ticked(SavedPlan saved, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, true), saved.CrateCap, saved.Watermark);

    /// <summary>A step: the crates of <paramref name="destination"/> into the last rack.</summary>
    private static SavedPlan Moved(SavedPlan saved, string destination)
    {
        Crate[] crates = [.. saved.Plan.Crates.Where(c => c.Destination == destination)];
        var moved = LoadPlanner.ResolveMoveToRack(saved.Plan, HullA, crates, HullA.Racks[^1].Id).Plan!;
        moved.Crates.Should().NotEqual(saved.Plan.Crates, "the step has to change the layout");
        return new SavedPlan(saved.Missions, moved, saved.CrateCap, saved.Watermark);
    }

    private static IEnumerable<(string, string, CrateSize, string, GridPosition)> Lying(SavedPlan saved) =>
        saved.Plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId, c.Position));

    // --- Captures since the step come in again ---

    [Fact]
    public void A_contract_synced_since_the_step_comes_in_again()
    {
        var before = Synced(Empty, Capture(36, "Riker"));
        var after = Moved(before, "Riker");
        var current = Synced(after, Capture(37, "Shubin"));

        var restored = ContractSync.Resync(before, after, current, HullA, null);

        restored.Missions.Select(m => m.Contract!.ContractIds.Single()).Should().Equal(36, 37);
        restored.Watermark.Should().Be(37);
        Lying(restored).Where(l => l.Item1 == "Riker").Should().Equal(Lying(before));
    }

    [Fact]
    public void Undoing_a_discard_keeps_the_old_watermark_and_brings_no_contract_twice()
    {
        var before = Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin"));
        var after = LoadPlanner.Discard(before, HullA);
        var current = Synced(after, Capture(36, "Riker"), Capture(37, "Shubin"));

        var restored = ContractSync.Resync(before, after, current, HullA, null);

        restored.Missions.Select(m => m.Contract!.ContractIds.Single()).Should().Equal(36, 37);
        restored.Watermark.Should().Be(37);
    }

    [Fact]
    public void Redoing_a_discard_stays_empty_with_watermark_0()
    {
        var plan = Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin"));
        var discarded = LoadPlanner.Discard(plan, HullA);

        // Redo: the target is the plan before the undo, the reference what the undo restored.
        var restored = ContractSync.Resync(discarded, plan, plan, HullA, null);

        restored.Missions.Should().BeEmpty();
        restored.Watermark.Should().Be(0);
    }

    [Fact]
    public void A_contract_gone_from_ContractTracker_leaves_the_restored_plan_unless_on_board()
    {
        var before = Ticked(Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin")), "Riker");
        var after = Moved(before, "Shubin");

        var restored = ContractSync.Resync(before, after, after, HullA, new HashSet<long> { 37 });

        restored.Missions.Select(m => m.Contract!.ContractIds.Single()).Should().Equal(37);
    }

    [Fact]
    public void A_typed_mission_the_step_added_doesnt_come_in_again()
    {
        var before = Synced(Empty, Capture(36, "Riker"));
        var after = LoadPlanner.PlaceInto(before, HullA, [new MissionStop("Orison", 1, [new MissionItem("Gold", 4, CrateSize.Four)])]);

        ContractSync.Resync(before, after, after, HullA, null).Missions.Should().ContainSingle();
    }

}
