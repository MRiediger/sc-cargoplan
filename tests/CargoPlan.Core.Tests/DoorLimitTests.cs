using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The door limit (D70) on the shipped Clipper: its 3×2×2 grid takes an 8, its door only crates up to 2 SCU. A mix the
/// planner derives is split up to the door, a mix the player set stays and waits with the door named.
/// </summary>
public class DoorLimitTests
{
    private static readonly Ship Clipper = ShipLoader.Load("clipper");
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly CrateCap Door = new(CrateSize.Two, CapReason.Door);

    private static MissionStop Riker(params CrateSize[] set) =>
        new("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Eight, set.Length == 0 ? null : set)]);

    private static MissionStop Shubin => new("Shubin", 1, [new MissionItem("Stims", 2, CrateSize.One, [CrateSize.One, CrateSize.One])]);

    private static SavedPlan Planned(Ship ship, params MissionStop[] stops) =>
        LoadPlanner.Plan(ship, [.. stops.Select((s, i) => new Mission(i + 1, [s]))], [.. stops.Select(s => s.Destination)]);

    private static SavedPlan Empty(Ship ship) => new([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship));

    private static CrateSize[] Sizes(SavedPlan saved, string destination) =>
        [.. saved.Plan.Crates.Where(c => c.Destination == destination).Select(c => c.CrateSize)];

    private static MissionItem ItemOf(SavedPlan saved, string destination) =>
        saved.Missions.SelectMany(m => m.Items).Single(at => at.Stop.Destination == destination).Item;

    [Fact]
    public void A_derived_mix_is_split_up_to_the_door()
    {
        var saved = Planned(Clipper, Riker());

        Sizes(saved, "Riker").Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.Two);
        ItemOf(saved, "Riker").Crates.Should().BeNull("the mix stays derived");
        saved.CrateCap.Should().Be(Door);
        saved.CratesCapped.Should().BeTrue();
        LoadPlanner.Validate(saved.Plan, Clipper).Should().BeEmpty();
    }

    [Fact]
    public void A_plan_without_a_capped_item_has_no_cap_notice()
    {
        Planned(Clipper, Shubin).CratesCapped.Should().BeFalse();
        Planned(HullA, Riker()).CratesCapped.Should().BeFalse();
    }

    [Fact]
    public void A_set_mix_the_door_refuses_is_refused_by_Plan()
    {
        var act = () => Planned(Clipper, Riker(CrateSize.Four, CrateSize.Four));

        act.Should().Throw<PlanException>().WithMessage("4 SCU crate of Titanium for Riker doesn't fit any rack of clipper (door limit 2 SCU).");
    }

    [Fact]
    public void PlaceInto_caps_a_derived_mix()
    {
        var saved = LoadPlanner.PlaceInto(Empty(Clipper), Clipper, [Riker()]);

        Sizes(saved, "Riker").Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.Two);
        saved.Missions.Should().ContainSingle().Which.Unplaced.Should().BeFalse();
    }

    [Fact]
    public void PlaceInto_keeps_a_set_mix_the_door_refuses_waiting_and_names_the_door()
    {
        var saved = LoadPlanner.PlaceInto(Empty(Clipper), Clipper, [Riker(CrateSize.Four, CrateSize.Four)]);

        saved.Missions.Should().ContainSingle().Which.Unplaced.Should().BeTrue();
        ItemOf(saved, "Riker").Crates.Should().Equal(CrateSize.Four, CrateSize.Four);
        LoadPlanner.PlaceProblem(saved, Clipper, 1).Should().Be("doesn't fit (no rack accepts 4 SCU crates, door limit 2 SCU)");
    }

    // Rule 3: a derived mix follows the ship, a set one never changes.
    [Fact]
    public void A_ship_switch_derives_the_mix_anew_and_keeps_a_set_one()
    {
        var onHullA = Planned(HullA, Riker(), Shubin);
        Sizes(onHullA, "Riker").Should().Equal(CrateSize.Eight);

        var onClipper = LoadPlanner.Replan(onHullA, Clipper);
        Sizes(onClipper, "Riker").Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.Two);
        Sizes(onClipper, "Shubin").Should().Equal(CrateSize.One, CrateSize.One);

        var back = LoadPlanner.Replan(onClipper, HullA);
        Sizes(back, "Riker").Should().Equal(CrateSize.Eight);
        ItemOf(back, "Riker").Crates.Should().BeNull();
        ItemOf(back, "Shubin").Crates.Should().Equal(CrateSize.One, CrateSize.One);
    }

    [Fact]
    public void Split_and_merge_back_of_a_capped_crate_leave_the_mix_derived()
    {
        var saved = Planned(Clipper, Riker());

        var split = LoadPlanner.SplitCrate(saved, Clipper, saved.Plan.Crates[0]);
        ItemOf(split, "Riker").Crates.Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.One, CrateSize.One);

        var merged = LoadPlanner.MergeCrates(split, Clipper, [.. split.Plan.Crates.Where(c => c.CrateSize == CrateSize.One)]);
        ItemOf(merged, "Riker").Crates.Should().BeNull();
    }

    // Every way a saved plan is built anew keeps the ship's cap, else its item check fails (the mix on board is capped).
    [Fact]
    public void Every_saved_plan_path_on_the_Clipper_keeps_the_cap()
    {
        var saved = Planned(Clipper, Riker(), Shubin);

        var renamed = LoadPlanner.RenameStop(saved, Clipper, "Riker", "Riker City");
        var removed = LoadPlanner.RemoveMission(renamed, Clipper, 2);
        var replanned = LoadPlanner.Replan(removed, Clipper);
        var ticked = new SavedPlan(replanned.Missions, LoadPlanner.SetDelivered(replanned.Plan, Clipper, "Riker City", true), replanned.CrateCap, replanned.Watermark);
        var retried = LoadPlanner.RetryUnplaced(ticked, Clipper);
        var synced = ContractSync.Apply(LoadPlanner.RemoveMission(retried, Clipper, 1), Clipper,
            [new ContractCapture([Riker()], new ContractSource([7]))], null);

        using var stream = new MemoryStream();
        PlanFile.Write(stream, synced);
        stream.Position = 0;
        var read = PlanFile.Read(stream, "plan.json", Clipper);

        read.CrateCap.Should().Be(Door);
        Sizes(read, "Riker").Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.Two);
    }

    [Fact]
    public void A_32_contract_on_the_Hull_A_is_capped_to_its_largest_rack()
    {
        var saved = Planned(HullA, new MissionStop("Riker", 1, [new MissionItem("Titanium", 32, CrateSize.ThirtyTwo)]));

        Sizes(saved, "Riker").Should().Equal(CrateSize.Sixteen, CrateSize.Sixteen);
        saved.CrateCap.Notice.Should().Be("crates capped to 16 SCU (largest rack)");
        saved.CratesCapped.Should().BeTrue();
    }

    // A plan saved before D70 (or on another ship) holds a mix derived under another cap: it reads, and a re-plan caps it.
    [Fact]
    public void A_plan_file_with_an_uncapped_mix_reads_and_a_replan_caps_it()
    {
        // Same id and grid, no door: the Clipper as shipped before D70.
        var withoutDoor = new Ship(Clipper.Id, Clipper.Name, Clipper.TotalScu,
            [.. Clipper.Racks.Select(r => new Rack(r.Id, r.Index, r.CapacityScu, r.Grid, r.Position, r.Origin, r.StackAxis))]);
        var old = Planned(withoutDoor, Riker());
        Sizes(old, "Riker").Should().Equal(CrateSize.Eight);

        using var stream = new MemoryStream();
        PlanFile.Write(stream, old);
        stream.Position = 0;
        var read = PlanFile.Read(stream, "plan.json", Clipper);

        read.CrateCap.Should().NotBe(CrateCap.Of(Clipper));
        Sizes(LoadPlanner.Replan(read, Clipper), "Riker").Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.Two);
    }
}
