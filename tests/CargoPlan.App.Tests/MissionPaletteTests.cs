using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

public class MissionPaletteTests
{
    private static LoadPlan PlanFor(params string[] destinations) => new(
        "hull_a",
        [.. destinations.Select(d => new Placement(d, "rack-0", new CargoItem("Gold", 1, CrateSize.One)))],
        []);

    [Fact]
    public void Has_eight_colours()
    {
        MissionPalette.Colors.Should().HaveCount(8).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Stops_get_colours_in_the_order_they_first_appear_in_the_plan()
    {
        var indices = MissionPalette.Assign(PlanFor("Riker", "Shubin", "Riker", "Orison"));

        indices.Should().Equal(new Dictionary<string, int> { ["Riker"] = 0, ["Shubin"] = 1, ["Orison"] = 2 });
    }

    [Fact]
    public void The_same_plan_always_gets_the_same_colours()
    {
        var plan = PlanFor("Lorville", "Riker", "Shubin");

        MissionPalette.Assign(plan).Should().Equal(MissionPalette.Assign(plan));
    }

    [Fact]
    public void Stop_nine_repeats_the_first_colour_and_says_so()
    {
        string[] stops = [.. Enumerable.Range(1, 9).Select(i => $"Stop {i}")];
        var plan = PlanFor(stops);

        MissionPalette.Assign(plan)["Stop 9"].Should().Be(0);
        MissionPalette.Repeats(plan).Should().BeTrue();
    }

    [Fact]
    public void Eight_stops_do_not_repeat()
    {
        MissionPalette.Repeats(PlanFor([.. Enumerable.Range(1, 8).Select(i => $"Stop {i}")])).Should().BeFalse();
    }

    [Fact]
    public void Delivered_free_and_violation_colours_differ_from_every_stop_colour()
    {
        MissionPalette.Colors.Should().NotContain([MissionPalette.Delivered, MissionPalette.Free, MissionPalette.Violation]);
    }

    [Fact]
    public void Brushes_are_frozen_so_rendering_never_copies_them()
    {
        MissionPalette.Brush(0).IsFrozen.Should().BeTrue();
        MissionPalette.Brush(0).Should().BeSameAs(MissionPalette.Brush(0));
    }
}
