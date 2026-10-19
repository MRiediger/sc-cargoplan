using System.Windows;
using System.Windows.Input;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>
/// Points are cell centres in ship coordinates (D14), projected with the default camera. Fill order bow first: in
/// each layer the row next to the spindle, then the outer row (<see cref="RackCells"/>).
/// </summary>
public class SceneHitTestTests
{
    private static readonly IsoProjection Default = IsoProjection.Default;
    private static readonly ShipScene Scene = BuildSample();
    private static readonly ProjectedScene Projected = SceneProjector.Project(Scene, Default);

    // Rack 1 (Riker Titanium, full), top layer.
    private static readonly Point Rack1Top = Default.Project(2.5, 1.5, 5);

    // Rack 3 (Orison 14/16), top layer, outer row, aft end: free.
    private static readonly Point Rack3Free = Default.Project(0.5, -1.5, 5);

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
        return Build(plan, ship);
    }

    private static ShipScene Build(LoadPlan plan, Ship ship) =>
        ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship), FillOrder.BowFirst);

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
        SceneHitTest.ToolTipAt(Projected, Rack1Top).Should().Be("Rack 1 · port upper · 16 SCU Titanium (4×4)");
    }

    [Fact]
    public void The_rack_in_front_wins_over_the_rack_behind()
    {
        // (2, 0, 3.5) lies on Rack 3's port face, hidden behind Rack 1: same screen point as (3.5, 1.5, 5) on Rack 1's top.
        var point = Default.Project(2, 0, 3.5);

        SceneHitTest.ToolTipAt(Projected, point).Should().StartWith("Rack 1 ·");
    }

    [Fact]
    public void Free_cells_have_no_tool_tip()
    {
        SceneHitTest.ToolTipAt(Projected, Rack3Free).Should().BeNull();
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
        var projected = SceneProjector.Project(ShipViewBuilder.Build(delivered, ship, [], FillOrder.BowFirst), Default);

        SceneHitTest.ToolTipAt(projected, Rack1Top).Should().Be("Rack 1 · port upper · 16 SCU Titanium (4×4) (delivered)");
    }

    [Fact]
    public void Destination_of_cargo_under_the_point()
    {
        SceneHitTest.DestinationAt(Projected, Rack1Top).Should().Be("Riker");
    }

    [Fact]
    public void Delivered_cargo_still_names_its_destination_so_a_click_can_undo()
    {
        var (ship, plan) = TestPlans.Load();
        var projected = SceneProjector.Project(Build(LoadPlanner.SetDelivered(plan, ship, "Riker", true), ship), Default);

        SceneHitTest.DestinationAt(projected, Rack1Top).Should().Be("Riker");
    }

    [Fact]
    public void Free_and_empty_space_have_no_destination()
    {
        SceneHitTest.DestinationAt(Projected, Rack3Free).Should().BeNull();
        SceneHitTest.DestinationAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Destination_follows_the_rack_in_front()
    {
        // Rack 3 (Orison) lies behind Rack 1 (Riker) at this screen point.
        SceneHitTest.DestinationAt(Projected, Default.Project(2, 0, 3.5)).Should().Be("Riker");
    }

    [Fact]
    public void Cargo_seen_through_a_free_cell_of_the_same_rack_answers()
    {
        // Rack 2's outer top row is free; through its outline the top row next to the spindle shows: Shubin.
        SceneHitTest.DestinationAt(Projected, Default.Project(2.5, 2.5, 2)).Should().Be("Shubin");
    }

    [Fact]
    public void A_gap_between_cells_shows_the_cell_behind()
    {
        // x = 3 is the joint between two top cells of Rack 1; the cell below the joint shows through.
        SceneHitTest.DestinationAt(Projected, Default.Project(3, 1.5, 5)).Should().Be("Riker");
    }

    [Fact]
    public void Hit_on_cargo_names_its_rack_and_placement()
    {
        var (_, plan) = TestPlans.Load();
        var titanium = plan.Placements.Single(p => p.Item.Commodity == "Titanium");

        SceneHitTest.HitAt(Projected, Rack1Top).Should().Be(new SceneHit("rack-0", titanium));
        SceneHitTest.PlacementAt(Projected, Rack1Top).Should().Be(titanium);
    }

    [Fact]
    public void Hit_on_a_free_cell_names_the_rack_without_a_placement()
    {
        // A drop target: free cells belong to their rack.
        SceneHitTest.HitAt(Projected, Rack3Free).Should().Be(new SceneHit("rack-2", null));
        SceneHitTest.PlacementAt(Projected, Rack3Free).Should().BeNull();
    }

    [Fact]
    public void Hit_on_a_gap_between_free_cells_still_names_the_rack()
    {
        // x = 1: the joint between the two free cells of Rack 3, nothing behind it.
        SceneHitTest.HitAt(Projected, Default.Project(1, -1.5, 5)).Should().Be(new SceneHit("rack-2", null));
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
    public void From_below_the_bottom_layer_answers()
    {
        // Rack 2's bottom layer: Riker's Quartz next to the spindle, Shubin's Agricium in the outer row.
        var below = new IsoProjection(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);
        var projected = SceneProjector.Project(Scene, below);

        SceneHitTest.ToolTipAt(projected, below.Project(2.5, 2.5, 0)).Should().Be("Rack 2 · port lower · 8 SCU Agricium (1×8)");
        SceneHitTest.ToolTipAt(projected, below.Project(2.5, 1.5, 0)).Should().Be("Rack 2 · port lower · 4 SCU Quartz (2×2)");
    }

    [Fact]
    public void Right_drag_from_a_cell_onto_another_rack_moves_its_placement()
    {
        // Riker's Quartz in Rack 2 shows on the bow face, bottom layer next to the spindle.
        var (_, plan) = TestPlans.Load();
        var quartz = plan.Placements.Single(p => p.Item.Commodity == "Quartz");
        var down = SceneHitTest.HitAt(Projected, Default.Project(4, 1.5, 0.5));

        var gesture = DragGesture.Resolve(MouseButton.Right, down, moved: true, SceneHitTest.HitAt(Projected, Rack1Top));

        down.Should().Be(new SceneHit("rack-1", quartz));
        gesture.Should().Be(new Gesture.Move(quartz, "rack-0"));
    }
}
