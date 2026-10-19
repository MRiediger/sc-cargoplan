using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class SceneHitTestTests
{
    private static readonly IsoProjection Default = IsoProjection.Default;
    private static readonly ShipScene Scene = BuildSample();
    private static readonly ProjectedScene Projected = SceneProjector.Project(Scene, Default);

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
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
        var point = Default.Project(2, 2, 5);

        SceneHitTest.ToolTipAt(Projected, point).Should().Be("Rack 1 · port upper · 16 SCU Titanium (4×4)");
    }

    [Fact]
    public void The_rack_in_front_wins_over_the_rack_behind()
    {
        // (2, 0, 3.5) lies on Rack 3's port face, hidden behind Rack 1: same screen point as (3.5, 1.5, 5) on Rack 1's top.
        var point = Default.Project(2, 0, 3.5);

        SceneHitTest.ToolTipAt(Projected, point).Should().StartWith("Rack 1 ·");
    }

    [Fact]
    public void Free_space_has_no_tool_tip()
    {
        // Rack 3 holds 14/16 SCU; the free rest sits at the aft end (x 0..0.5).
        var point = Default.Project(0.25, -1, 5);

        SceneHitTest.ToolTipAt(Projected, point).Should().BeNull();
    }

    [Fact]
    public void Empty_space_has_no_tool_tip()
    {
        SceneHitTest.ToolTipAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Delivered_cargo_says_so()
    {
        var (ship, plan) = TestPlans.Load();
        var delivered = plan with { Placements = [.. plan.Placements.Select(p => p with { Delivered = true })] };
        var projected = SceneProjector.Project(ShipViewBuilder.Build(delivered, ship, []), Default);

        SceneHitTest.ToolTipAt(projected, Default.Project(2, 2, 5))
            .Should().Be("Rack 1 · port upper · 16 SCU Titanium (4×4) (delivered)");
    }

    [Fact]
    public void Destination_of_cargo_under_the_point()
    {
        SceneHitTest.DestinationAt(Projected, Default.Project(2, 2, 5)).Should().Be("Riker");
    }

    [Fact]
    public void Delivered_cargo_still_names_its_destination_so_a_click_can_undo()
    {
        var (ship, plan) = TestPlans.Load();
        var projected = SceneProjector.Project(ShipViewBuilder.Build(LoadPlanner.SetDelivered(plan, ship, "Riker", true), ship, []), Default);

        SceneHitTest.DestinationAt(projected, Default.Project(2, 2, 5)).Should().Be("Riker");
    }

    [Fact]
    public void Free_and_empty_space_have_no_destination()
    {
        SceneHitTest.DestinationAt(Projected, Default.Project(0.25, -1, 5)).Should().BeNull();
        SceneHitTest.DestinationAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Destination_follows_the_rack_in_front()
    {
        // Rack 3 (Orison) lies behind Rack 1 (Riker) at this screen point.
        SceneHitTest.DestinationAt(Projected, Default.Project(2, 0, 3.5)).Should().Be("Riker");
    }

    [Fact]
    public void Hit_on_cargo_names_its_rack_and_placement()
    {
        var (_, plan) = TestPlans.Load();
        var titanium = plan.Placements.Single(p => p.Item.Commodity == "Titanium");

        SceneHitTest.HitAt(Projected, Default.Project(2, 2, 5)).Should().Be(new SceneHit("rack-0", titanium));
        SceneHitTest.PlacementAt(Projected, Default.Project(2, 2, 5)).Should().Be(titanium);
    }

    [Fact]
    public void Hit_on_free_space_names_the_rack_without_a_placement()
    {
        // A drop target: free space belongs to its rack.
        SceneHitTest.HitAt(Projected, Default.Project(0.25, -1, 5)).Should().Be(new SceneHit("rack-2", null));
        SceneHitTest.PlacementAt(Projected, Default.Project(0.25, -1, 5)).Should().BeNull();
    }

    [Fact]
    public void Hit_off_the_racks_is_null()
    {
        SceneHitTest.HitAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Hit_follows_the_rack_in_front()
    {
        SceneHitTest.HitAt(Projected, Default.Project(2, 0, 3.5))!.RackId.Should().Be("rack-0");
    }

    [Fact]
    public void From_below_the_bottom_face_answers()
    {
        // Rack 2's bottom face, middle: Riker x 4..3, Shubin x 3..1, free x 1..0.
        var below = new IsoProjection(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);
        var projected = SceneProjector.Project(Scene, below);

        SceneHitTest.ToolTipAt(projected, below.Project(2, 2, 0)).Should().Be("Rack 2 · port lower · 8 SCU Agricium (1×8)");
    }
}
