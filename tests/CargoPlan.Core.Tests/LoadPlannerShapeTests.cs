using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Racks whose grid isn't a multiple of the crate shapes (D54): the planner's search checks every rack's crates against its
/// grid (D64), and refuses what finds no gap. uneven_long has two racks of 10×3×3 (90 SCU): each holds one 32 SCU crate
/// (8×2×2, D57), by SCU it would be two. It replays runs 3 and 4 of spike S7.0, whose 5×5×3 racks take no 32 since D57.
/// </summary>
public class LoadPlannerShapeTests
{
    private static readonly Ship Uneven = TestShips.Load("uneven_long");
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static Stop StopOf(string destination, int order, string commodity, int scu, CrateSize size) =>
        new(destination, order, [new CargoItem(commodity, scu, size)]);

    /// <summary>
    /// Area18 70 × 1 and Port Tressler 2 × 32. By SCU Port Tressler's 32s share a rack; by shape each rack takes one, and
    /// Area18's 1s fill the rest of both racks: two destinations in both racks.
    /// </summary>
    private static readonly Stop[] OnesThen32s =
        [StopOf("Area18", 1, "Quartz", 70, CrateSize.One), StopOf("Port Tressler", 2, "Titanium", 64, CrateSize.ThirtyTwo)];

    private static IReadOnlyList<Stop> Run4 => MissionFixtures.Load("spike_uneven_run4");

    private static int Scu(LoadPlan plan) => plan.Crates.Sum(c => (int)c.CrateSize);

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Plan_lays_run_4_with_one_32_per_rack(FillOrder fillOrder)
    {
        var plan = LoadPlanner.Plan(Uneven, Run4, fillOrder);

        Scu(plan).Should().Be(104);
        Uneven.Racks.Should().OnlyContain(r => plan.CratesForRack(r.Id).Count(c => c.CrateSize == CrateSize.ThirtyTwo) == 1);
        LoadPlanner.Validate(plan, Uneven).Should().BeEmpty();
    }

    [Fact]
    public void Plan_of_run_4_gives_the_derived_hints()
    {
        var plan = LoadPlanner.Plan(Uneven, Run4);

        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Dest1, Dest2 – identify crates by count"),
            new PlanWarning("rack-0", "Dest1 spans racks 1+2"),
            new PlanWarning("rack-1", "Dest1 spans racks 1+2"));
    }

    [Fact]
    public void Plan_lets_two_stops_share_both_racks()
    {
        var plan = LoadPlanner.Plan(Uneven, OnesThen32s);

        Scu(plan).Should().Be(134);
        LoadPlanner.Validate(plan, Uneven).Should().BeEmpty();
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Area18, Port Tressler – identify crates by count"),
            new PlanWarning("rack-0", "Area18 spans racks 1+2"),
            new PlanWarning("rack-0", "Port Tressler spans racks 1+2"),
            new PlanWarning("rack-1", "Rack 2 shared by Area18, Port Tressler – identify crates by count"),
            new PlanWarning("rack-1", "Area18 spans racks 1+2"),
            new PlanWarning("rack-1", "Port Tressler spans racks 1+2"));
    }

    [Fact]
    public void Place_lays_the_largest_crate_first_whatever_the_input_order()
    {
        // The input puts Area18's 1s before the 32; laid in that order, 58 1s (90 − 32) would leave no 8×2×2 gap for it.
        (CrateSize Size, string Destination)[] crates = [.. Enumerable.Repeat((CrateSize.One, "Area18"), 58), (CrateSize.ThirtyTwo, "Port Tressler")];

        var positions = CrateLayout.Place(Uneven.Racks[0], [], crates, FillOrder.BowFirst);

        positions.Should().NotBeNull();
        CrateLayout.Problems(Uneven.Racks[0], [.. crates.Zip(positions!).Select(p =>
            new Crate(p.First.Destination, "Gold", p.First.Size, "rack-0", p.Second.Position, Rotated: p.Second.Rotated))]).Should().BeEmpty();
    }

    // Dest1's two 32s take one rack each, Dest2's two find no gap: 180 − 128 = 52 SCU free.
    [Fact]
    public void Plan_rejects_run_3_naming_what_doesnt_fit()
    {
        var act = () => LoadPlanner.Plan(Uneven, MissionFixtures.Load("spike_uneven_run3"));

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "Dest2 doesn't fit: no room for 2 × 32 SCU crates of Titanium – 52 SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks.");
    }

    // One 32 per rack, the third finds none: 180 − 96 = 84 SCU free.
    [Fact]
    public void Plan_names_the_stop_whose_32_finds_no_rack()
    {
        Stop[] route =
        [
            StopOf("Area18", 1, "Gold", 32, CrateSize.ThirtyTwo),
            StopOf("Port Tressler", 2, "Iron", 32, CrateSize.ThirtyTwo),
            StopOf("Seraphim", 3, "Titanium", 32, CrateSize.ThirtyTwo),
        ];

        var act = () => LoadPlanner.Plan(Uneven, route);

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "Seraphim doesn't fit: no room for 1 × 32 SCU crate of Titanium – 84 SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks.");
    }

    [Fact]
    public void Hull_A_load_case_is_laid_whole()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.HullAEightStopsMissions.ToStops());

        Scu(plan).Should().Be(58);
        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
    }
}
