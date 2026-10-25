using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class MissionPaletteTests
{
    private static LoadPlan PlanFor(params string[] destinations) =>
        TestPlans.Of(ShipLoader.Load("hull_a"), [.. destinations.Select(d => new Placement(d, "rack-0", new CargoItem("Gold", 1, CrateSize.One)))]);

    [Fact]
    public void Stops_get_ordinals_in_the_order_they_first_appear_in_the_plan()
    {
        var indices = MissionPalette.Assign(PlanFor("Riker", "Shubin", "Riker", "Orison"));

        indices.Should().Equal(new Dictionary<string, int> { ["Riker"] = 0, ["Shubin"] = 1, ["Orison"] = 2 });
    }

    [Fact]
    public void The_same_plan_always_gets_the_same_ordinals()
    {
        var plan = PlanFor("Lorville", "Riker", "Shubin");

        MissionPalette.Assign(plan).Should().Equal(MissionPalette.Assign(plan));
    }

    [Fact]
    public void Stop_nine_keeps_its_own_ordinal()
    {
        // No wrap-around here (K4.2): the palette turns the ordinal into colour and hatch, so the round isn't lost.
        string[] stops = [.. Enumerable.Range(1, 9).Select(i => $"Stop {i}")];

        MissionPalette.Assign(PlanFor(stops))["Stop 9"].Should().Be(8);
    }

    [Fact]
    public void Selection_differs_from_the_drop_target_and_the_problem_red()
    {
        MissionPalette.Selection.Should().NotBe(MissionPalette.DragTarget).And.NotBe(MissionPalette.Violation);
    }

    [Fact]
    public void The_under_stroke_is_dark_and_wider_than_the_edges_it_lies_under()
    {
        // P6: under the red, magenta and cyan edges, so they read on any stop colour.
        MissionPalette.EdgeUnderStroke.Should().Match<System.Windows.Media.Color>(c => c.R == 0 && c.G == 0 && c.B == 0 && c.A > 0);
        MissionPalette.EdgeUnderStrokeWidth.Should().BeGreaterThan(MissionPalette.EdgeWidth);
    }
}
