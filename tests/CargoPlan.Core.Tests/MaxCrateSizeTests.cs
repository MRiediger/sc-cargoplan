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

    private static SavedPlan Planned(Ship ship, MissionStop stop) => SavedPlan.FromStops([stop], LoadPlanner.Plan(ship, [stop.ToStop()]));

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
            "32 SCU crate of Titanium for Riker doesn't fit any rack of limited (racks take crates up to 16 SCU).");
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
        var saved = LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(Mixed.Id, [], [])), Mixed, [Stop("Riker", 32, CrateSize.ThirtyTwo)]);

        saved.Plan.Crates.Should().ContainSingle().Which.RackId.Should().Be("rack-1");
    }

    [Fact]
    public void Moving_a_32_into_a_limited_rack_keeps_it_in_the_rack_without_a_limit()
    {
        var plan = LoadPlanner.Plan(Mixed, [Stop("Riker", 32, CrateSize.ThirtyTwo).ToStop()]);

        var outcome = LoadPlanner.ResolveMove(plan, Mixed, [new CrateMove(plan.Crates[0], "rack-0", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Should().Equal(plan.Crates);
        outcome.Notices.Should().Equal("No room in rack 1 – placed in rack 2");
    }

    [Fact]
    public void Merging_two_16s_in_a_limited_rack_is_refused()
    {
        var saved = Planned(LimitedShip, Stop("Riker", 32, CrateSize.ThirtyTwo, CrateSize.Sixteen, CrateSize.Sixteen));

        LoadPlanner.MergeProblem(saved, LimitedShip, saved.Plan.Crates).Should().Be("Rack 1 takes crates up to 16 SCU.");
    }
}
