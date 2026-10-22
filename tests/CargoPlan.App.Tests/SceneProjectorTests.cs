using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
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
    public void A_single_box_projects_like_the_same_box_in_a_scene()
    {
        // The ghost of a dragged crate (D25) has the faces and the inset of a crate the scene draws.
        var box = SceneProjector.Project(Scene, IsoProjection.Default).Racks.SelectMany(r => r.Boxes).First(b => b.Box.Crate is not null);

        SceneProjector.ProjectBox(box.Box.Bounds, IsoProjection.Default).Should().BeEquivalentTo(box.Faces, o => o.WithStrictOrdering());
    }

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
        // Rack 2 (y 1..3, z 0..2, stacking to port, D26): Riker's two 2 SCU Quartz crates at the aft end next to the
        // spindle, one above the other (width runs up on the plate).
        ShipBox lower = new(new ShipPoint(0, 1, 0), new ShipPoint(2, 2, 1));
        ShipBox upper = new(new ShipPoint(0, 1, 1), new ShipPoint(2, 2, 2));

        int IndexOf(ProjectedScene scene, ShipBox box) =>
            scene.Racks.Single(r => r.Number == 2).Boxes.ToList().FindIndex(b => b.Box.Bounds == box);

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
    public void Extent_encloses_the_frame_and_the_bow_wedge(double yaw, double pitch)
    {
        var projected = SceneProjector.Project(Scene, new IsoProjection(yaw, pitch));

        projected.Frame.Should().HaveCount(12);
        projected.Frame.SelectMany(l => new[] { l.From, l.To })
            .Concat(projected.BowWedge.Faces.SelectMany(f => f.Polygon))
            .Should().OnlyContain(p =>
                p.X >= projected.Extent.Left - 1e-9 && p.X <= projected.Extent.Right + 1e-9
                && p.Y >= projected.Extent.Top - 1e-9 && p.Y <= projected.Extent.Bottom + 1e-9);
    }

    [Theory]
    [InlineData(4, 1)] // Hull A: 10 % of the length is 0.4 cells, the nose keeps its minimum
    [InlineData(20, 2)] // 10 % of the length
    public void Bow_wedge_has_a_tip_top_bottom_and_two_flanks_beyond_the_bow(int length, double nose)
    {
        ShipBox bounds = new(new ShipPoint(0, -2, 0), new ShipPoint(length, 3, 5));

        var wedge = SceneProjector.BowWedge(bounds);

        wedge.Select(f => f.Face).Should().BeEquivalentTo([BoxFace.Bow, BoxFace.Top, BoxFace.Bottom, BoxFace.Port, BoxFace.Starboard]);
        var corners = wedge.SelectMany(f => f.Corners).ToArray();
        corners.Should().OnlyContain(p => p.X >= length && p.Z >= 0 && p.Z <= 5);

        // The tip: half the width (5 cells around y = 0.5), the full height.
        var tip = wedge.Single(f => f.Face == BoxFace.Bow).Corners;
        tip.Should().OnlyContain(p => Math.Abs(p.X - (length + nose)) < 1e-9);
        tip.Select(p => (p.Y, p.Z)).Should().BeEquivalentTo([(-0.75, 0.0), (1.75, 0.0), (-0.75, 5.0), (1.75, 5.0)]);

        // Every normal points out of the wedge: away from its centre.
        var (cx, cy, cz) = (corners.Average(p => p.X), corners.Average(p => p.Y), corners.Average(p => p.Z));
        wedge.Should().OnlyContain(f =>
            (f.Corners.Average(p => p.X) - cx) * f.Normal.X
            + (f.Corners.Average(p => p.Y) - cy) * f.Normal.Y
            + (f.Corners.Average(p => p.Z) - cz) * f.Normal.Z > 0);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(135)]
    [InlineData(225)]
    [InlineData(315)]
    public void Bow_wedge_points_bowwards_on_screen(double yaw)
    {
        var camera = new IsoProjection(yaw, DefaultPitch);
        var projected = SceneProjector.Project(Scene, camera);
        var (min, max) = (Scene.Bounds.Min, Scene.Bounds.Max);
        Point[] tip = [.. SceneProjector.BowWedge(Scene.Bounds)
            .Single(f => f.Face == BoxFace.Bow).Corners
            .Select(p => camera.Project(p.X, p.Y, p.Z))];

        // From the bow side the wedge lies in front of every rack and shows its tip; from the stern it lies behind (D52).
        var bowSide = Math.Cos(yaw * Math.PI / 180) > 0;
        projected.BowWedge.InFront.Should().Be(bowSide);
        projected.BowWedge.Faces.Any(f => f.Face == BoxFace.Bow).Should().Be(bowSide);

        var boxCentre = camera.Project((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
        var bowwards = camera.Project(1, 0, 0) - camera.Project(0, 0, 0);
        ((Centre(tip) - boxCentre) * bowwards).Should().BePositive();
        ((projected.BowWedge.Floor.Centre - boxCentre) * bowwards).Should().BePositive();
        if (yaw == DefaultYaw)
        {
            // The bow stays on the left in the default view (D15), and so does its label.
            Centre(tip).X.Should().BeLessThan(boxCentre.X);
            projected.BowWedge.Floor.Centre.X.Should().BeLessThan(boxCentre.X);
        }
    }

    [Theory]
    [InlineData(45)]
    [InlineData(135)]
    [InlineData(225)]
    [InlineData(315)]
    public void Bow_label_lies_on_the_wedge_floor(double yaw)
    {
        var camera = new IsoProjection(yaw, DefaultPitch);
        var projected = SceneProjector.Project(Scene, camera);
        ViewTransform transform = new(40, new Vector(200, 200));
        var label = FloorLabel.Lay(Glyphs("Bow"), LabelEmSize, projected.BowWedge.Floor, transform, screenEmSize: 27);

        // Flattened into screen pixels: no transform left that would skew a pen's outline.
        (label.Transform?.Value ?? Matrix.Identity).IsIdentity.Should().BeTrue();
        Point[] floor = [.. SceneProjector.BowWedge(Scene.Bounds).Single(f => f.Face == BoxFace.Bottom).Corners
            .Select(c => transform.Apply(camera.Project(c.X, c.Y, c.Z)))];
        Point[] points = [.. label.Figures.SelectMany(f => f.Segments
            .SelectMany(s => s switch
            {
                PolyLineSegment poly => poly.Points,
                LineSegment line => [line.Point],
                _ => throw new InvalidOperationException($"Not flattened: {s.GetType().Name}"),
            })
            .Prepend(f.StartPoint))];
        points.Should().NotBeEmpty().And.OnlyContain(p => SceneHitTest.Contains(floor, p));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Bow_label_B_is_as_tall_on_screen_as_a_rack_number_digit(double zoom)
    {
        // The default overlay's ship view, as in RackNumberSizeTests.
        var projected = SceneProjector.Project(Scene, IsoProjection.Default);
        var fit = ViewTransform.Fit(projected.Extent, new Size(400, 302), margin: 8);
        ViewTransform transform = new(fit.Scale * zoom, fit.Offset * zoom);
        var numberSize = RackNumberSize.For(27, transform.Scale, Scene.Racks);

        // The B of "Bow" from its top to its baseline, through the label's matrix onto the screen.
        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, projected.BowWedge.Floor, transform, numberSize);
        var b = Glyphs("B").Bounds;
        var bOnScreen = (placement.Transform(b.TopLeft) - placement.Transform(b.BottomLeft)).Length;

        bOnScreen.Should().BeApproximately(Glyphs("1", numberSize).Bounds.Height, 1);
    }

    [Fact]
    public void Bow_label_never_leaves_a_short_floor()
    {
        // A floor of a narrow ship: a tenth of a cell of room across, far less than a rack number needs.
        var floor = SceneProjector.Project(Scene, IsoProjection.Default).BowWedge.Floor with { Room = new Size(0.1, 0.7) };
        ViewTransform transform = new(40, new Vector(200, 200));
        var ink = Glyphs("Bow").Bounds;

        var placement = FloorLabel.Placement(ink, LabelEmSize, floor, transform, screenEmSize: 27);

        var acrossCells = (placement.Transform(ink.TopRight) - placement.Transform(ink.TopLeft)).Length / (floor.Across.Length * transform.Scale);
        acrossCells.Should().BeApproximately(0.1, 1e-9);
    }

    // The label's text is built at this size and scaled onto the floor, like ShipViewControl's.
    private const double LabelEmSize = 100;

    private static Geometry Glyphs(string text, double emSize = LabelEmSize) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), emSize, Brushes.White, 1)
            .BuildGeometry(new Point(0, 0));

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
