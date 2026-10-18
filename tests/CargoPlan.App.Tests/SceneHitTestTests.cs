using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class SceneHitTestTests
{
    private static readonly ShipScene Scene = BuildSample();

    private static ShipScene BuildSample()
    {
        var (ship, plan) = SamplePlan.Load();
        return ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));
    }

    [Fact]
    public void Polygon_contains_inner_points_only()
    {
        Point[] square = [new(0, 0), new(2, 0), new(2, 2), new(0, 2)];

        SceneHitTest.Contains(square, new Point(1, 1)).Should().BeTrue();
        SceneHitTest.Contains(square, new Point(3, 1)).Should().BeFalse();
        SceneHitTest.Contains(square, new Point(1, -0.5)).Should().BeFalse();
    }

    [Fact]
    public void Tool_tip_names_rack_position_and_cargo()
    {
        // Rack 1's top face, middle of the plate.
        var point = IsoProjection.Project(2, 2, 5);

        SceneHitTest.ToolTipAt(Scene, point).Should().Be("Rack 1 · port upper · 16 SCU Titanium (4×4)");
    }

    [Fact]
    public void The_rack_in_front_wins_over_the_rack_behind()
    {
        // (2, 0, 3.5) lies on Rack 3's port face, hidden behind Rack 1: same screen point as (3.5, 1.5, 5) on Rack 1's top.
        var point = IsoProjection.Project(2, 0, 3.5);

        SceneHitTest.ToolTipAt(Scene, point).Should().StartWith("Rack 1 ·");
    }

    [Fact]
    public void Free_space_has_no_tool_tip()
    {
        // Rack 3 holds 14/16 SCU; the free rest sits at the aft end (x 0..0.5).
        var point = IsoProjection.Project(0.25, -1, 5);

        SceneHitTest.ToolTipAt(Scene, point).Should().BeNull();
    }

    [Fact]
    public void Empty_space_has_no_tool_tip()
    {
        SceneHitTest.ToolTipAt(Scene, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Delivered_cargo_says_so()
    {
        var (ship, plan) = SamplePlan.Load();
        var delivered = plan with { Placements = [.. plan.Placements.Select(p => p with { Delivered = true })] };
        var scene = ShipViewBuilder.Build(delivered, ship, []);

        SceneHitTest.ToolTipAt(scene, IsoProjection.Project(2, 2, 5))
            .Should().Be("Rack 1 · port upper · 16 SCU Titanium (4×4) (delivered)");
    }
}
