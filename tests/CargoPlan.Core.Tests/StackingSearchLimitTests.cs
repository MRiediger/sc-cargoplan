using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The search for a layout without a crate on another destination's cargo gives up after
/// <see cref="CrateLayout.MaxStackingSearchSteps"/> steps, scaled by the rack's cells (D58); the rack keeps its first layout
/// and the plan notes it (D54, D64). Giving up itself is tested in <see cref="CrateLayoutTests"/> with a budget of one step:
/// with the column bounds (D64) no load found so far runs out, not even the T7.0 stress load, which the search under the
/// route order (D24 addendum) never finished. Here: that load, and the note carried through the plan's operations.
/// </summary>
public class StackingSearchLimitTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private const string Note = "Rack 1: too many crates to rearrange – a layout without crates on other cargo may exist.";

    /// <summary>The T7.0 stress rack (T7.1c, D58): one rack of 12×12×7, 1,008 cells.</summary>
    private static readonly Ship Huge = new("huge", "Stress test ship", 1_008,
        [new Rack("rack-0", 0, 1_008, new Dimensions(12, 12, 7), new RackPosition(RackSide.Port, RackLevel.Lower), new ShipPoint(0, 0, 0))]);

    /// <summary>
    /// The T7.1c stress load, 958 SCU: Dest1/2 each 2 × 32, 2 × 24, 3 × 16, 5 × 8, 8 × 4, 10 × 2, 26 × 1; Dest3/4 each 1 × 32,
    /// 1 × 24, 2 × 16, 5 × 8, 7 × 4, 10 × 2, 25 × 1.
    /// </summary>
    private static readonly Stop[] Stress =
    [
        StressStop("Dest1", 1, 2, 2, 3, 5, 8, 10, 26),
        StressStop("Dest2", 2, 2, 2, 3, 5, 8, 10, 26),
        StressStop("Dest3", 3, 1, 1, 2, 5, 7, 10, 25),
        StressStop("Dest4", 4, 1, 1, 2, 5, 7, 10, 25),
    ];

    private static Stop StressStop(string destination, int order, params int[] counts) =>
        new(destination, order,
        [
            .. new[] { CrateSize.ThirtyTwo, CrateSize.TwentyFour, CrateSize.Sixteen, CrateSize.Eight, CrateSize.Four, CrateSize.Two, CrateSize.One }
                .Zip(counts)
                .Select(p => new CargoItem("Titanium", (int)p.First * p.Second, p.First)),
        ]);

    // D58: steps × cells stays the same, 150,000 steps on 1,008 cells (the rack of the T7.0 stress load).
    [Theory]
    [InlineData(1_008, 150_000)]
    [InlineData(216, 700_000)]
    [InlineData(81, 1_866_666)]
    [InlineData(16, 9_450_000)]
    public void The_budget_scales_with_the_cells_of_the_rack(int cells, int steps)
    {
        CrateLayout.MaxStackingSearchSteps(cells).Should().Be(steps);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void Plan_lays_the_stress_load_apart_within_the_budget()
    {
        var plan = LoadPlanner.Plan(Huge, Stress);

        plan.Crates.Sum(c => (int)c.CrateSize).Should().Be(958);
        plan.Warnings.Should().Equal(new PlanWarning("rack-0", "Rack 1 shared by Dest1, Dest2, Dest3, Dest4 – identify crates by count"));
        LoadPlanner.Validate(plan, Huge).Should().BeEmpty();
    }

    /// <summary>Riker's 4 in rack 1, with the note of a search that gave up there.</summary>
    private static LoadPlan Noted() =>
        new(HullA.Id, [new Crate("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0))], [new PlanWarning("rack-0", Note)]);

    [Fact]
    public void The_note_stays_through_ticks_moves_and_the_plan_file()
    {
        var plan = Noted();

        var ticked = LoadPlanner.SetDelivered(plan, HullA, "Riker", true);
        var moved = LoadPlanner.MoveCrate(plan, HullA, plan.Crates[0], "rack-0", new GridPosition(2, 0, 0));
        var read = ReadBack(SavedPlan.FromStops([new MissionStop("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)])], plan));

        ticked.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
        moved.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
        read.Plan.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
    }

    [Fact]
    public void A_new_plan_has_no_note_of_the_plan_before()
    {
        // The note is carried by moves and ticks, never into a plan of its own.
        var plan = LoadPlanner.Plan(HullA, [new Stop("Riker", 1, [new CargoItem("Titanium", 4, CrateSize.Four)])]);

        plan.Warnings.Should().BeEmpty();
    }

    private static SavedPlan ReadBack(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        using var input = new MemoryStream(stream.ToArray());
        return PlanFile.Read(input, "plan.json", HullA);
    }
}
