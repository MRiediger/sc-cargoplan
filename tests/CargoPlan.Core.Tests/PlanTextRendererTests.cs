using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Rendering;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class PlanTextRendererTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    /// <summary>The renderer always uses "\n"; this keeps the expected text independent of the checkout's line endings.</summary>
    private static string Lines(string text) => text.ReplaceLineEndings("\n");

    private static Stop TitaniumStop(string destination, int order, int scu) =>
        new(destination, order, [new CargoItem("Titanium", scu, CrateSize.Two)]);

    [Fact]
    public void Render_example_missions_shows_one_stop_per_rack_and_the_empty_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        PlanTextRenderer.Render(plan, HullA).Should().Be(Lines("""
            [Rack 1 · 12/16]  Riker   12 SCU Titanium (3×4)
            [Rack 2 · 10/16]  Shubin  10 SCU Quartz (5×2)
            [Rack 3 ·  8/16]  Orison   8 SCU Agricium (1×8)
            [Rack 4 ·  0/16]  -
            """));
    }

    [Fact]
    public void Render_split_stop_shows_the_warning_per_rack_and_once_in_the_summary()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);

        PlanTextRenderer.Render(plan, HullA).Should().Be(Lines("""
            [Rack 1 · 16/16]  Riker  16 SCU Titanium (4×4)
                              ! Riker spans racks 1+2
            [Rack 2 ·  4/16]  Riker   4 SCU Titanium (1×4)
                              ! Riker spans racks 1+2
            [Rack 3 ·  0/16]  -
            [Rack 4 ·  0/16]  -

            Warnings:
            - Riker spans racks 1+2
            """));
    }

    [Fact]
    public void Render_shared_and_split_rack_lists_all_placements_and_deduplicates_the_summary()
    {
        var plan = LoadPlanner.Plan(HullA,
        [
            TitaniumStop("Riker", 1, 20),
            TitaniumStop("Shubin", 2, 12),
            TitaniumStop("Orison", 3, 16),
            TitaniumStop("Lorville", 4, 16),
        ]);

        PlanTextRenderer.Render(plan, HullA).Should().Be(Lines("""
            [Rack 1 · 16/16]  Riker     16 SCU Titanium (8×2)
                              ! Riker spans racks 1+2
            [Rack 2 · 16/16]  Riker      4 SCU Titanium (2×2)
                              Shubin    12 SCU Titanium (6×2)
                              ! Rack 2 shared by Riker, Shubin – identify crates by count
                              ! Riker spans racks 1+2
            [Rack 3 · 16/16]  Orison    16 SCU Titanium (8×2)
            [Rack 4 · 16/16]  Lorville  16 SCU Titanium (8×2)

            Warnings:
            - Riker spans racks 1+2
            - Rack 2 shared by Riker, Shubin – identify crates by count
            """));
    }

    [Fact]
    public void Render_empty_plan_shows_every_rack_empty()
    {
        var plan = LoadPlanner.Plan(HullA, []);

        PlanTextRenderer.Render(plan, HullA).Should().Be(Lines("""
            [Rack 1 ·  0/16]  -
            [Rack 2 ·  0/16]  -
            [Rack 3 ·  0/16]  -
            [Rack 4 ·  0/16]  -
            """));
    }

    [Fact]
    public void Render_rejects_a_plan_for_another_ship()
    {
        var plan = new LoadPlan("cutlass_black", [], []);

        var act = () => PlanTextRenderer.Render(plan, HullA);

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }
}
