using System.Windows;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// Every look key that reaches the ship view acts in the 3D scene (T8.5, D77, D83): rendered through the overlay's own
/// wiring (<see cref="ViewPictures"/>), changing one key changes the picture, and a preset exported and imported again
/// gives the same picture. Hull A with <c>split_and_shared</c>, one crate selected.
/// </summary>
public sealed class LookSceneTests
{
    private static readonly Size Size = new(600, 450);
    private static readonly ShipOutlineCatalog Outlines = ShipOutlineLoader.Load();
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static AppSettings With(Look look) => AppSettings.Default with
    {
        Appearance = look.Appearance,
        Legend = look.Legend,
        ToolWindowTheme = look.ToolWindowTheme,
        ShipOutline = look.ShipOutline,
        HoloOpacity = look.HoloOpacity,
        HoloHullColor = look.HoloHullColor,
        HoloEdgeColor = look.HoloEdgeColor,
        ShipView = new ShipViewSettings(look.MaxZoomPxPerCell),
    };

    private static AppSettings Crates(Func<CrateAppearance, CrateAppearance> change)
    {
        var appearance = AppSettings.Default.Appearance;
        return AppSettings.Default with { Appearance = appearance with { Crates = change(appearance.Crates) } };
    }

    // Close enough that the max zoom limits the distance: 40 and 160 px per cell then give two pictures.
    private static ShipCamera Near()
    {
        var b = HullA.Bounds;
        return new ShipCamera(CameraOrientation.DefaultYaw, CameraOrientation.DefaultPitch, Distance: 0.5,
            Target: new((b.Min.X + b.Max.X) / 2.0, (b.Min.Y + b.Max.Y) / 2.0, (b.Min.Z + b.Max.Z) / 2.0));
    }

    // A box hull as in OverlayViewModelTests: no download in a test.
    private static PreparedHolo HoloFor(Ship ship, ShipOutlineMode look)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        System.Numerics.Vector3[] hull = [.. vertices.Select(v => new System.Numerics.Vector3((v.X - 0.5f) * 30, (v.Y - 0.5f) * 12, v.Z * 8))];
        return PreparedHolo.Prepare(ship, HoloReader.Read(HoloFixtures.Write(hull, triangles, HoloFixtures.CubeEdges(), 4, 8, 30)), look);
    }

    private static byte[] Picture(AppSettings settings, ShipCamera? camera = null)
    {
        var viewModel = new OverlayViewModel(HullA) { Outlines = Outlines };
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        var (_, root) = ViewPictures.Wire(viewModel, settings, Size);
        if (ShipOutlineModes.Parse(settings.ShipOutline ?? ShipOutlineModes.DefaultName) is var mode && mode.IsHologram())
        {
            viewModel.ShowHolo(HoloFor(HullA, mode));
        }

        viewModel.Selection = CrateSelection.Of(viewModel.Scene, [viewModel.Scene.Plan.Crates[0]]);
        viewModel.Camera = camera;
        ViewPictures.Layout(root, Size);
        return ViewPictures.Pixels(root);
    }

    private static readonly AppSettings Shaded = AppSettings.Default with { ShipOutline = "holo-shaded" };
    private static readonly AppSettings HiddenLine = AppSettings.Default with { ShipOutline = "holo-hiddenline" };

    // Per key: the picture before, after, and whether the camera stands near (the max zoom acts only there).
    private static readonly Dictionary<string, (AppSettings Before, AppSettings After, bool Near)> Changes = new()
    {
        ["crates.fillAlpha"] = (AppSettings.Default, Crates(c => c with { FillAlpha = 0x60 }), false),
        ["crates.edgeAlpha"] = (AppSettings.Default, Crates(c => c with { EdgeAlpha = 0x20 }), false),
        ["crates.stopPalette"] = (AppSettings.Default, Crates(c => c with { StopPalette = "tolBright" }), false),
        ["crates.rackNumberColor"] = (AppSettings.Default, Crates(c => c with { RackNumberColor = "#FFFF0000" }), false),
        ["crates.rackNumberFontSize"] = (AppSettings.Default, Crates(c => c with { RackNumberFontSize = 40 }), false),
        ["crates.halo"] = (AppSettings.Default, Crates(c => c with { Halo = HaloSettings.Off }), false),
        ["crates.selectionColor"] = (AppSettings.Default, Crates(c => c with { SelectionColor = "#FF00FF00" }), false),
        ["crates.selectionWidth"] = (AppSettings.Default, Crates(c => c with { SelectionWidth = 6 }), false),
        ["crates.selectionOutline"] = (AppSettings.Default, Crates(c => c with { SelectionOutline = HaloSettings.Off }), false),
        ["shipOutline"] = (AppSettings.Default, AppSettings.Default with { ShipOutline = "none" }, false),
        ["holoOpacity"] = (Shaded, Shaded with { HoloOpacity = 0.9 }, false),
        ["holoHullColor"] = (Shaded, Shaded with { HoloHullColor = "#FFFF0000" }, false),
        ["holoEdgeColor"] = (HiddenLine, HiddenLine with { HoloEdgeColor = "#FFFF0000" }, false),
        ["shipView.maxZoomPxPerCell"] = (AppSettings.Default, AppSettings.Default with { ShipView = new ShipViewSettings(40) }, true),
    };

    public static TheoryData<string> Keys() => [.. Changes.Keys];

    [Theory]
    [MemberData(nameof(Keys))]
    public void Each_ship_view_key_changes_the_picture(string key)
    {
        Sta.Run(() =>
        {
            var (before, after, near) = Changes[key];
            var camera = near ? Near() : null;

            ViewPictures.Difference(Picture(before, camera), Picture(after, camera)).Should().BePositive(key);
        });
    }

    // The outline's line takes the rack numbers' colour and its halo the crates' halo (D62): with the outline these keys
    // change more of the picture than without it.
    [Theory]
    [InlineData("crates.rackNumberColor")]
    [InlineData("crates.halo")]
    public void The_outline_takes_the_rack_numbers_colour_and_the_halo(string key)
    {
        Sta.Run(() =>
        {
            var (before, after, _) = Changes[key];
            double Changed(string outline) => ViewPictures.Difference(
                Picture(before with { ShipOutline = outline }), Picture(after with { ShipOutline = outline }));

            Changed("outline").Should().BeGreaterThan(Changed("none"), key);
        });
    }

    [Fact]
    public void Every_preset_shows_its_own_picture()
    {
        Sta.Run(() =>
        {
            var pictures = LookPresets.All.Select(p => Picture(With(p.Look))).ToArray();

            for (var i = 1; i < pictures.Length; i++)
            {
                ViewPictures.Difference(pictures[0], pictures[i]).Should().BePositive(LookPresets.All[i].Name);
            }
        });
    }

    // WPF renders in software in the same process: bit for bit. Should text anti-aliasing ever make it flicker, a share
    // of differing pixels with its reason goes here, not the test (Martin, T8.5 E2).
    [Fact]
    public void A_preset_exported_and_imported_shows_the_same_picture()
    {
        Sta.Run(() =>
        {
            foreach (var preset in LookPresets.All)
            {
                using MemoryStream file = new();
                LookFile.Write(file, preset.Look);
                file.Position = 0;
                var (imported, problems) = LookFile.Read(file, Look.Default);

                problems.Should().BeEmpty(preset.Name);
                imported.Should().Be(preset.Look, preset.Name);
                ViewPictures.Difference(Picture(With(preset.Look)), Picture(With(imported))).Should().Be(0, preset.Name);
            }
        });
    }
}
