using System.Diagnostics;
using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class SceneProjectorTests
{
    private const double DefaultYaw = IsoProjection.DefaultYaw;
    private static readonly double DefaultPitch = IsoProjection.DefaultPitch;

    private static readonly ShipScene Scene = BuildSample();

    private static ShipScene BuildSample()
    {
        var (ship, plan) = SamplePlan.Load();
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
    public void Every_rack_shows_its_visible_faces_and_strips_on_them()
    {
        foreach (var rack in SceneProjector.Project(Scene, IsoProjection.Default).Racks)
        {
            rack.Faces.Select(f => f.Face).Should().Equal(BoxFace.Top, BoxFace.Port, BoxFace.Bow);
            rack.Faces.Should().AllSatisfy(f => f.Polygon.Should().HaveCount(4));
            rack.Strips.Select(s => s.Face).Distinct().Should().BeEquivalentTo(new[] { BoxFace.Top, BoxFace.Port, BoxFace.Bow });
        }
    }

    [Fact]
    public void Racks_come_back_to_front()
    {
        SceneProjector.Project(Scene, IsoProjection.Default).Racks.Select(r => r.Number).Should().Equal(4, 2, 3, 1);
    }

    [Fact]
    public void The_bow_face_shows_only_the_bow_end_segment()
    {
        var projected = SceneProjector.Project(Scene, IsoProjection.Default);

        foreach (var rack in projected.Racks)
        {
            var first = Scene.Racks.Single(r => r.Number == rack.Number).Segments[0];
            rack.Strips.Where(s => s.Face == BoxFace.Bow).Should().ContainSingle().Which.ToolTip.Should().Be(first.ToolTip);
        }
    }

    [Fact]
    public void The_aft_face_shows_only_the_aft_end_segment()
    {
        // Rack 3 holds 14/16 SCU: its free rest is the aft end.
        var rack3 = RackNumber(SceneProjector.Project(Scene, new IsoProjection(DefaultYaw + 180, DefaultPitch)), 3);

        rack3.Strips.Where(s => s.Face == BoxFace.Aft).Should().ContainSingle().Which.Fill.Should().Be(SegmentFill.Free);
    }

    [Fact]
    public void The_bottom_shows_the_same_segments_as_the_top()
    {
        var above = SceneProjector.Project(Scene, IsoProjection.Default);
        var below = SceneProjector.Project(Scene, new IsoProjection(DefaultYaw, -DefaultPitch));

        foreach (var rack in above.Racks)
        {
            var top = rack.Strips.Where(s => s.Face == BoxFace.Top).Select(s => (s.Fill, s.PaletteIndex, s.ToolTip));
            var bottom = RackNumber(below, rack.Number).Strips.Where(s => s.Face == BoxFace.Bottom).Select(s => (s.Fill, s.PaletteIndex, s.ToolTip));
            bottom.Should().Equal(top);
        }
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
}
