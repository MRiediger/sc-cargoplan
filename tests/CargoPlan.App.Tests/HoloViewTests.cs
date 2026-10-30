using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>Where the hologram lies and the hidden-line strips (D63). Its camera is the crates' (<see cref="CameraConformityTests"/>).</summary>
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
    public void A_holo_offset_moves_the_hologram_by_whole_cells()
    {
        // The ship file's holoOffset (K5.3b): from the middle on the racks, here one cell up; the racks stay.
        var bounds = ShipLoader.Load("hull_a").Bounds;
        var centred = HoloPlacement.Place(Box(30, 12.5f, 10), bounds);

        var lifted = HoloPlacement.Place(Box(30, 12.5f, 10), bounds, new ShipPoint(0, 0, 1));

        lifted.Offset.Should().Be(centred.Offset + new Vector3D(0, 0, 1));
        (lifted.Min.Z, lifted.Max.Z).Should().Be((centred.Min.Z + 1, centred.Max.Z + 1));
        (lifted.Min.X, lifted.Max.Y).Should().Be((centred.Min.X, centred.Max.Y));
    }

    [Fact]
    public void A_vertex_goes_to_cells_the_way_the_box_does()
    {
        var placed = HoloPlacement.Place(Box(30, 12.5f, 10), ShipLoader.Load("hull_a").Bounds);

        placed.ToCells(15, 6.25f, 10).Should().Be(new Point3D(placed.Max.X, placed.Max.Y, placed.Max.Z));
        placed.ToCells(-15, -6.25f, 0).Should().Be(new Point3D(placed.Min.X, placed.Min.Y, placed.Min.Z));
    }

    // --- Hidden-line strips ---

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(4)]
    public void A_strip_is_as_wide_on_screen_at_every_zoom(double zoom)
    {
        // At the target's plane, where the strip's middle lies; the lift towards the camera widens it by d / (d − lift).
        Point3D target = new(2, 0, 0);
        Size view = new(800, 600);
        var focal = new CameraMath(CameraOrientation.Default, 1, target, view).Focal;
        CameraMath camera = new(CameraOrientation.Default, focal / (25 * zoom), target, view);
        Point3D[] points = [new(0, 0, 0), new(4, 0, 0)];

        var (positions, indices) = HoloLines.Strips(points, [0, 1], camera.LookDirection, HoloLines.HalfWidthCells(camera.PxPerCellAtTarget));

        positions.Should().HaveCount(4);
        indices.Should().HaveCount(6);
        Point3D Middle(Point3D a, Point3D b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
        var across = camera.Project(Middle(positions[1], positions[2])) - camera.Project(Middle(positions[0], positions[3]));
        across.Length.Should().BeApproximately(HoloLines.WidthPixels * camera.Distance / (camera.Distance - HoloLines.LiftCells), 1e-9);
    }

    [Fact]
    public void A_strip_lies_a_little_towards_the_camera_so_the_surface_under_it_doesnt_hide_it()
    {
        var camera = CameraOrientation.Default;
        var look = Look(camera);

        var (positions, _) = HoloLines.Strips([new(0, 0, 0), new(4, 0, 0)], [0, 1], look, 0.01);

        positions.Should().OnlyContain(p => Vector3D.DotProduct((Vector3D)p, look) < 0);
    }

    // WPF draws a triangle's front where its corners run counter-clockwise on screen; a model without BackMaterial shows
    // nothing from behind. The scene's edges have none, so a strip turned away from the camera vanished (T8.2 → T8.5).
    [Fact]
    public void A_strip_shows_its_front_to_the_camera()
    {
        var look = Look(CameraOrientation.Default);

        var (positions, indices) = HoloLines.Strips([new(0, 0, 0), new(4, 0, 0)], [0, 1], look, 0.01);

        for (var t = 0; t < indices.Length; t += 3)
        {
            var (a, b, c) = (positions[indices[t]], positions[indices[t + 1]], positions[indices[t + 2]]);
            Vector3D.DotProduct(Vector3D.CrossProduct(b - a, c - a), look).Should().BeNegative($"triangle {t / 3}");
        }
    }

    [Fact]
    public void An_edge_seen_end_on_draws_nothing()
    {
        var look = new Vector3D(-1, 0, 0);

        var (positions, indices) = HoloLines.Strips([new(0, 0, 0), new(4, 0, 0)], [0, 1], look, 0.01);

        positions.Should().BeEmpty();
        indices.Should().BeEmpty();
    }

    private static Vector3D Look(CameraOrientation camera) => new(camera.ViewDirection.X, camera.ViewDirection.Y, camera.ViewDirection.Z);
}
