using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// What an undo or redo restores (K2.4): a snapshot of the plan, then what happened since the step that isn't on the
/// stack. Ticks (route state) carry over per delivery, but only those that changed since the step; captures a sync brought
/// in since the step come in again (Martin: restore, then sync incrementally).
/// </summary>
public class UndoRestoreTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static ContractCapture Capture(long id, string destination) =>
        new([new MissionStop(destination, 1, [new MissionItem("Stims", 4, CrateSize.Four)])], new ContractSource([id]));

    private static SavedPlan Synced(SavedPlan saved, params ContractCapture[] captures) =>
        ContractSync.Apply(saved, HullA, captures, null);

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    private static SavedPlan Ticked(SavedPlan saved, string destination, bool delivered = true) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, delivered), saved.CrateCap, saved.Watermark);

    /// <summary>A step: the crates of <paramref name="destination"/> into the last rack.</summary>
    private static SavedPlan Moved(SavedPlan saved, string destination)
    {
        Crate[] crates = [.. saved.Plan.Crates.Where(c => c.Destination == destination)];
        var moved = LoadPlanner.ResolveMoveToRack(saved.Plan, HullA, crates, HullA.Racks[^1].Id).Plan!;
        moved.Crates.Should().NotEqual(saved.Plan.Crates, "the step has to change the layout");
        return new SavedPlan(saved.Missions, moved, saved.CrateCap, saved.Watermark);
    }

    private static IEnumerable<Crate> Of(SavedPlan saved, string destination) => saved.Plan.Crates.Where(c => c.Destination == destination);

    private static IEnumerable<(string, string, CrateSize, string, GridPosition)> Lying(SavedPlan saved) =>
        saved.Plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId, c.Position));

    // --- Ticks carry over: only what changed since the step ---

    [Fact]
    public void A_tick_after_the_step_stays_on_the_restored_plan()
    {
        var before = Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin"));
        var after = Moved(before, "Shubin");
        var current = Ticked(after, "Riker");

        var (restored, refused) = LoadPlanner.CarryTicks(before, after, current, HullA);

        Of(restored, "Riker").Should().OnlyContain(c => c.Delivered);
        Of(restored, "Shubin").Should().OnlyContain(c => !c.Delivered);
        Lying(restored).Should().Equal(Lying(before), "the layout is the snapshot's");
        refused.Should().BeEmpty();
    }

    [Fact]
    public void Ticks_the_step_itself_cleared_come_back_with_the_snapshot()
    {
        // The editor's Plan clears every tick (D19); undoing it brings them back.
        var before = Ticked(Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin")), "Riker");
        var after = LoadPlanner.Plan(HullA, before.Missions, [.. before.Stops.Select(s => s.Destination)], watermark: before.Watermark);

        var (restored, refused) = LoadPlanner.CarryTicks(before, after, after, HullA);

        restored.Should().BeSameAs(before);
        refused.Should().BeEmpty();
    }

    [Fact]
    public void A_tick_taken_back_after_the_step_is_taken_back_on_the_restored_plan()
    {
        var before = Ticked(Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin")), "Riker");
        var after = Moved(before, "Shubin");
        var current = Ticked(after, "Riker", delivered: false);

        var (restored, refused) = LoadPlanner.CarryTicks(before, after, current, HullA);

        Of(restored, "Riker").Should().OnlyContain(c => !c.Delivered);
        refused.Should().BeEmpty();
    }

    [Fact]
    public void A_tick_back_without_room_on_the_restored_plan_stays_ticked_and_says_why()
    {
        // In the snapshot Shubin's crate lies in Riker's cells, freed by Riker's tick; the step took it out again.
        var ticked = Ticked(Synced(Empty, Capture(36, "Riker"), Capture(37, "Shubin")), "Riker");
        var riker = Of(ticked, "Riker").Single();
        SavedPlan before = new(
            ticked.Missions,
            ticked.Plan with { Crates = [.. ticked.Plan.Crates.Select(c => c.Destination == "Shubin" ? c with { RackId = riker.RackId, Position = riker.Position } : c)] },
            ticked.CrateCap,
            ticked.Watermark);
        var current = Ticked(ticked, "Riker", delivered: false);

        var (restored, refused) = LoadPlanner.CarryTicks(before, ticked, current, HullA);

        Of(restored, "Riker").Should().OnlyContain(c => c.Delivered);
        refused.Should().ContainSingle().Which.Should().StartWith("Riker can't go back on board");
    }

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

    [Fact]
    public void A_tick_on_a_contract_that_came_in_again_follows_the_contract_not_the_mission_id()
    {
        // Mission 2 is contract 37 in the snapshot; after the step deleted it, a sync gave contract 38 the id 2.
        var before = Synced(Empty, Capture(36, "Riker"), Capture(37, "Orison"));
        var after = LoadPlanner.RemoveMission(before, HullA, 2);
        var current = Ticked(Synced(after, Capture(38, "Shubin")), "Shubin");
        current.Missions.Single(m => m.Contract!.ContractIds.Single() == 38).Id.Should().Be(2);

        var resynced = ContractSync.Resync(before, after, current, HullA, null);
        var (restored, refused) = LoadPlanner.CarryTicks(resynced, after, current, HullA);

        Of(restored, "Shubin").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered);
        Of(restored, "Orison").Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered);
        refused.Should().BeEmpty();
    }
}
