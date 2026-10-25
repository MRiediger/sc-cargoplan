using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A rack's max crate size (D56): a limit on top of the grid. Synthetic: an 8×2×2 rack takes a 32 SCU crate (8×2×2, D57)
/// by shape, the limit says 16. No shipped ship needs it since D57: Fleetyards' limit equals what the grid allows.
/// </summary>
public class MaxCrateSizeTests
{
    private static Rack Limited(int index, ShipPoint origin) =>
        new($"rack-{index}", index, 32, new Dimensions(8, 2, 2), new RackPosition(RackSide.Port, RackLevel.Lower), origin, StackAxis.PlusZ, CrateSize.Sixteen);

    /// <summary>One rack of 8×2×2, crates up to 16 SCU.</summary>
    private static readonly Ship LimitedShip = new("limited", "Limited test ship", 32, [Limited(0, new ShipPoint(0, 0, 0))]);

    /// <summary>Rack 1 limited, rack 2 an 8×2×2 without a limit: the only one for a 32.</summary>
    private static readonly Ship Mixed = new("mixed", "Mixed test ship", 64,
    [
        Limited(0, new ShipPoint(0, 0, 0)),
        new Rack("rack-1", 1, 32, new Dimensions(8, 2, 2), new RackPosition(RackSide.Starboard, RackLevel.Lower), new ShipPoint(8, 0, 0)),
    ]);

    private static MissionStop Stop(string destination, int scu, CrateSize max, params CrateSize[] crates) =>
        new(destination, 1, [new MissionItem("Titanium", scu, max, crates.Length == 0 ? null : crates)]);

    private static SavedPlan Planned(Ship ship, MissionStop stop) => SavedPlan.FromStops([stop], LoadPlanner.Plan(ship, [stop.ToStop()]), CrateCap.Of(ship));

    [Theory]
    [InlineData(CrateSize.One, true)]
    [InlineData(CrateSize.Eight, true)]
    [InlineData(CrateSize.Sixteen, true)]
    [InlineData(CrateSize.ThirtyTwo, false)]
    public void CanHold_respects_the_max_crate_size(CrateSize size, bool expected)
    {
        LimitedShip.Racks[0].CanHold(size).Should().Be(expected);
    }

    [Fact]
    public void Without_a_max_crate_size_the_grid_decides()
    {
        var rack = new Rack("rack-0", 0, 32, new Dimensions(8, 2, 2), new RackPosition(RackSide.Port, RackLevel.Lower), new ShipPoint(0, 0, 0));

        rack.CanHold(CrateSize.ThirtyTwo).Should().BeTrue();
    }

    [Fact]
    public void Plan_refuses_a_32_in_a_ship_whose_racks_take_16()
    {
        var act = () => LoadPlanner.Plan(LimitedShip, [Stop("Riker", 32, CrateSize.ThirtyTwo).ToStop()]);

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "32 SCU crate of Titanium for Riker doesn't fit any rack of limited (door limit 16 SCU).");
    }

    [Fact]
    public void Plan_lays_16s_into_the_limited_rack()
    {
        var plan = LoadPlanner.Plan(LimitedShip, [Stop("Riker", 32, CrateSize.Sixteen).ToStop()]);

        plan.Crates.Should().HaveCount(2).And.OnlyContain(c => c.CrateSize == CrateSize.Sixteen);
        LoadPlanner.Validate(plan, LimitedShip).Should().BeEmpty();
    }

    // By SCU the 32 goes to rack 1; the layout refuses it there, the shape-aware fallback (D54) finds rack 2.
    [Fact]
    public void Plan_puts_a_32_into_the_rack_without_a_limit()
    {
        var plan = LoadPlanner.Plan(Mixed, [Stop("Riker", 32, CrateSize.ThirtyTwo).ToStop()]);

        plan.Crates.Should().ContainSingle().Which.RackId.Should().Be("rack-1");
        LoadPlanner.Validate(plan, Mixed).Should().BeEmpty();
    }

    [Fact]
    public void PlaceInto_puts_a_32_into_the_rack_without_a_limit()
    {
        var saved = LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(Mixed.Id, [], []), CrateCap.Of(Mixed)), Mixed, [Stop("Riker", 32, CrateSize.ThirtyTwo)]);

        saved.Plan.Crates.Should().ContainSingle().Which.RackId.Should().Be("rack-1");
    }

    [Fact]
    public void Moving_a_32_into_a_limited_rack_keeps_it_in_the_rack_without_a_limit()
    {
        var plan = LoadPlanner.Plan(Mixed, [Stop("Riker", 32, CrateSize.ThirtyTwo).ToStop()]);

        var outcome = LoadPlanner.ResolveMove(plan, Mixed, [new CrateMove(plan.Crates[0], "rack-0", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Should().Equal(plan.Crates);
        outcome.Notices.Should().Equal("Rack 1: door limit 16 SCU – placed in rack 2");
    }

    [Fact]
    public void Merging_two_16s_in_a_limited_rack_is_refused()
    {
        var saved = Planned(LimitedShip, Stop("Riker", 32, CrateSize.ThirtyTwo, CrateSize.Sixteen, CrateSize.Sixteen));

        LoadPlanner.MergeProblem(saved, LimitedShip, saved.Plan.Crates).Should().Be("Rack 1: door limit 16 SCU.");
    }

    /// <summary>
    /// Rack 1 (2×2×2) full with Riker's 8, rack 2 (4×2×2, door <paramref name="door"/>) with Shubin's four 2s in one half,
    /// its other half free: an 8 fits there by the grid.
    /// </summary>
    private static (Ship Ship, LoadPlan Plan) FullRackNextToADoor(CrateSize? door)
    {
        Ship ship = new("door", "Door test ship", 24,
        [
            new Rack("rack-0", 0, 8, new Dimensions(2, 2, 2), null, new ShipPoint(0, 0, 0)),
            new Rack("rack-1", 1, 16, new Dimensions(4, 2, 2), null, new ShipPoint(4, 0, 0), StackAxis.PlusZ, door),
        ]);
        GridPosition[] shubin = [new(0, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, 1, 1)];
        LoadPlan plan = new(ship.Id,
        [
            new Crate("Riker", "Titanium", CrateSize.Eight, "rack-0", new GridPosition(0, 0, 0)),
            .. shubin.Select(p => new Crate("Shubin", "Stims", CrateSize.Two, "rack-1", p)),
        ], []);
        return (ship, plan);
    }

    // Stage 3b displaces only where the displaced crate may go: a door that refuses it keeps it where it is (D70).
    [Fact]
    public void Displacing_never_puts_a_crate_behind_a_door_that_refuses_it()
    {
        var (ship, plan) = FullRackNextToADoor(CrateSize.Two);
        var top = plan.Crates.Single(c => c.Destination == "Shubin" && c.Position == new GridPosition(0, 0, 1));

        var outcome = LoadPlanner.ResolveMove(plan, ship, [new CrateMove(top, "rack-0", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Single(c => c.Destination == "Riker").RackId.Should().Be("rack-0");
        outcome.Plan.Crates.Where(c => c.RackId == "rack-1").Should().OnlyContain(c => c.CrateSize <= CrateSize.Two);
    }

    // The same move without the door: the 8 is displaced into rack 2's free half, so the door above is what stops it.
    [Fact]
    public void Without_the_door_the_same_move_displaces_the_8()
    {
        var (ship, plan) = FullRackNextToADoor(null);
        var top = plan.Crates.Single(c => c.Destination == "Shubin" && c.Position == new GridPosition(0, 0, 1));

        var outcome = LoadPlanner.ResolveMove(plan, ship, [new CrateMove(top, "rack-0", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Displace);
        outcome.Plan!.Crates.Single(c => c.Destination == "Riker").RackId.Should().Be("rack-1");
    }
}
