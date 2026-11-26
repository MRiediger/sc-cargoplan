using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CargoPlan.App.Routing;
using CargoPlan.App.Settings;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// What the map costs (T13.7, D128; E of the approval: by hand, the numbers into TASKS.md): building it from the pin's
/// hauling data (the view model's graph and the view's layout and labels; budget Stanton 50 ms, Pyro 20 ms) and one wheel
/// notch (a projection only, K4; target under 5 ms). No time is asserted; frames over the game are Martin's gate.
/// <c>dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~RouteMapTimingTests --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class RouteMapTimingTests(ITestOutputHelper output)
{
    private const int Rounds = 15;

    private static readonly Size Wide = new(1100, 700);

    [Fact]
    public void Build_and_wheel_notch() => Sta.Run(() =>
    {
        var load = HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder));
        output.WriteLine("| Map | first build ms | build ms (median) | notch ms (median) | notch ms (max) |");
        output.WriteLine("|---|---|---|---|---|");
        foreach (var system in new[] { StarSystem.Stanton, StarSystem.Pyro })
        {
            List<double> builds = [];
            List<double> notches = [];
            for (var round = 0; round < Rounds; round++)
            {
                var watch = Stopwatch.StartNew();
                var model = new RoutesViewModel(load, new ShipDataVersion(2026, 11, 2), RoutesSettings.Default with { Rank = "senior" }, null);
                if (system == StarSystem.Pyro)
                {
                    model.NextCommand.Execute("system");
                }

                // The view model's part alone: the matrix it has, the map it builds when the view opens.
                watch.Restart();
                model.PreviousCommand.Execute("view");
                var view = new RouteMapView();
                view.Resources["LegendTextStyle"] = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.FontSizeProperty, 12.0) } };
                view.Scene = model.Map;
                view.Measure(Wide);
                view.Arrange(new Rect(Wide));
                view.UpdateLayout();
                builds.Add(watch.Elapsed.TotalMilliseconds);

                for (var notch = 0; notch < 10; notch++)
                {
                    watch.Restart();
                    view.ZoomAt(new Point(500, 300), notch < 5 ? 120 : -120);
                    view.UpdateLayout();
                    notches.Add(watch.Elapsed.TotalMilliseconds);
                }
            }

            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {system} | {builds[0]:0.0} | {Median(builds.Skip(1)):0.0} | {Median(notches):0.00} | {notches.Max():0.00} |"));
        }

        output.WriteLine("Build = switching the view to the map (matrix → graph → scene) plus the view's layout and labels; it runs on the UI thread.");
    });

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
