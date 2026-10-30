using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>The bow wedge (D52) in ship coordinates, and where the perspective camera shows it (D83).</summary>
public sealed class BowWedgeTests
{
    private static readonly ShipScene Scene = BuildSample();

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
        return ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));
    }

    private static Point Centre(IReadOnlyList<Point> points) => new(points.Average(p => p.X), points.Average(p => p.Y));

    [Theory]
    [InlineData(4, 1)] // Hull A: 10 % of the length is 0.4 cells, the nose keeps its minimum
    [InlineData(20, 2)] // 10 % of the length
    public void Bow_wedge_has_a_tip_top_bottom_and_two_flanks_beyond_the_bow(int length, double nose)
    {
        ShipBox bounds = new(new ShipPoint(0, -2, 0), new ShipPoint(length, 3, 5));

        var wedge = BowWedge.Faces(bounds);

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
    public void Bow_wedge_and_its_floor_point_bowwards_on_screen(double yaw)
    {
        // Which faces show and what covers what is the depth buffer's (D83); the wedge only has to lie at the bow.
        var (min, max) = (Scene.Bounds.Min, Scene.Bounds.Max);
        Point3D middle = new((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
        CameraMath camera = new(new IsoProjection(yaw, IsoProjection.DefaultPitch), 30, middle, new Size(400, 400));
        Point[] tip = [.. BowWedge.Faces(Scene.Bounds).Single(f => f.Face == BoxFace.Bow).Corners.Select(camera.Project)];
        var floor = BowWedge.Floor(Scene.Bounds, camera.Project);

        var boxCentre = camera.Project(middle);
        var bowwards = camera.Project(middle + new Vector3D(1, 0, 0)) - boxCentre;
        ((Centre(tip) - boxCentre) * bowwards).Should().BePositive();
        ((floor.Centre - boxCentre) * bowwards).Should().BePositive();
        if (yaw == IsoProjection.DefaultYaw)
        {
            // The bow stays on the left in the default view (D15), and so does its label.
            Centre(tip).X.Should().BeLessThan(boxCentre.X);
            floor.Centre.X.Should().BeLessThan(boxCentre.X);
        }
    }
}
