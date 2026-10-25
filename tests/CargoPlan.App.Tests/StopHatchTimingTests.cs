using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// What the hatch costs (K4.2): the layered window renders in software (D2), and a tiled brush is the one new kind of
/// cost. The C2 with 16 stops, rendered with Okabe-Ito (stops 9–16 hatched) and with a test palette of 16 plain colours,
/// the same scene. No time is asserted; the table goes to the test output:
/// <c>dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class StopHatchTimingTests(ITestOutputHelper output)
{
    private const int Rounds = 30;

    [Fact]
    public void Render_time_with_and_without_hatch()
    {
        Exception? failure = null;
        List<string> lines = [];
        Thread thread = new(() =>
        {
            try
            {
                lines.AddRange(Measure());
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.Should().BeNull();
        lines.ForEach(output.WriteLine);
    }

    private static IEnumerable<string> Measure()
    {
        var c2 = ShipLoader.Load("c2_hercules");
        MissionStop[] stops = [.. Enumerable.Range(1, 16).Select(i => new MissionStop($"Stop {i}", i, [new MissionItem("Gold", 8, CrateSize.Two)]))];
        var cap = CrateCap.Of(c2);
        var saved = SavedPlan.FromStops(stops, LoadPlanner.Plan(c2, stops.ToStops(cap)), cap);
        var scene = ShipViewBuilder.Build(saved, c2, LoadPlanner.Validate(saved.Plan, c2));

        // 16 plain colours: Okabe-Ito and Tol Bright's six plus two of Tol Muted, so nothing is hatched.
        StopPalette plain = new(StopPaletteName.OkabeIto, "16 plain",
            [.. StopPalettes.OkabeIto.Colors, .. StopPalettes.TolBright.Colors, .. StopPalettes.TolMuted.Colors.Take(2)]);

        yield return string.Create(CultureInfo.InvariantCulture, $"C2, {scene.StopCount} stops, {saved.Plan.Crates.Count} crates, 800 × 600, median of {Rounds}");
        yield return "| Palette | hatched stops | render ms (median) |";
        foreach (var (palette, hatched) in new[] { (plain, 0), (StopPalettes.OkabeIto, 8) })
        {
            ShipViewControl view = new() { Width = 800, Height = 600, Scene = scene, StopPalette = palette, CrateFillAlpha = 0xD0 };
            view.Resources["HitTestBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            view.CrateEdgeBrush = new SolidColorBrush(MissionPalette.RackEdge with { A = 0xC0 });
            Border border = new() { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)), Child = view };
            border.Measure(new Size(800, 600));
            border.Arrange(new Rect(0, 0, 800, 600));
            border.UpdateLayout();

            List<double> times = [];
            for (var i = 0; i <= Rounds; i++)
            {
                RenderTargetBitmap bitmap = new(800, 600, 96, 96, PixelFormats.Pbgra32);
                var watch = Stopwatch.StartNew();
                bitmap.Render(border);
                watch.Stop();
                if (i > 0)
                {
                    times.Add(watch.Elapsed.TotalMilliseconds);
                }
            }

            times.Sort();
            yield return string.Create(CultureInfo.InvariantCulture, $"| {palette.DisplayName} | {hatched} | {times[times.Count / 2]:F2} |");
        }
    }
}
