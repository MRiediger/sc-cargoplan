using System.Diagnostics;
using System.Globalization;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// What a ContractTracker read costs in the overlay (D43): unify, sync and showing the plan, on Martin's Hurston plan of
/// 25 Oct (Hull A, four captures). No time is asserted; Martin's bound is 16 ms as for an undo (K2.4). The table goes to
/// the test output:
/// <c>dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class SyncTimingTests(ITestOutputHelper output)
{
    private static readonly long[] AllIds = [1, 2, 3, 4, 37, 38];

    /// <summary>Captures 2–3 and 4 gone, 1 and 37–38 left.</summary>
    private static readonly long[] TwoGone = [1, 37, 38];

    [Fact]
    public void Sync_time()
    {
        output.WriteLine("| Case | ms (median) |");
        Row("watcher read, nothing new", overlay => overlay.ApplyContractTracker(new([], null, Present(AllIds))));
        Row("Sync button, nothing new", overlay => overlay.ApplyContractTracker(new([], null, Present(AllIds)), manual: true));
        Row("two contracts gone", overlay => overlay.ApplyContractTracker(new([], null, Present(TwoGone))));
        Row("two contracts gone, all delivered", overlay => overlay.ApplyContractTracker(new([], null, Present(TwoGone))), DeliverAll);
        Row("two contracts gone, remove", overlay => overlay.ApplyContractTracker(new([], null, Present(TwoGone))), Policy(ContractGonePolicy.Remove));
        Row("two contracts gone, all delivered, remove", overlay => overlay.ApplyContractTracker(new([], null, Present(TwoGone))),
            overlay => { Policy(ContractGonePolicy.Remove)(overlay); DeliverAll(overlay); });
        Row("watcher read, two gone before", overlay => overlay.ApplyContractTracker(new([], null, Present(TwoGone))), Gone);
        Row("two contracts back", overlay => overlay.ApplyContractTracker(new([], null, Present(AllIds))), Gone);
    }

    private static Action<OverlayViewModel> Policy(ContractGonePolicy onGone) => overlay => overlay.OnContractGone = onGone;

    /// <summary>Two contracts marked gone by an earlier read.</summary>
    private static void Gone(OverlayViewModel overlay) => overlay.ApplyContractTracker(new([], null, Present(TwoGone)));

    private static void DeliverAll(OverlayViewModel overlay)
    {
        foreach (var stop in overlay.Plan!.Route.ToList())
        {
            overlay.ToggleDeliveredCommand.Execute(stop);
        }
    }

    private void Row(string name, Action<OverlayViewModel> read, Action<OverlayViewModel>? before = null)
    {
        var hullA = ShipLoader.Load("hull_a");
        var saved = TestPlans.HurstonPickups();
        List<double> runs = [];
        for (var i = 0; i < 6; i++)
        {
            var overlay = new OverlayViewModel(hullA);
            overlay.ShowPlan(saved);
            before?.Invoke(overlay);
            var watch = Stopwatch.StartNew();
            read(overlay);
            runs.Add(watch.Elapsed.TotalMilliseconds);
        }

        // The first round warms up.
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| {name} | {runs.Skip(1).Order().ElementAt(2):F2} |"));
    }

    private static HashSet<long> Present(params long[] ids) => [.. ids];
}
