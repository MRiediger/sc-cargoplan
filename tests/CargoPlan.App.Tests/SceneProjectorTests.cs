using System.Diagnostics;
using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class SceneProjectorTests
{
    private const double DefaultYaw = IsoProjection.DefaultYaw;
    private static readonly double DefaultPitch = IsoProjection.DefaultPitch;

    private static readonly ShipScene Scene = BuildSample();

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
        return ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));
    }

    private static ProjectedRack RackNumber(ProjectedScene scene, int number) => scene.Racks.Single(r => r.Number == number);

    /// <summary>Shoelace formula: the area of a simple polygon from its corners.</summary>
    private static double Area(IReadOnlyList<Point> polygon) =>
        Math.Abs(polygon.Zip(polygon.Skip(1).Append(polygon[0])).Sum(e => e.First.X * e.Second.Y - e.Second.X * e.First.Y)) / 2;

    private static Point Centre(IReadOnlyList<Point> polygon) => new(polygon.Average(p => p.X), polygon.Average(p => p.Y));

    [Fact]
    public void Default_view_sees_top_port_and_bow()
    {
        SceneProjector.VisibleFaces(IsoProjection.Default).Should().Equal(BoxFace.Top, BoxFace.Port, BoxFace.Bow);
    }

    [Fact]
    public void From_below_the_bottom_replaces_the_top()
    {
        SceneProjector.VisibleFaces(new IsoProjection(DefaultYaw, -DefaultPitch)).Should().Equal(BoxFace.Bottom, BoxFace.Port, BoxFace.Bow);
    }

    [Fact]
    public void Turned_half_round_sees_top_starboard_and_aft()
    {
        SceneProjector.VisibleFaces(new IsoProjection(DefaultYaw + 180, DefaultPitch)).Should().Equal(BoxFace.Top, BoxFace.Starboard, BoxFace.Aft);
    }

    [Fact]
    public void Level_view_sees_neither_top_nor_bottom()
    {
        SceneProjector.VisibleFaces(new IsoProjection(DefaultYaw, 0)).Should().Equal(BoxFace.Port, BoxFace.Bow);
    }

    [Fact]
    public void Every_rack_and_every_box_shows_the_visible_faces()
    {
        foreach (var rack in SceneProjector.Project(Scene, IsoProjection.Default).Racks)
        {
            rack.Faces.Select(f => f.Face).Should().Equal(BoxFace.Top, BoxFace.Port, BoxFace.Bow);
            rack.Faces.Should().AllSatisfy(f => f.Polygon.Should().HaveCount(4));
            rack.Boxes.Should().NotBeEmpty().And.AllSatisfy(b => b.Faces.Select(f => f.Face).Should().Equal(BoxFace.Top, BoxFace.Port, BoxFace.Bow));
        }
    }

    [Fact]
    public void A_crate_is_one_box_with_the_gap_of_a_cell_all_round()
    {
        // Shubin's 8 SCU crate in Rack 2 is x 2..4, y 1..3, z 0..2. In the default view one cell is 1 unit long on all
        // three axes (IsoProjection): the drawn crate is 2 − 2 × 0.05 long, like a cell is 1 − 2 × 0.05.
        var crate = SceneProjector.Project(Scene, IsoProjection.Default).Racks
            .SelectMany(r => r.Boxes)
            .Single(b => b.Box.Bounds == new ShipBox(new ShipPoint(2, 1, 0), new ShipPoint(4, 3, 2)));
        var top = crate.Faces.Single(f => f.Face == BoxFace.Top).Polygon;

        crate.Faces.Should().HaveCount(3);
        (top[1] - top[0]).Length.Should().BeApproximately(1.9, 1e-9);
        (top[2] - top[1]).Length.Should().BeApproximately(1.9, 1e-9);
    }

    [Fact]
    public void Free_cells_are_drawn_smaller_than_one_cell_so_the_grid_shows()
    {
        var cell = SceneProjector.Project(Scene, IsoProjection.Default).Racks.SelectMany(r => r.Boxes).First(b => b.Box.Fill == CellFill.Free);
        var top = cell.Faces.Single(f => f.Face == BoxFace.Top).Polygon;

        (top[1] - top[0]).Length.Should().BeApproximately(0.9, 1e-9);
    }

    [Fact]
    public void Racks_come_back_to_front()
    {
        SceneProjector.Project(Scene, IsoProjection.Default).Racks.Select(r => r.Number).Should().Equal(4, 2, 3, 1);
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(45, -35.26)]
    [InlineData(225, 35.26)]
    [InlineData(300, -70)]
    public void Each_rack_projects_its_boxes_in_draw_order(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);

        foreach (var rack in SceneProjector.Project(Scene, camera).Racks)
        {
            var own = Scene.Racks.Single(r => r.RackId == rack.RackId).Boxes;
            rack.Boxes.Select(b => b.Box).Should().Equal(DrawOrder.BoxesBackToFront(own, camera));
        }
    }

    [Fact]
    public void From_above_a_layer_is_drawn_over_the_one_below_and_from_below_the_other_way_round()
    {
        // Rack 3 (Orison, y −2..0, z 3..5): two 4 SCU Laranite crates at the bow end, one above the other.
        ShipBox lower = new(new ShipPoint(2, -2, 3), new ShipPoint(4, 0, 4));
        ShipBox upper = new(new ShipPoint(2, -2, 4), new ShipPoint(4, 0, 5));

        int IndexOf(ProjectedScene scene, ShipBox box) =>
            scene.Racks.Single(r => r.Number == 3).Boxes.ToList().FindIndex(b => b.Box.Bounds == box);

        var above = SceneProjector.Project(Scene, IsoProjection.Default);
        var below = SceneProjector.Project(Scene, new IsoProjection(DefaultYaw, -DefaultPitch));

        IndexOf(above, lower).Should().BeLessThan(IndexOf(above, upper)).And.BeGreaterThanOrEqualTo(0);
        IndexOf(below, upper).Should().BeLessThan(IndexOf(below, lower)).And.BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(45, -35.26)]
    [InlineData(225, 35.26)]
    [InlineData(100, 10)]
    [InlineData(300, -70)]
    public void The_rack_number_sits_on_the_largest_visible_face(double yaw, double pitch)
    {
        foreach (var rack in SceneProjector.Project(Scene, new IsoProjection(yaw, pitch)).Racks)
        {
            var largest = rack.Faces.Max(f => Area(f.Polygon));
            var labelled = rack.Faces.Single(f => (Centre(f.Polygon) - rack.LabelCentre).Length < 1e-9);

            Area(labelled.Polygon).Should().BeApproximately(largest, 1e-9);
            Area(labelled.Polygon).Should().BeGreaterThanOrEqualTo(0.2 * largest);
        }
    }

    [Fact]
    public void Looking_almost_straight_down_puts_the_number_on_top()
    {
        var projected = SceneProjector.Project(Scene, new IsoProjection(DefaultYaw, IsoProjection.MaxPitch));

        projected.Racks.Should().AllSatisfy(rack =>
            (Centre(rack.Faces.Single(f => f.Face == BoxFace.Top).Polygon) - rack.LabelCentre).Length.Should().BeLessThan(1e-9));
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(0, 0)]
    [InlineData(135, -60)]
    [InlineData(270, 85)]
    public void Extent_encloses_the_frame(double yaw, double pitch)
    {
        var projected = SceneProjector.Project(Scene, new IsoProjection(yaw, pitch));

        projected.Frame.Should().HaveCount(12);
        projected.Frame.SelectMany(l => new[] { l.From, l.To }).Should().OnlyContain(p =>
            p.X >= projected.Extent.Left - 1e-9 && p.X <= projected.Extent.Right + 1e-9
            && p.Y >= projected.Extent.Top - 1e-9 && p.Y <= projected.Extent.Bottom + 1e-9);
    }

    [Fact]
    public void A_full_turn_projects_fast_enough_for_dragging()
    {
        // Generous: dragging projects once per mouse move, a full turn is ~720 px at 0.5°/px.
        var watch = Stopwatch.StartNew();
        for (var yaw = 0; yaw < 360; yaw++)
        {
            SceneProjector.Project(Scene, new IsoProjection(yaw, DefaultPitch));
        }

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void A_rack_of_1000_crates_projects_within_50_ms()
    {
        // Hull A has 64 cells, a Hull C would have about 4,600 (D21); 1 SCU crates are the most boxes a rack can hold.
        Rack rack = new("big", 0, 1000, new Dimensions(10, 10, 10), new RackPosition(RackSide.Port, RackLevel.Upper), new ShipPoint(0, 0, 0));
        Ship ship = new("big", "Synthetic", 1000, [rack]);
        var plan = TestPlans.Of(ship, new Placement("Riker", rack.Id, new CargoItem("Titanium", 1000, CrateSize.One)));
        var scene = ShipViewBuilder.Build(plan, ship, []);

        // Warm-up: the first call pays for the JIT, not for the cells.
        SceneProjector.Project(scene, IsoProjection.Default);
        var watch = Stopwatch.StartNew();
        var projected = SceneProjector.Project(scene, new IsoProjection(100, 20));
        watch.Stop();

        projected.Racks.Single().Boxes.Should().HaveCount(1000);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(50));
    }
}
