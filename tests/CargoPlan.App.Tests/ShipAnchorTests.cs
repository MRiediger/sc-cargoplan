using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The point of F7 (D90): a corrected rack moves that rack and nothing else. A copy of the Hull A (the frozen test copy,
/// D92) with Rack 1 one cell further to the bow. The values in the ship files come from the pipeline (T12.5, D113).
/// </summary>
public sealed class ShipAnchorTests
{
    private static readonly ShipOutlineCatalog Outlines = FrozenOutlines.Load();
    private static readonly Ship Before = TestPlans.HullA();
    private static readonly Ship After = Corrected();

    private static Ship Corrected()
    {
        var folder = Directory.CreateTempSubdirectory("cargoplan-anchor-").FullName;
        try
        {
            var text = File.ReadAllText(Path.Combine(TestPlans.TestShipsFolder, "hull_a.json"));
            File.WriteAllText(Path.Combine(folder, "hull_a.json"), text.Replace("\"origin\": [0, 1, 3]", "\"origin\": [1, 1, 3]", StringComparison.Ordinal));
            return ShipLoader.Load(folder, "hull_a");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // 30 m × 12.5 m × 10 m, origin at the middle of its x/y box on its keel like a real hologram.
    private static HoloMesh Hull(float length = 30, float width = 12.5f, float height = 10, float shiftX = 0)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        Vector3[] scaled = [.. vertices.Select(v => new Vector3((v.X - 0.5f) * length + shiftX, (v.Y - 0.5f) * width, v.Z * height))];
        return HoloReader.Read(HoloFixtures.Write(scaled, triangles, HoloFixtures.CubeEdges(), 4, 8, length));
    }

    [Fact]
    public void Only_the_corrected_rack_moves()
    {
        After.Racks[0].Bounds.Should().Be(new ShipBox(new ShipPoint(1, 1, 3), new ShipPoint(5, 3, 5)));
        Before.Racks[0].Bounds.Should().Be(new ShipBox(new ShipPoint(0, 1, 3), new ShipPoint(4, 3, 5)));
        After.Racks.Skip(1).Select(r => r.Bounds).Should().Equal(Before.Racks.Skip(1).Select(r => r.Bounds));
    }

    [Fact]
    public void The_corrected_rack_leaves_the_hologram_where_it_was()
    {
        var mesh = Hull();

        HoloPlacement.Place(mesh, After).Should().Be(HoloPlacement.Place(mesh, Before));
    }

    [Fact]
    public void The_corrected_rack_leaves_the_outline_where_it_was_at_its_size()
    {
        var outline = Outlines.For(Before)!;

        OutlinePlacement.Place(outline, After).Should().Equal(OutlinePlacement.Place(outline, Before));
    }

    private static Point3D FitTarget(ShipScene scene) =>
        CameraBasis.For(scene, CameraOrientation.Default, new Size(400, 302), margin: 10).FitTarget;

    private static ShipScene SceneOf(Ship ship, BowMarker? marker = null)
    {
        var scene = new OverlayViewModel(ship) { Outlines = Outlines }.Scene;
        return marker is null ? scene : scene with { Marker = marker };
    }

    // The fit looks at the racks and the wedge's bow zone: x from 0..4 + nose to 0..5 + nose, so half a cell.
    [Fact]
    public void With_the_outline_the_fit_follows_the_rack()
    {
        var (before, after) = (FitTarget(SceneOf(Before)), FitTarget(SceneOf(After)));

        (after - before).Should().Be(new Vector3D(0.5, 0, 0));
    }

    // The hologram's box holds the rack before and after, so nothing in the fit changes.
    [Fact]
    public void With_a_hologram_the_fit_stays()
    {
        var placed = HoloPlacement.Place(Hull(), Before);
        BowMarker.Holo marker = new(placed.Min, placed.Max);

        FitTarget(SceneOf(After, marker)).Should().Be(FitTarget(SceneOf(Before, marker)));
    }
}
