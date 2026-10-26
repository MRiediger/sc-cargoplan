using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

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
        // 3 before 2 since K5.2's wider spindle gap (D78), as in DrawOrderTests.
        SceneProjector.Project(Scene, IsoProjection.Default).Racks.Select(r => r.Number).Should().Equal(4, 3, 2, 1);
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
            .Concat(projected.BowWedge!.Faces.SelectMany(f => f.Polygon))
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
        projected.BowWedge!.InFront.Should().Be(bowSide);
        projected.BowWedge!.Faces.Any(f => f.Face == BoxFace.Bow).Should().Be(bowSide);

        var boxCentre = camera.Project((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
        var bowwards = camera.Project(1, 0, 0) - camera.Project(0, 0, 0);
        ((Centre(tip) - boxCentre) * bowwards).Should().BePositive();
        ((projected.BowWedge!.Floor.Centre - boxCentre) * bowwards).Should().BePositive();
        if (yaw == DefaultYaw)
        {
            // The bow stays on the left in the default view (D15), and so does its label.
            Centre(tip).X.Should().BeLessThan(boxCentre.X);
            projected.BowWedge!.Floor.Centre.X.Should().BeLessThan(boxCentre.X);
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
        var label = FloorLabel.Lay(Glyphs("Bow"), LabelEmSize, projected.BowWedge!.Floor, transform, screenEmSize: 27);

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
        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, projected.BowWedge!.Floor, transform, numberSize);
        var b = Glyphs("B").Bounds;
        var bOnScreen = (placement.Transform(b.TopLeft) - placement.Transform(b.BottomLeft)).Length;

        bOnScreen.Should().BeApproximately(Glyphs("1", numberSize).Bounds.Height, 1);
    }

    [Fact]
    public void Bow_label_keeps_to_a_floor_with_room_for_the_minimum()
    {
        // One cell across at 40 px: room for about 21 px, less than the setting, more than the minimum.
        var floor = SceneProjector.Project(Scene, IsoProjection.Default).BowWedge!.Floor with { Room = new Size(1, 0.7) };
        ViewTransform transform = new(40, new Vector(200, 200));
        var ink = Glyphs("Bow").Bounds;

        var placement = FloorLabel.Placement(ink, LabelEmSize, floor, transform, screenEmSize: 27);

        var acrossCells = (placement.Transform(ink.TopRight) - placement.Transform(ink.TopLeft)).Length / (floor.Across.Length * transform.Scale);
        acrossCells.Should().BeApproximately(1, 1e-9);
        ScreenEm(placement).Should().BeInRange(RackNumberSize.MinSize, 27);
    }

    [Fact]
    public void Bow_label_on_a_short_floor_shrinks_only_to_the_minimum_like_a_rack_number()
    {
        // A floor of a narrow ship: a tenth of a cell of room across, far less than a rack number needs (D32, D61).
        var floor = SceneProjector.Project(Scene, IsoProjection.Default).BowWedge!.Floor with { Room = new Size(0.1, 0.7) };
        ViewTransform transform = new(40, new Vector(200, 200));

        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, floor, transform, screenEmSize: 27);

        ScreenEm(placement).Should().BeApproximately(RackNumberSize.MinSize, 1e-9);
    }

    [Fact]
    public void Bow_label_keeps_a_setting_below_the_minimum()
    {
        var floor = SceneProjector.Project(Scene, IsoProjection.Default).BowWedge!.Floor with { Room = new Size(0.1, 0.7) };
        ViewTransform transform = new(40, new Vector(200, 200));

        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, floor, transform, screenEmSize: 8);

        ScreenEm(placement).Should().BeApproximately(8, 1e-9);
    }

    /// <summary>One em of the label along its upright axis, in screen pixels.</summary>
    private static double ScreenEm(Matrix placement) => new Vector(placement.M21, placement.M22).Length * LabelEmSize;

    private static readonly Ship Caterpillar = ShipLoader.Load("caterpillar");

    /// <summary>The Caterpillar with one 1 SCU crate in each of these racks.</summary>
    private static ShipScene CaterpillarWith(params string[] rackIds) => ShipViewBuilder.Build(
        TestPlans.Of(Caterpillar, [.. rackIds.Select(id => new Placement("Riker", id, new CargoItem("Titanium", 1, CrateSize.One)))]),
        Caterpillar,
        []);

    private static Point[] Corners(ShipBox box, IsoProjection camera) =>
    [
        .. from x in new[] { box.Min.X, box.Max.X }
           from y in new[] { box.Min.Y, box.Max.Y }
           from z in new[] { box.Min.Z, box.Max.Z }
           select camera.Project(x, y, z),
    ];

    private static void ShouldEnclose(Rect rect, IEnumerable<Point> points) => points.Should().OnlyContain(p =>
        p.X >= rect.Left - 1e-9 && p.X <= rect.Right + 1e-9 && p.Y >= rect.Top - 1e-9 && p.Y <= rect.Bottom + 1e-9);

    [Fact]
    public void Fit_extent_of_a_compact_ship_is_its_extent()
    {
        var projected = SceneProjector.Project(Scene, IsoProjection.Default);

        projected.FitExtent.Should().Be(projected.Extent);
    }

    [Fact]
    public void Fit_extent_of_a_long_ship_without_cargo_is_its_extent()
    {
        var projected = SceneProjector.Project(CaterpillarWith(), IsoProjection.DefaultFor(Caterpillar.Bounds));

        projected.FitExtent.Should().Be(projected.Extent);
    }

    [Theory]
    [InlineData(90, 35.26)]
    [InlineData(45, 35.26)]
    [InlineData(225, -40)]
    public void Fit_extent_of_a_long_ship_holds_its_loaded_racks_and_the_bow_wedge(double yaw, double pitch)
    {
        // Racks 1 and 3 hold cargo; rack 2 lies between them, racks 4 to 14 further aft (D61).
        var camera = new IsoProjection(yaw, pitch);
        var projected = SceneProjector.Project(CaterpillarWith("rack-0", "rack-6"), camera);
        var rack = Caterpillar.Racks.ToDictionary(r => r.Number, r => r.Bounds);
        Point[] wanted =
        [
            .. Corners(rack[1], camera),
            .. Corners(rack[3], camera),
            .. SceneProjector.BowWedge(Caterpillar.Bounds).SelectMany(f => f.Corners).Select(c => camera.Project(c.X, c.Y, c.Z)),
        ];
        var enclosing = wanted.Aggregate(new Rect(wanted[0], wanted[0]), (r, p) => Rect.Union(r, p));

        ShouldEnclose(projected.FitExtent, wanted);
        projected.FitExtent.Width.Should().BeApproximately(enclosing.Width, 1e-9);
        projected.FitExtent.Height.Should().BeApproximately(enclosing.Height, 1e-9);
        ShouldEnclose(projected.Extent, Corners(rack[14], camera));
        projected.FitExtent.Width.Should().BeLessThan(projected.Extent.Width);
    }

    [Theory]
    [InlineData(70, 35.26)]
    [InlineData(225, -40)]
    public void A_hologram_is_fitted_whole_even_on_a_long_ship_with_cargo(double yaw, double pitch)
    {
        // A box longer than the Caterpillar's racks: the hull may reach beyond them, and the fit shows all of it (D63).
        var camera = new IsoProjection(yaw, pitch);
        var bounds = Caterpillar.Bounds;
        SceneProjector.Corner min = new(bounds.Min.X - 10, bounds.Min.Y - 3, bounds.Min.Z - 2);
        SceneProjector.Corner max = new(bounds.Max.X + 12, bounds.Max.Y + 3, bounds.Max.Z + 4);
        var scene = CaterpillarWith("rack-0", "rack-6") with { Marker = new BowMarker.Holo(min, max) };

        var projected = SceneProjector.Project(scene, camera);

        projected.FitExtent.Should().Be(projected.Extent);
        ShouldEnclose(projected.Extent, from x in new[] { min.X, max.X } from y in new[] { min.Y, max.Y } from z in new[] { min.Z, max.Z } select camera.Project(x, y, z));
        projected.BowWedge.Should().BeNull();
        projected.Outline.Should().BeNull();
    }

    [Theory]
    [InlineData(70, 35.26)]
    [InlineData(225, -40)]
    public void The_anchor_is_the_projected_centre_of_the_ship_whatever_is_loaded(double yaw, double pitch)
    {
        // The player's framing hangs on it (D76): a sync loading a rack further aft moves the fit, never the anchor.
        var camera = new IsoProjection(yaw, pitch);
        var bounds = Caterpillar.Bounds;
        var centre = camera.Project((bounds.Min.X + bounds.Max.X) / 2.0, (bounds.Min.Y + bounds.Max.Y) / 2.0, (bounds.Min.Z + bounds.Max.Z) / 2.0);

        var bowOnly = SceneProjector.Project(CaterpillarWith("rack-0"), camera);
        var bowAndAft = SceneProjector.Project(CaterpillarWith("rack-0", "rack-13"), camera);

        bowOnly.Anchor.Should().Be(centre);
        bowAndAft.Anchor.Should().Be(centre);
        bowAndAft.FitExtent.Should().NotBe(bowOnly.FitExtent);
    }

    [Fact]
    public void Projected_racks_know_whether_they_hold_cargo()
    {
        var projected = SceneProjector.Project(CaterpillarWith("rack-0"), IsoProjection.Default);

        projected.Racks.Where(r => r.IsLoaded).Select(r => r.Number).Should().Equal(1);
    }

    [Theory]
    [InlineData(90, 35.26)]
    [InlineData(45, 35.26)]
    [InlineData(225, 35.26)]
    [InlineData(100, -30)]
    public void On_a_long_ship_the_rack_number_stands_above_its_rack(double yaw, double pitch)
    {
        // Above the outline, in the middle of its width: never over a crate of its own rack, even a full one (D61).
        foreach (var rack in SceneProjector.Project(CaterpillarWith("rack-0"), new IsoProjection(yaw, pitch)).Racks)
        {
            Point[] outline = [.. rack.Faces.SelectMany(f => f.Polygon)];

            rack.LabelAbove.Should().BeTrue();
            rack.LabelCentre.Y.Should().BeApproximately(outline.Min(p => p.Y), 1e-9);
            rack.LabelCentre.X.Should().BeApproximately((outline.Min(p => p.X) + outline.Max(p => p.X)) / 2, 1e-9);
        }
    }

    [Fact]
    public void On_a_compact_ship_the_rack_number_stays_on_its_face()
    {
        SceneProjector.Project(Scene, IsoProjection.Default).Racks.Should().OnlyContain(r => !r.LabelAbove);
    }

    // The label's text is built at this size and scaled onto the floor, like ShipViewControl's.
    private const double LabelEmSize = 100;

    private static Geometry Glyphs(string text, double emSize = LabelEmSize) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), emSize, Brushes.White, 1)
            .BuildGeometry(new Point(0, 0));

    // --- Bow marker (D62): outline, none, or the wedge for ships without an outline ---

    // An arrow 20 m long, 8 m wide, pointing to the bow, placed on the scene's racks as the overlay does.
    private static readonly ShipOutline Arrow = new("arrow", [new(10, 0), new(-10, 4), new(-10, -4)]);

    private static ShipScene WithOutline(ShipScene scene) =>
        scene with { Marker = new BowMarker.Outline(OutlinePlacement.Place(Arrow, scene.Bounds)) };

    private static ShipScene WithoutMarker(ShipScene scene) => scene with { Marker = new BowMarker.None() };

    [Fact]
    public void A_scene_shows_the_bow_wedge_unless_told_otherwise()
    {
        // Ships without an outline, and every scene built in tests, keep the wedge with its label (D52).
        var projected = SceneProjector.Project(Scene, IsoProjection.Default);

        Scene.Marker.Should().BeOfType<BowMarker.Wedge>();
        projected.BowWedge.Should().NotBeNull();
        projected.Outline.Should().BeNull();
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(225, -40)]
    public void With_an_outline_there_is_no_wedge_and_the_outline_is_projected_on_the_floor(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);
        var scene = WithOutline(Scene);

        var projected = SceneProjector.Project(scene, camera);

        projected.BowWedge.Should().BeNull();
        projected.Outline.Should().Equal(((BowMarker.Outline)scene.Marker).Points.Select(p => camera.Project(p.X, p.Y, p.Z)));
    }

    // Since K5.2c the outline is at least true size, mostly far larger than the racks: with it a ship fits as with the
    // wedge, its racks and a short bow zone, a tenth of their length (D52, D62 addendum); the hull beyond may be cut.
    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(0, 0)]
    [InlineData(270, 85)]
    public void A_compact_ship_with_an_outline_fits_as_with_the_wedge(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);

        var projected = SceneProjector.Project(WithOutline(Scene), camera);

        projected.FitExtent.Should().Be(SceneProjector.Project(Scene, camera).FitExtent);
        ShouldEnclose(projected.Extent, projected.Outline!);
    }

    [Theory]
    [InlineData(70, 35.26)]
    [InlineData(225, -40)]
    public void A_long_ship_with_an_outline_fits_its_loaded_racks_as_with_the_wedge_but_not_the_whole_outline(double yaw, double pitch)
    {
        // Racks 1 and 3 hold cargo (D61).
        var camera = new IsoProjection(yaw, pitch);
        var scene = CaterpillarWith("rack-0", "rack-6");

        var projected = SceneProjector.Project(WithOutline(scene), camera);

        projected.FitExtent.Should().Be(SceneProjector.Project(scene, camera).FitExtent);
        ShouldEnclose(projected.Extent, projected.Outline!);
        projected.FitExtent.Width.Should().BeLessThan(projected.Extent.Width);
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(225, -40)]
    public void Without_a_marker_there_is_neither_wedge_nor_outline_and_the_extent_is_the_frame(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);

        var projected = SceneProjector.Project(WithoutMarker(Scene), camera);

        projected.BowWedge.Should().BeNull();
        projected.Outline.Should().BeNull();
        Point[] frame = [.. Corners(Scene.Bounds, camera)];
        projected.Extent.Should().Be(frame.Aggregate(new Rect(frame[0], frame[0]), (r, p) => Rect.Union(r, p)));
        projected.FitExtent.Should().Be(projected.Extent);
    }

    [Fact]
    public void Without_a_marker_a_long_ship_fits_its_loaded_racks_only()
    {
        var camera = IsoProjection.DefaultFor(Caterpillar.Bounds);
        Point[] loaded = [.. Corners(Caterpillar.Racks[0].Bounds, camera)];

        var projected = SceneProjector.Project(WithoutMarker(CaterpillarWith("rack-0")), camera);

        projected.FitExtent.Should().Be(loaded.Aggregate(new Rect(loaded[0], loaded[0]), (r, p) => Rect.Union(r, p)));
    }

    [Fact]
    public void Projecting_the_largest_outline_stays_fast_enough_for_dragging()
    {
        // Gate polygon of D55: the Hull D's 362 points, the largest of the file; a full turn like the test below.
        var hullD = new ShipOutline("hull-d", [.. Enumerable.Range(0, 362).Select(i => new OutlinePoint(100 * Math.Cos(i * Math.Tau / 362), 38 * Math.Sin(i * Math.Tau / 362)))]);
        var scene = Scene with { Marker = new BowMarker.Outline(OutlinePlacement.Place(hullD, Scene.Bounds)) };

        var watch = Stopwatch.StartNew();
        for (var yaw = 0; yaw < 360; yaw++)
        {
            SceneProjector.Project(scene, new IsoProjection(yaw, DefaultPitch));
        }

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
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
