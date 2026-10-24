using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>Where the hologram lies, how the 3D camera follows the ship view's, and the hidden-line strips (D63).</summary>
public sealed class HoloViewTests
{
    private static HoloMesh Box(float length, float width, float height)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        Vector3[] scaled = [.. vertices.Select(v => new Vector3((v.X - 0.5f) * length, (v.Y - 0.5f) * width, v.Z * height))];
        return HoloReader.Read(HoloFixtures.Write(scaled, triangles, HoloFixtures.CubeEdges(), 4, 8, length));
    }

    // --- Placement ---

    [Fact]
    public void The_hologram_is_true_to_scale_centred_on_the_racks_with_its_middle_at_their_middle()
    {
        var hullA = ShipLoader.Load("hull_a");
        var bounds = hullA.Bounds;

        // 30 m × 12.5 m × 10 m: 24 × 10 × 8 cells.
        var placed = HoloPlacement.Place(Box(30, 12.5f, 10), bounds);

        (placed.Max.X - placed.Min.X).Should().BeApproximately(24, 1e-6);
        (placed.Max.Y - placed.Min.Y).Should().BeApproximately(10, 1e-6);
        (placed.Max.Z - placed.Min.Z).Should().BeApproximately(8, 1e-6);
        ((placed.Min.X + placed.Max.X) / 2).Should().BeApproximately((bounds.Min.X + bounds.Max.X) / 2.0, 1e-6);
        ((placed.Min.Y + placed.Max.Y) / 2).Should().BeApproximately((bounds.Min.Y + bounds.Max.Y) / 2.0, 1e-6);
        ((placed.Min.Z + placed.Max.Z) / 2).Should().BeApproximately((bounds.Min.Z + bounds.Max.Z) / 2.0, 1e-6);
    }

    [Fact]
    public void A_vertex_goes_to_cells_the_way_the_box_does()
    {
        var placed = HoloPlacement.Place(Box(30, 12.5f, 10), ShipLoader.Load("hull_a").Bounds);

        placed.ToCells(15, 6.25f, 10).Should().Be(new Point3D(placed.Max.X, placed.Max.Y, placed.Max.Z));
        placed.ToCells(-15, -6.25f, 0).Should().Be(new Point3D(placed.Min.X, placed.Min.Y, placed.Min.Z));
    }

    // --- Camera ---

    [Theory]
    [InlineData(45, 35.26, 1, 0, 0)]
    [InlineData(70, 35.26, 2.5, 40, -30)]
    [InlineData(200, -60, 0.7, -10, 25)]
    [InlineData(315, 85, 4, 3, 3)]
    public void The_3d_camera_puts_every_point_where_the_ship_view_draws_it(double yaw, double pitch, double scale, double offsetX, double offsetY)
    {
        IsoProjection camera = new(yaw, pitch);
        ViewTransform transform = new(scale * 20, new System.Windows.Vector(400 + offsetX, 300 + offsetY));
        Size viewport = new(800, 600);

        var view = HoloCamera.For(camera, transform, viewport);

        // WPF's orthographic camera: right = look × up, the viewport's width spans Width world units.
        var right = Vector3D.CrossProduct(view.LookDirection, view.UpDirection);
        var pixelsPerUnit = viewport.Width / view.Width;
        foreach (var (x, y, z) in new[] { (0.0, 0.0, 0.0), (4, 2, 2), (-7.5, 3.25, 9), (30, -12, -4) })
        {
            var p = new Point3D(x, y, z) - view.Position;
            Point wpf = new(viewport.Width / 2 + Vector3D.DotProduct(p, right) * pixelsPerUnit, viewport.Height / 2 - Vector3D.DotProduct(p, view.UpDirection) * pixelsPerUnit);

            var expected = transform.Apply(camera.Project(x, y, z));

            wpf.X.Should().BeApproximately(expected.X, 1e-6);
            wpf.Y.Should().BeApproximately(expected.Y, 1e-6);
            Vector3D.DotProduct(p, view.LookDirection).Should().BeInRange(1, 2 * HoloCamera.Distance, because: "the point lies in front of the camera, inside its far plane");
        }
    }

    // --- Hidden-line strips ---

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(4)]
    public void A_strip_is_as_wide_on_screen_at_every_zoom(double zoom)
    {
        var camera = IsoProjection.Default;
        var scale = 25 * zoom;
        Point3D[] points = [new(0, 0, 0), new(4, 0, 0)];

        var (positions, indices) = HoloLines.Strips(points, [0, 1], Look(camera), HoloLines.HalfWidthCells(scale));

        positions.Should().HaveCount(4);
        indices.Should().HaveCount(6);
        var across = camera.Project(positions[1].X, positions[1].Y, positions[1].Z) - camera.Project(positions[0].X, positions[0].Y, positions[0].Z);
        (across.Length * scale).Should().BeApproximately(HoloLines.WidthPixels, 1e-9);
    }

    [Fact]
    public void A_strip_lies_a_little_towards_the_camera_so_the_surface_under_it_doesnt_hide_it()
    {
        var camera = IsoProjection.Default;
        var look = Look(camera);

        var (positions, _) = HoloLines.Strips([new(0, 0, 0), new(4, 0, 0)], [0, 1], look, 0.01);

        positions.Should().OnlyContain(p => Vector3D.DotProduct((Vector3D)p, look) < 0);
    }

    [Fact]
    public void An_edge_seen_end_on_draws_nothing()
    {
        var look = new Vector3D(-1, 0, 0);

        var (positions, indices) = HoloLines.Strips([new(0, 0, 0), new(4, 0, 0)], [0, 1], look, 0.01);

        positions.Should().BeEmpty();
        indices.Should().BeEmpty();
    }

    private static Vector3D Look(IsoProjection camera) => new(camera.ViewDirection.X, camera.ViewDirection.Y, camera.ViewDirection.Z);
}
