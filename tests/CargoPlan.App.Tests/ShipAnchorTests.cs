using System.Numerics;
using System.Text.Json;
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
/// D92) with Rack 1 one cell further to the bow, and the calculation of the values in the ship files.
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

    // --- The values in the files ---

    private static Ship Unanchored() => new(
        "plain", "Plain", 8, [new Rack("rack-0", 0, 8, new Dimensions(2, 2, 2), null, new ShipPoint(-3, 1, 2))], HoloOffset: new ShipPoint(0, 0, 1));

    private static ShipOutline Outline(double heightM = 10) => new("box", [new(10, 5), new(-10, 5), new(-10, -5), new(10, -5)]) { HeightM = heightM };

    private static Ship With(Ship ship, ShipAnchors anchors) => ship with
    {
        HoloOffset = null,
        HoloOrigin = anchors.HoloOrigin,
        OutlineOrigin = anchors.OutlineOrigin,
        OutlineScale = anchors.OutlineScale,
    };

    [Fact]
    public void Frozen_values_draw_what_the_rule_on_the_racks_draws_bit_for_bit()
    {
        var (ship, mesh, outline) = (Unanchored(), Hull(23.42f, 7.3f, 5.6f, shiftX: 0.0137f), Outline());

        var frozen = With(ship, ShipAnchorValues.Freeze(ship, mesh, outline));

        HoloPlacement.Place(mesh, frozen).Should().Be(HoloPlacement.Place(mesh, ship));
        OutlinePlacement.Place(outline, frozen).Should().Equal(OutlinePlacement.Place(outline, ship));
    }

    [Fact]
    public void Frozen_values_keep_a_scale_above_true_size()
    {
        // Racks of 16 × 2 cells against a hull of 16 × 8: 1.2 × 16 / 16 = 1.2.
        Ship wide = new("wide", "Wide", 64, [new Rack("rack-0", 0, 64, new Dimensions(16, 2, 2), null, new ShipPoint(0, 0, 0))]);

        var frozen = ShipAnchorValues.Freeze(wide, Hull(), Outline());

        frozen.OutlineScale.Should().BeApproximately(1.2, 1e-12);
        OutlinePlacement.Place(Outline(), With(wide, frozen)).Should().Equal(OutlinePlacement.Place(Outline(), wide));
    }

    [Fact]
    public void Frozen_values_leave_out_a_scale_at_true_size()
    {
        ShipAnchorValues.Freeze(Unanchored(), Hull(), Outline()).OutlineScale.Should().BeNull();
    }

    // K2: a new holo version may move its box against its origin; the hull stays, and with it any corrected rack.
    [Fact]
    public void Carrying_a_hologram_over_keeps_the_middle_of_its_box()
    {
        ShipCoordinates origin = new(5.25, -1.5, -2);
        var (previous, next) = (Hull(shiftX: 0.5f), Hull(height: 12, shiftX: -0.25f));

        var carried = ShipAnchorValues.Carry(origin, previous.Bounds, next.Bounds);

        Point3D Middle(HoloPlacement p) => new((p.Min.X + p.Max.X) / 2, (p.Min.Y + p.Max.Y) / 2, (p.Min.Z + p.Max.Z) / 2);
        var before = Middle(HoloPlacement.Place(previous, Unanchored() with { HoloOffset = null, HoloOrigin = origin }));
        var after = Middle(HoloPlacement.Place(next, Unanchored() with { HoloOffset = null, HoloOrigin = carried }));
        (after - before).Length.Should().BeLessThan(1e-5);
    }

    [Fact]
    public void Writing_replaces_the_fields_and_keeps_the_rest_of_the_file()
    {
        const string File = "{\n  \"schemaVersion\": 5,\n  \"id\": \"x\",\n  \"holoOffset\": [0, 0, 1],\n  \"totalScu\": 2,\n  \"racks\": []\n}\n";
        ShipAnchors anchors = new(new ShipCoordinates(1.9994354248046875, -0.5, 0.26019287109375), new ShipCoordinates(2, -0.5, 0), 1.12);

        var written = ShipAnchorValues.WriteInto(File, anchors, "totalScu");

        written.Should().Be("{\n  \"schemaVersion\": 5,\n  \"id\": \"x\",\n  \"holoOrigin\": [1.9994354248046875, -0.5, 0.26019287109375],\n  \"outlineOrigin\": [2, -0.5, 0],\n  \"outlineScale\": 1.12,\n  \"totalScu\": 2,\n  \"racks\": []\n}\n");
        ShipAnchorValues.WriteInto(written, anchors with { OutlineScale = null }, "totalScu").Should().NotContain("outlineScale");
    }

    // A Windows checkout has CRLF files (.gitattributes text=auto).
    [Fact]
    public void Writing_keeps_the_files_line_ends()
    {
        const string File = "{\r\n  \"holoOrigin\": [0, 0, 0],\r\n  \"totalScu\": 2\r\n}\r\n";
        ShipAnchors anchors = new(new ShipCoordinates(1, 2, 3), new ShipCoordinates(4, 5, 6), null);

        ShipAnchorValues.WriteInto(File, anchors, "totalScu").Should().Be("{\r\n  \"holoOrigin\": [1, 2, 3],\r\n  \"outlineOrigin\": [4, 5, 6],\r\n  \"totalScu\": 2\r\n}\r\n");
    }
}

/// <summary>
/// Writes the anchors into the ship files by hand (D90, ship-format.md): <c>CARGOPLAN_SHIPS_WRITE</c> is the repository's
/// <c>ships/</c>, <c>CARGOPLAN_HOLO_FOLDER</c> the holograms. Ships without values are frozen; with
/// <c>CARGOPLAN_HOLO_PREVIOUS</c> (the folder of the holo version the files were made for) every ship is carried over.
/// </summary>
[Trait("Category", "Slow")]
public sealed class ShipAnchorValueTests
{
    [Fact]
    public void Write_the_anchors_into_the_ship_files()
    {
        var ships = Required("CARGOPLAN_SHIPS_WRITE");
        var holograms = Required("CARGOPLAN_HOLO_FOLDER");
        var previous = Environment.GetEnvironmentVariable("CARGOPLAN_HOLO_PREVIOUS");
        var catalog = ShipLoader.LoadAll(ships);
        var outlines = ShipOutlineLoader.Load();
        HoloMesh Mesh(string folder, Ship ship) => HoloReader.Read(File.ReadAllBytes(Path.Combine(folder, $"{ship.ModelKey}.holo")));

        ShipAnchors Values(Ship ship)
        {
            if (previous is null)
            {
                return ShipAnchorValues.Freeze(ship, Mesh(holograms, ship), outlines.For(ship)!);
            }

            var carried = ShipAnchorValues.Carry(ship.HoloOrigin!, Mesh(previous, ship).Bounds, Mesh(holograms, ship).Bounds);
            return new ShipAnchors(carried, ship.OutlineOrigin!, ship.OutlineScale);
        }

        Dictionary<string, ShipAnchors> values = catalog.Ships.ToDictionary(s => s.Id, Values);
        foreach (var path in Directory.GetFiles(ships, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            File.WriteAllText(path, ShipAnchorValues.WriteInto(File.ReadAllText(path), values[id], "totalScu"));
        }

        // A module says its own places only where they differ from its ship's, as the TS module's do.
        foreach (var path in Directory.GetFiles(Path.Combine(ships, "modules"), "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var moduleId = document.RootElement.GetProperty("id").GetString();
            foreach (var ship in document.RootElement.GetProperty("ships").EnumerateArray().Select(e => e.GetString()!))
            {
                if (values[$"{ship}_{moduleId}"] != values[ship])
                {
                    File.WriteAllText(path, ShipAnchorValues.WriteInto(File.ReadAllText(path), values[$"{ship}_{moduleId}"], "racks"));
                }
            }
        }
    }

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{variable} is not set; this test runs by hand, see CLAUDE.md, Befehle.");
}
