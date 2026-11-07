using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// The picture of every catalog entry, bit for bit, against a run of another build (F7, D90): each look and size at the
/// default camera without cargo, two looks with cargo and a crate selected (F3, D91), and the gate's ships at a saved camera.
/// Run by hand, see CLAUDE.md, Befehle. It uses only
/// what <c>main</c> has, so the file can be copied into a worktree of an older commit for the baseline.
/// Set <c>CARGOPLAN_PICTURES_OUT</c> (a new folder outside the repo) and <c>CARGOPLAN_HOLO_FOLDER</c> (the
/// <c>&lt;slug&gt;.holo</c> files); <c>CARGOPLAN_PICTURES_BASELINE</c> (an earlier run's folder) compares;
/// <c>CARGOPLAN_PICTURES_EXPECTED</c> (ids, comma-separated) names the ships allowed to differ.
/// </summary>
[Trait("Category", "Slow")]
public sealed class ShipPictureTests(ITestOutputHelper output)
{
    private const string PicturesFile = "pictures.csv";
    private const string ValuesFile = "values.csv";
    private const string ReportFile = "report.txt";

    private static readonly string[] Looks = ["outline", "none", "holo-shaded", "holo-flat", "holo-hiddenline"];
    private static readonly Size[] Sizes = [new(400, 302), new(800, 600)];
    private static readonly string[] GateShips = ["hull_a", "aurora_mk_ii_ts_module", "utv", "hull_c", "carrack"];
    private static readonly string[] SavedCameraLooks = ["outline", "holo-shaded"];

    [Fact]
    public void Every_ship_looks_as_in_the_baseline()
    {
        var folder = Required("CARGOPLAN_PICTURES_OUT");
        var holograms = Required("CARGOPLAN_HOLO_FOLDER");
        var baseline = Environment.GetEnvironmentVariable("CARGOPLAN_PICTURES_BASELINE");
        HashSet<string> expected = [.. (Environment.GetEnvironmentVariable("CARGOPLAN_PICTURES_EXPECTED") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)];
        Directory.CreateDirectory(folder);

        List<string> pictures = [];
        List<string> values = [];
        Sta.Run(() => Render(folder, holograms, keepEveryPng: baseline is null, baseline, pictures, values));
        File.WriteAllLines(Path.Combine(folder, PicturesFile), pictures);
        File.WriteAllLines(Path.Combine(folder, ValuesFile), values);

        if (baseline is null)
        {
            output.WriteLine($"{pictures.Count} pictures written to {folder}; no baseline, nothing compared.");
            return;
        }

        var report = Compare(baseline, pictures, values, expected);
        File.WriteAllLines(Path.Combine(folder, ReportFile), report.Lines);
        report.Lines.ForEach(output.WriteLine);
        report.Unexpected.Should().BeEmpty("every deviation from the baseline is a stop; see {0}", Path.Combine(folder, ReportFile));
    }

    private static void Render(string folder, string holograms, bool keepEveryPng, string? baseline, List<string> pictures, List<string> values)
    {
        var known = baseline is null ? null : Read(Path.Combine(baseline, PicturesFile));
        var outlines = ShipOutlineLoader.Load();
        foreach (var ship in ShipLoader.LoadAll().Ships.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            // A ship without a hologram (no Fleetyards slug, the Cyclone, D92) has the looks without one.
            var holoFile = Path.Combine(holograms, $"{ship.FleetyardsSlug}.holo");
            var mesh = ship.FleetyardsSlug is not null && File.Exists(holoFile) ? HoloReader.Read(File.ReadAllBytes(holoFile)) : null;
            values.Add(mesh is null ? $"{ship.Id};no hologram;-" : Values(ship, mesh, outlines));
            foreach (var look in Looks.Where(l => mesh is not null || !ShipOutlineModes.Parse(l).IsHologram()))
            {
                foreach (var size in Sizes)
                {
                    Take(ship, mesh, outlines, look, size, camera: null, "fit");
                }
            }

            // The contact sheet's second view (D92): from above, as steep as the camera goes, hull and racks in the picture.
            Take(ship, mesh, outlines, mesh is null ? "outline" : "holo-shaded", Sizes[1], Above(ship, mesh), "above");

            var cameraLooks = SavedCameraLooks.Where(l => mesh is not null || !ShipOutlineModes.Parse(l).IsHologram()).ToArray();
            if (GateShips.Contains(ship.Id))
            {
                foreach (var look in cameraLooks)
                {
                    Take(ship, mesh, outlines, look, Sizes[1], Saved(ship), "saved");
                }
            }

            foreach (var look in cameraLooks)
            {
                Take(ship, mesh, outlines, look, Sizes[1], camera: null, "cargo", LoadPlanner.Replan(TestPlans.SplitAndShared(), ship));
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        void Take(Ship ship, HoloMesh? mesh, ShipOutlineCatalog outlineData, string look, Size size, ShipCamera? camera, string cameraName, Core.Plans.SavedPlan? cargo = null)
        {
            var root = Wired(ship, mesh, outlineData, look, size, camera, cargo);
            var key = $"{ship.Id};{look};{size.Width}x{size.Height};{cameraName}";
            var hash = Convert.ToHexString(SHA256.HashData(ViewPictures.Pixels(root)));
            pictures.Add($"{key};{hash}");
            if (keepEveryPng || known?.GetValueOrDefault(key) != hash)
            {
                Directory.CreateDirectory(Path.Combine(folder, "png"));
                ViewPictures.SavePng(root, Path.Combine(folder, "png", $"{key.Replace(';', '.')}.png"));
            }
        }
    }

    private static FrameworkElement Wired(Ship ship, HoloMesh? mesh, ShipOutlineCatalog outlines, string look, Size size, ShipCamera? camera, Core.Plans.SavedPlan? cargo)
    {
        var viewModel = new OverlayViewModel(ship) { Outlines = outlines };
        if (cargo is not null)
        {
            viewModel.ShowPlan(cargo);
        }

        var (_, root) = ViewPictures.Wire(viewModel, AppSettings.Default with { ShipOutline = look }, size);
        if (mesh is not null && ShipOutlineModes.Parse(look) is var mode && mode.IsHologram())
        {
            viewModel.ShowHolo(Holo.PreparedHolo.Prepare(ship, mesh, mode));
        }

        // Last: a new scene forgets the selection. One crate shows its own frame and its mission's.
        if (cargo is not null && viewModel.Scene is { } scene)
        {
            viewModel.Selection = CrateSelection.Of(scene, scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Crate is not null).Select(b => b.Crate!).Take(1));
        }

        viewModel.Camera = camera;
        ViewPictures.Layout(root, size);
        return root;
    }

    // A placed view that looks at the racks' middle from a fixed side: what a saved camera holds (K1.2, D83).
    private static ShipCamera Saved(Ship ship)
    {
        var (min, max) = (ship.Bounds.Min, ship.Bounds.Max);
        var diagonal = Math.Sqrt(Math.Pow(max.X - min.X, 2) + Math.Pow(max.Y - min.Y, 2) + Math.Pow(max.Z - min.Z, 2));
        return new ShipCamera(35, 20, Distance: 3 * diagonal, Target: new((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0));
    }

    // From above (the steepest pitch the camera takes) at the racks' middle, far enough for the larger of racks and hull.
    private static ShipCamera Above(Ship ship, HoloMesh? mesh)
    {
        var (min, max) = (ship.Bounds.Min, ship.Bounds.Max);
        var diagonal = Math.Sqrt(Math.Pow(max.X - min.X, 2) + Math.Pow(max.Y - min.Y, 2) + Math.Pow(max.Z - min.Z, 2));
        var hull = mesh is null ? 0 : mesh.LengthM / 1.25;
        return new ShipCamera(0, CameraOrientation.MaxPitch, Distance: 1.6 * Math.Max(diagonal, hull),
            Target: new((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0));
    }

    // The placement's numbers, exact: a deviation here names the cause of a picture's.
    private static string Values(Ship ship, HoloMesh mesh, ShipOutlineCatalog outlines)
    {
        var holo = Holo.PreparedHolo.Prepare(ship, mesh, ShipOutlineMode.HoloFlat).Placement;
        StringBuilder points = new();
        if (outlines.For(ship) is { } outline)
        {
            foreach (var p in OutlinePlacement.Place(outline, ship))
            {
                points.Append(Exact(p.X)).Append(' ').Append(Exact(p.Y)).Append(' ').Append(Exact(p.Z)).Append(' ');
            }
        }

        var outlineHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(points.ToString())));
        return $"{ship.Id};{Exact(holo.Offset.X)} {Exact(holo.Offset.Y)} {Exact(holo.Offset.Z)};{outlineHash}";
    }

    private static string Exact(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static Report Compare(string baseline, List<string> pictures, List<string> values, HashSet<string> expected)
    {
        var before = Read(Path.Combine(baseline, PicturesFile));
        var after = pictures.Select(Split).ToDictionary(p => p.Key, p => p.Value);
        var valuesBefore = Read(Path.Combine(baseline, ValuesFile));
        var valuesAfter = values.Select(Split).ToDictionary(p => p.Key, p => p.Value);

        string[] differing = [.. after.Where(p => before.GetValueOrDefault(p.Key) != p.Value).Select(p => p.Key)];
        string[] missing = [.. before.Keys.Except(after.Keys)];
        string[] numbers = [.. valuesAfter.Where(v => valuesBefore.GetValueOrDefault(v.Key) != v.Value).Select(v => v.Key)];

        List<string> lines = ["| Look | Picture set | Identical | Different |", "|---|---|---|---|"];
        foreach (var group in after.Keys.GroupBy(k => string.Join(';', k.Split(';')[1..])).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var different = group.Count(differing.Contains);
            lines.Add($"| {group.Key.Split(';')[0]} | {group.Key[(group.Key.IndexOf(';', StringComparison.Ordinal) + 1)..].Replace(';', ' ')} | {group.Count() - different} | {different} |");
        }

        lines.Add($"**Sum: {after.Count - differing.Length} identical, {differing.Length} different, {missing.Length} missing; placement numbers differ on {numbers.Length} of {valuesAfter.Count} entries.**");
        lines.AddRange(differing.Select(k => $"different: {k}"));
        lines.AddRange(missing.Select(k => $"missing: {k}"));
        lines.AddRange(numbers.Select(k => $"numbers: {k}: {valuesBefore.GetValueOrDefault(k)} -> {valuesAfter[k]}"));

        string[] unexpected = [.. differing.Concat(missing).Concat(numbers).Where(k => !expected.Contains(k.Split(';')[0]))];
        return new Report(lines, unexpected);
    }

    private static Dictionary<string, string> Read(string path) => File.ReadAllLines(path).Select(Split).ToDictionary(p => p.Key, p => p.Value);

    private static (string Key, string Value) Split(string line)
    {
        // The key is everything before the last field for a picture, the ship id and its numbers for a value line.
        var last = line.LastIndexOf(';');
        return line.Count(c => c == ';') == 2 ? (line[..line.IndexOf(';', StringComparison.Ordinal)], line[(line.IndexOf(';', StringComparison.Ordinal) + 1)..]) : (line[..last], line[(last + 1)..]);
    }

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{variable} is not set; this test runs by hand, see CLAUDE.md, Befehle.");

    private sealed record Report(List<string> Lines, string[] Unexpected);
}
