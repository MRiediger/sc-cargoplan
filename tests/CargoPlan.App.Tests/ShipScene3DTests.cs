using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>The 3D scene's models (T8.2).</summary>
public class ShipScene3DTests
{
    private static readonly ShipBox Box = new(new ShipPoint(0, 0, 0), new ShipPoint(4, 2, 2));
    private static readonly Size View = new(800, 600);
    private static readonly Color Edge = Color.FromArgb(0xC0, 0xE6, 0xE6, 0xE6);

    // Hull A, three stops: Riker in rack 1 (two crates), Shubin in rack 2 (one), Orison delivered in rack 3, one crate off the ship.
    private static (ShipScene Scene, Crate[] Crates) HullA()
    {
        var ship = ShipLoader.Load("hull_a");
        var racks = ship.Racks;
        Crate[] crates =
        [
            new("Riker", "Gold", CrateSize.Four, racks[0].Id, new GridPosition(0, 0, 0), Mission: 1),
            new("Riker", "Gold", CrateSize.Four, racks[0].Id, new GridPosition(2, 0, 0), Mission: 1),
            new("Shubin", "Iron", CrateSize.Eight, racks[1].Id, new GridPosition(0, 0, 0), Mission: 2),
            new Crate("Orison", "Tin", CrateSize.Four, racks[2].Id, new GridPosition(0, 0, 0), Mission: 3).AsDelivered(),
            new Crate("Lorville", "Salt", CrateSize.Four, Crate.NoRack, new GridPosition(0, 0, 0), Mission: 4).AsDelivered(),
        ];
        return (ShipViewBuilder.Build(new LoadPlan(ship.Id, crates, []), ship, []), crates);
    }

    private static CrateLook Look(ShipScene scene, CrateSelection? selection = null) =>
        new(StopPalettes.OkabeIto, 0xD0, Edge, SelectionFocus.Missions((selection ?? CrateSelection.Empty).In(scene)));

    private static CameraMath Camera(ShipScene scene, double distance = 20) =>
        new(IsoProjection.Default, distance, ScreenPoints.Centre(scene.Bounds), View);

    [Fact]
    public void Every_crate_on_board_is_one_model_and_cargo_off_the_ship_none()
    {
        Sta.Run(() =>
        {
            var (scene, _) = HullA();
            var scene3D = new ShipScene3D();

            scene3D.Show(scene, Look(scene));

            scene3D.Crates.Children.Should().HaveCount(4, "three crates on board and one delivered in its rack; the crate off the ship isn't drawn (D75)");
        });
    }

    [Fact]
    public void Crates_of_one_stop_share_their_material_and_delivered_cargo_is_grey()
    {
        Sta.Run(() =>
        {
            var (scene, _) = HullA();
            var scene3D = new ShipScene3D();
            scene3D.Show(scene, Look(scene));
            scene3D.Follow(Camera(scene));

            var materials = scene3D.Crates.Children.Cast<GeometryModel3D>().Select(m => m.Material).ToArray();

            materials.Distinct().Should().HaveCount(3);
            materials.Should().Contain(CrateMaterials.Delivered(0xD0, dimmed: false));
        });
    }

    [Fact]
    public void Crates_are_drawn_from_the_back_to_the_front()
    {
        // Half-transparent crates blend over what was drawn before them; WPF doesn't sort (D83).
        Sta.Run(() =>
        {
            var (scene, _) = HullA();
            var scene3D = new ShipScene3D();
            scene3D.Show(scene, Look(scene));
            var camera = Camera(scene);

            scene3D.Follow(camera);

            double[] depths = [.. scene3D.Crates.Children.Cast<GeometryModel3D>().Select(m => Depth(m, camera))];
            depths.Should().BeInDescendingOrder();
        });
    }

    [Fact]
    public void The_hologram_stays_the_last_child()
    {
        Sta.Run(() =>
        {
            var scene3D = new ShipScene3D();

            scene3D.Visuals[^1].Should().BeSameAs(scene3D.HoloVisual);
        });
    }

    [Fact]
    public void A_selection_dims_the_crates_of_other_missions()
    {
        Sta.Run(() =>
        {
            var (scene, crates) = HullA();
            var scene3D = new ShipScene3D();
            scene3D.Show(scene, Look(scene, CrateSelection.Of(scene, [crates[2]])));
            scene3D.Follow(Camera(scene));

            var materials = scene3D.Crates.Children.Cast<GeometryModel3D>().Select(m => m.Material).ToArray();

            materials.Should().Contain(CrateMaterials.Fill(StopPalettes.OkabeIto, 0, 0xD0, dimmed: true, tileCells: 1));
            materials.Should().Contain(CrateMaterials.Fill(StopPalettes.OkabeIto, 1, 0xD0, dimmed: false, tileCells: 1));
        });
    }

    [Fact]
    public void The_free_grid_is_one_model_per_rack_once_a_cell_is_large_enough()
    {
        Sta.Run(() =>
        {
            var ship = ShipLoader.Load("hull_a");
            var scene = ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);
            var scene3D = new ShipScene3D();
            scene3D.Show(scene, Look(scene));

            scene3D.Follow(Camera(scene, distance: 400));
            scene3D.FreeGrid.Children.Should().BeEmpty("far out a cell is under 12 px and no layer holds cargo (D61)");

            scene3D.Follow(Camera(scene, distance: 10));
            scene3D.FreeGrid.Children.Should().HaveCount(ship.Racks.Count);
        });
    }

    private static double Depth(GeometryModel3D model, CameraMath camera)
    {
        var bounds = model.Bounds;
        Point3D centre = new(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
        return Vector3D.DotProduct(centre - camera.Position, camera.LookDirection);
    }

    [Fact]
    public void A_crate_carries_texture_coordinates_in_cells_on_every_face()
    {
        var mesh = ShipScene3D.Cuboid(Box);

        mesh.TextureCoordinates.Should().HaveCount(mesh.Positions.Count);
        for (var face = 0; face < 6; face++)
        {
            // Along each edge the texture moves by the edge's length: one unit per cell.
            for (var corner = 0; corner < 4; corner++)
            {
                var (i, j) = (4 * face + corner, 4 * face + (corner + 1) % 4);
                ((mesh.TextureCoordinates[j] - mesh.TextureCoordinates[i]).Length)
                    .Should().BeApproximately((mesh.Positions[j] - mesh.Positions[i]).Length, 1e-9);
            }
        }
    }

    [Fact]
    public void No_face_shows_its_hatch_mirrored_seen_from_outside()
    {
        // u runs right and v down as the face is seen from outside, so u × v points into the face on every face: a
        // forward hatch stays forward on port and starboard alike, and the second round stays apart from the third.
        var mesh = ShipScene3D.Cuboid(Box);
        for (var face = 0; face < 6; face++)
        {
            var (a, b, d) = (4 * face, 4 * face + 1, 4 * face + 3);
            var (uv0, uv1, uv3) = (mesh.TextureCoordinates[a], mesh.TextureCoordinates[b], mesh.TextureCoordinates[d]);
            var (e1, e2) = (mesh.Positions[b] - mesh.Positions[a], mesh.Positions[d] - mesh.Positions[a]);
            var (t1, t2) = (uv1 - uv0, uv3 - uv0);

            // Solve e = du·U + dv·V for the 3D directions of u and v on this face.
            var det = t1.X * t2.Y - t2.X * t1.Y;
            var u = (t2.Y * e1 - t1.Y * e2) / det;
            var v = (-t2.X * e1 + t1.X * e2) / det;

            Vector3D.DotProduct(Vector3D.CrossProduct(u, v), mesh.Normals[a]).Should().BeNegative($"face {face}");
        }
    }
}
