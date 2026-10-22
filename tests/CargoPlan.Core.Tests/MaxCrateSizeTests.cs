using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A rack's max crate size (D56): the game's limit, which the grid alone doesn't give. The Carrack's racks are 4×4×4
/// cells, so a 32 SCU crate (4×4×2) fits by shape, but the game takes crates up to 16 SCU.
/// </summary>
public class MaxCrateSizeTests
{
    private static Rack Limited(int index, ShipPoint origin) =>
        new($"rack-{index}", index, 64, new Dimensions(4, 4, 4), new RackPosition(RackSide.Port, RackLevel.Lower), origin, StackAxis.PlusZ, CrateSize.Sixteen);

    /// <summary>One Carrack-like rack: 4×4×4, crates up to 16 SCU.</summary>
    private static readonly Ship Carrack = new("carrack_like", "Carrack-like test ship", 64, [Limited(0, new ShipPoint(0, 0, 0))]);

    /// <summary>Rack 1 like the Carrack's, rack 2 a 4×4×2 without a limit: the only one for a 32.</summary>
    private static readonly Ship Mixed = new("mixed", "Mixed test ship", 96,
    [
        Limited(0, new ShipPoint(0, 0, 0)),
        new Rack("rack-1", 1, 32, new Dimensions(4, 4, 2), new RackPosition(RackSide.Starboard, RackLevel.Lower), new ShipPoint(4, 0, 0)),
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
        Carrack.Racks[0].CanHold(size).Should().Be(expected);
    }

    [Fact]
    public void Without_a_max_crate_size_the_grid_decides()
    {
        var rack = new Rack("rack-0", 0, 64, new Dimensions(4, 4, 4), new RackPosition(RackSide.Port, RackLevel.Lower), new ShipPoint(0, 0, 0));

        rack.CanHold(CrateSize.ThirtyTwo).Should().BeTrue();
    }

    [Fact]
    public void Plan_refuses_a_32_in_a_ship_whose_racks_take_16()
    {
        var act = () => LoadPlanner.Plan(Carrack, [Stop("Riker", 32, CrateSize.ThirtyTwo).ToStop()]);

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "32 SCU crate of Titanium for Riker doesn't fit any rack of carrack_like (racks take crates up to 16 SCU).");
    }

    [Fact]
    public void Plan_lays_16s_into_the_limited_rack()
    {
        var plan = LoadPlanner.Plan(Carrack, [Stop("Riker", 64, CrateSize.Sixteen).ToStop()]);

        plan.Crates.Should().HaveCount(4).And.OnlyContain(c => c.CrateSize == CrateSize.Sixteen);
        LoadPlanner.Validate(plan, Carrack).Should().BeEmpty();
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
    public void Moving_a_32_into_a_limited_rack_is_refused()
    {
        var plan = LoadPlanner.Plan(Mixed, [Stop("Riker", 32, CrateSize.ThirtyTwo).ToStop()]);

        var act = () => LoadPlanner.MoveCrate(plan, Mixed, plan.Crates[0], "rack-0", new GridPosition(0, 0, 0));

        act.Should().Throw<PlanException>().WithMessage("*32 SCU crate of Titanium for Riker doesn't fit rack 1 (takes crates up to 16 SCU)*");
    }

    // Two 16s side by side are a 32's block, and the contract allows 32: only the rack says no.
    [Fact]
    public void Merging_two_16s_in_a_limited_rack_is_refused()
    {
        var saved = Planned(Carrack, Stop("Riker", 32, CrateSize.ThirtyTwo, CrateSize.Sixteen, CrateSize.Sixteen));

        LoadPlanner.MergeProblem(saved, Carrack, saved.Plan.Crates).Should().Be("Rack 1 takes crates up to 16 SCU.");
    }
}
