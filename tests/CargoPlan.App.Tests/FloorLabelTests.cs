using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>The "Bow" label on the wedge's floor (D52), laid through the perspective camera (D83) as the overlay lays it.</summary>
public sealed class FloorLabelTests
{
    // The label's text is built at this size and laid onto the floor, like ShipOverlay's.
    private const double LabelEmSize = 100;

    private static readonly Size View = new(400, 400);

    private static readonly ShipScene Scene = BuildSample();

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
        return ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));
    }

    private static Geometry Glyphs(string text, double emSize = LabelEmSize) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), emSize, Brushes.White, 1)
            .BuildGeometry(new Point(0, 0));

    /// <summary>A camera <paramref name="pxPerCell"/> DIPs per cell at the middle of the wedge's floor, where the label lies.</summary>
    private static CameraMath AtFloor(double yaw, double pxPerCell = 40)
    {
        var bounds = Scene.Bounds;
        var tipX = BowWedge.Faces(bounds).Single(f => f.Face == BoxFace.Bow).Corners[0].X;
        Point3D middle = new((bounds.Max.X + tipX) / 2, (bounds.Min.Y + bounds.Max.Y) / 2.0, bounds.Min.Z);
        return new CameraMath(new CameraOrientation(yaw, CameraOrientation.DefaultPitch), DistanceLimits.Default.DistanceFor(pxPerCell, View), middle, View);
    }

    /// <summary>One em of the label along its upright axis, in screen pixels.</summary>
    private static double ScreenEm(Matrix placement) => new Vector(placement.M21, placement.M22).Length * LabelEmSize;

    /// <summary>Even-odd rule: a ray from the point crosses the outline an odd number of times if the point is inside.</summary>
    private static bool Inside(IReadOnlyList<Point> polygon, Point point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    [Theory]
    [InlineData(45)]
    [InlineData(135)]
    [InlineData(225)]
    [InlineData(315)]
    public void Bow_label_lies_on_the_wedge_floor(double yaw)
    {
        var camera = AtFloor(yaw);
        var label = FloorLabel.Lay(Glyphs("Bow"), LabelEmSize, BowWedge.Floor(Scene.Bounds, camera.Project), screenEmSize: 27);

        // Flattened into screen pixels: no transform left that would skew a pen's outline.
        (label.Transform?.Value ?? Matrix.Identity).IsIdentity.Should().BeTrue();
        Point[] floor = [.. BowWedge.Faces(Scene.Bounds).Single(f => f.Face == BoxFace.Bottom).Corners.Select(camera.Project)];
        Point[] points = [.. label.Figures.SelectMany(f => f.Segments
            .SelectMany(s => s switch
            {
                PolyLineSegment poly => poly.Points,
                LineSegment line => [line.Point],
                _ => throw new InvalidOperationException($"Not flattened: {s.GetType().Name}"),
            })
            .Prepend(f.StartPoint))];
        points.Should().NotBeEmpty().And.OnlyContain(p => Inside(floor, p));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Bow_label_B_is_as_tall_on_screen_as_a_rack_number_digit(double zoom)
    {
        // The default overlay's ship view, as in RackNumberSizeTests, at the fit and zoomed in.
        Size view = new(400, 302);
        var basis = CameraBasis.For(Scene, CameraOrientation.Default, view, margin: 8);
        var fit = CameraNavigation.Default.On(basis, DistanceLimits.Default, view);
        var camera = fit with { Distance = fit.Distance / zoom };
        var numberSize = RackNumberSize.For(27, camera.PxPerCellAtTarget, Scene.Racks);

        // The B of "Bow" from its top to its baseline, through the label's matrix onto the screen.
        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, BowWedge.Floor(Scene.Bounds, camera.Project), numberSize);
        var b = Glyphs("B").Bounds;
        var bOnScreen = (placement.Transform(b.TopLeft) - placement.Transform(b.BottomLeft)).Length;

        bOnScreen.Should().BeApproximately(Glyphs("1", numberSize).Bounds.Height, 1);
    }

    [Fact]
    public void Bow_label_keeps_to_a_floor_with_room_for_the_minimum()
    {
        // One cell across at 40 px: room for about 21 px, less than the setting, more than the minimum.
        var floor = BowWedge.Floor(Scene.Bounds, AtFloor(CameraOrientation.DefaultYaw).Project) with { Room = new Size(1, 0.7) };
        var ink = Glyphs("Bow").Bounds;

        var placement = FloorLabel.Placement(ink, LabelEmSize, floor, screenEmSize: 27);

        var acrossCells = (placement.Transform(ink.TopRight) - placement.Transform(ink.TopLeft)).Length / floor.Across.Length;
        acrossCells.Should().BeApproximately(1, 1e-9);
        ScreenEm(placement).Should().BeInRange(RackNumberSize.MinSize, 27);
    }

    [Fact]
    public void Bow_label_on_a_short_floor_shrinks_only_to_the_minimum_like_a_rack_number()
    {
        // A floor of a narrow ship: a tenth of a cell of room across, far less than a rack number needs (D32, D61).
        var floor = BowWedge.Floor(Scene.Bounds, AtFloor(CameraOrientation.DefaultYaw).Project) with { Room = new Size(0.1, 0.7) };

        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, floor, screenEmSize: 27);

        ScreenEm(placement).Should().BeApproximately(RackNumberSize.MinSize, 1e-9);
    }

    [Fact]
    public void Bow_label_keeps_a_setting_below_the_minimum()
    {
        var floor = BowWedge.Floor(Scene.Bounds, AtFloor(CameraOrientation.DefaultYaw).Project) with { Room = new Size(0.1, 0.7) };

        var placement = FloorLabel.Placement(Glyphs("Bow").Bounds, LabelEmSize, floor, screenEmSize: 8);

        ScreenEm(placement).Should().BeApproximately(8, 1e-9);
    }
}
