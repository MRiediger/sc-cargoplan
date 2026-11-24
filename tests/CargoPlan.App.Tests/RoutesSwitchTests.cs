using System.IO;
using System.Security.Cryptography;
using System.Windows;
using CargoPlan.App.Plans;
using CargoPlan.App.Routing;
using CargoPlan.App.Scanning;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The Routes switch (T13.1, U3, D118 addendum): with Routes selected an offer stays out of the plan; every readable scan
/// is an observation in routes.json, whatever is selected. The six cells of the D118 table, through the scan runner.
/// </summary>
public sealed class RoutesSwitchTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private const string Title = "Junior Rank - Small Cargo Haul";

    private static readonly ObservationKey JuniorSmall = new(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Small, Direct: false, Reward: 50_250, ["Stims"]);

    private const string Observed = "Observed (Plan tab): Covalex Junior Small · 50,250 aUEC – cargo unchanged";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private readonly OverlayViewModel _overlay = new(HullA);

    private string RoutesPath => Path.Combine(_folder, "routes.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    public enum View
    {
        Offer,
        Accepted,
        Unknown,
    }

    /// <summary>A contract in our OCR's reading order with its title; the view from its buttons, none for unknown.</summary>
    private static string Raw(View view, string title = Title, int scu = 8) =>
        $"{title}\n{(view == View.Accepted ? "ABANDON\n" : "")}DETAILS\nReward 50,250\n"
        + view switch { View.Offer => "Contract Availability 2h 1m\n", View.Accepted => "Contract Deadline N/A\n", _ => "" }
        + $"PRIMARY OBJECTIVES\nDeliver 0/{scu} SCU of Stims to Riker.\nCollect Stims from Everus Harbor."
        + view switch { View.Offer => "\nACCEPT OFFER", View.Accepted => "\nSHARE UNTRACK", _ => "" };

    private static ScanAttempt Read(string raw) => new(ScanAttemptOutcome.Read, raw, TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero);

    private ScanRunner Runner(RoutesBook routes, params string[] raws)
    {
        var queue = new Queue<string>(raws);
        return new(() => Task.FromResult(Read(queue.Dequeue())), _overlay, new ScanLog(Path.Combine(_folder, "scans.log")),
            action => { action(); return Task.CompletedTask; }, _ => Task.CompletedTask, routes: routes);
    }

    private async Task<RoutesBook> Scan(bool routesSelected, params string[] raws)
    {
        _overlay.IsRoutesTabSelected = routesSelected;
        var routes = RoutesBook.Load(RoutesPath);
        var runner = Runner(routes, raws);
        foreach (var _ in raws)
        {
            await runner.RunAsync();
        }

        return routes;
    }

    [Fact]
    public async Task A_recorded_scan_tells_its_listener_all_observations_and_its_entry()
    {
        // T13.3: the Plan tab's matrix follows the scans through this event.
        var routes = RoutesBook.Load(RoutesPath);
        List<(Observations All, Observation Entry)> heard = [];
        routes.Recorded += (all, entry) => heard.Add((all, entry));

        await Runner(routes, Raw(View.Offer)).RunAsync();

        heard.Should().ContainSingle();
        heard[0].Entry.Key.Should().Be(JuniorSmall);
        heard[0].All.Entries.Should().ContainSingle();
    }

    // --- The six cells ---

    [Fact]
    public async Task Offer_without_routes_goes_into_the_plan_as_today_and_is_observed()
    {
        var routes = await Scan(routesSelected: false, Raw(View.Offer));

        _overlay.Missions.Should().ContainSingle().Which.Contract!.Scan.QuantitiesEstimated.Should().BeTrue();
        _overlay.Notices.Should().Contain(n => n.StartsWith("Scanned #1", StringComparison.Ordinal));
        routes.Observations.Entries.Should().ContainSingle().Which.Key.Should().Be(JuniorSmall);
        RoutesBook.Load(RoutesPath).Observations.Entries.Should().ContainSingle("the observation is on disk");
    }

    [Fact]
    public async Task Offer_with_routes_leaves_the_plan_alone_and_is_observed()
    {
        var routes = await Scan(routesSelected: true, Raw(View.Offer));

        _overlay.Saved.Should().BeNull();
        _overlay.LastScanNo.Should().Be(0, "no scan number is given for a contract not in the plan");
        _overlay.UndoCommand.CanExecute(null).Should().BeFalse("nothing happened to undo");
        _overlay.Notices.Should().Equal(Observed);
        routes.Observations.Entries.Should().ContainSingle().Which.Key.Should().Be(JuniorSmall);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_goes_into_the_plan_as_today_and_is_observed(bool routesSelected)
    {
        var routes = await Scan(routesSelected, Raw(View.Accepted));

        _overlay.Missions.Should().ContainSingle().Which.Contract!.Scan.QuantitiesEstimated.Should().BeFalse();
        routes.Observations.Entries.Should().ContainSingle().Which.Key.Should().Be(JuniorSmall);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_view_goes_into_the_plan_as_today_and_is_not_observed(bool routesSelected)
    {
        var routes = await Scan(routesSelected, Raw(View.Unknown));

        _overlay.Missions.Should().ContainSingle().Which.Contract!.Scan.QuantitiesEstimated.Should().BeTrue("unknown counts as estimated (D102)");
        routes.Observations.Entries.Should().BeEmpty();
        File.Exists(RoutesPath).Should().BeFalse("nothing observed, nothing written");
    }

    // --- The tab strip (T13.2, D120) drives the same switch ---

    [Fact]
    public async Task The_Plan_tab_keeps_an_offer_out_and_the_Cargo_tab_lets_the_next_one_in()
    {
        var routes = RoutesBook.Load(RoutesPath);
        var runner = Runner(routes, Raw(View.Offer), Raw(View.Offer, scu: 9));

        _overlay.ShowPlanCommand.Execute(null);
        await runner.RunAsync();
        _overlay.IsRoutesTabSelected.Should().BeTrue();
        _overlay.Missions.Should().BeEmpty();
        _overlay.Notices.Should().Equal(Observed);

        _overlay.ShowCargoCommand.Execute(null);
        await runner.RunAsync();
        _overlay.IsRoutesTabSelected.Should().BeFalse();
        _overlay.Missions.Should().ContainSingle();
        routes.Observations.Entries.Should().ContainSingle().Which.Count.Should().Be(2);
    }

    // --- Around the cells ---

    [Fact]
    public async Task Two_offers_of_one_variant_are_one_observation_counted_twice()
    {
        var routes = await Scan(routesSelected: true, Raw(View.Offer, scu: 8), Raw(View.Offer, scu: 12));

        var entry = routes.Observations.Entries.Should().ContainSingle().Subject;
        entry.Count.Should().Be(2);
        entry.Scu.Should().Equal(new ScuBand("Stims", 8, 12));
        _overlay.Saved.Should().BeNull();
    }

    [Theory]
    [InlineData("", "", "title unreadable")]
    [InlineData("Junior Rank - Huge Cargo Haul", "", "title unreadable")]
    // E1 of T13.3a: another contractor's contract (Martin's scans.log, 24 Nov) isn't a title OCR broke.
    [InlineData("Cargo Hauling Opportunity with Ling Hauling", "Contracted By Ling Family Hauling\n", "no hauling template form")]
    public async Task Offer_with_routes_and_no_title_stays_out_of_the_plan_and_says_why(string title, string contractor, string why)
    {
        var routes = await Scan(routesSelected: true, Raw(View.Offer, title).Replace("PRIMARY OBJECTIVES", contractor + "PRIMARY OBJECTIVES", StringComparison.Ordinal));

        _overlay.Saved.Should().BeNull();
        _overlay.Notices.Should().Equal($"Not observed (Plan tab): {why} – cargo unchanged");
        routes.Observations.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Offer_with_routes_leaves_an_existing_plan_as_the_same_object()
    {
        _overlay.ShowPlan(TestPlans.SplitAndShared());
        var before = _overlay.Saved;

        await Scan(routesSelected: true, Raw(View.Offer));

        _overlay.Saved.Should().BeSameAs(before);
    }

    [Fact]
    public async Task The_scan_log_names_the_time_of_the_observation()
    {
        await Scan(routesSelected: true, Raw(View.Offer));

        File.ReadAllText(Path.Combine(_folder, "scans.log")).Should().Contain("Observed | ").And.Contain(" observeMs=");
    }

    // --- plan.json byte for byte (acceptance) ---

    [Fact]
    public void Offer_with_routes_leaves_plan_json_byte_for_byte()
    {
        var planPath = Path.Combine(_folder, "plan.json");
        Sta.Run(() =>
        {
            var overlay = new OverlayViewModel(HullA);
            var saver = new PlanSaver(new Window(), overlay, planPath, false, null);
            overlay.ShowPlan(TestPlans.SplitAndShared());
            saver.Flush();
            var before = SHA256.HashData(File.ReadAllBytes(planPath));
            var written = File.GetLastWriteTimeUtc(planPath);

            overlay.IsRoutesTabSelected = true;
            overlay.ShowScanReport(overlay.ApplyScan(ApplyScanTests.Scanned(accepted: false) with { Title = new(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Small, false) }));
            saver.Flush();

            SHA256.HashData(File.ReadAllBytes(planPath)).Should().Equal(before);
            File.GetLastWriteTimeUtc(planPath).Should().Be(written, "the saver didn't write at all");
        });
    }

    // --- The notice line (A13): Scan keeps the scanner's state, Move takes the result, Stops stays ---

    [Fact]
    public async Task The_routes_notice_replaces_the_last_scan_result_and_leaves_the_other_lines()
    {
        const string Warming = "Contract scan: OCR is still warming up …";
        const string Stops = "Stops merged: Riker";
        await Scan(routesSelected: false, Raw(View.Offer, "Rookie Rank - Small Cargo Haul"));
        _overlay.SetNotice(NoticeSource.Scan, Warming);
        _overlay.SetNotice(NoticeSource.Stops, Stops);

        await Scan(routesSelected: true, Raw(View.Offer));

        _overlay.Notices.Should().Contain([Stops, Warming, Observed])
            .And.NotContain(n => n.StartsWith("Scanned #", StringComparison.Ordinal), "the result of the last scan is replaced");
    }

    // --- routes.json (D106 pattern) ---

    [Fact]
    public async Task An_unreadable_routes_json_is_kept_aside_by_the_first_observation_never_overwritten()
    {
        Directory.CreateDirectory(_folder);
        const string Broken = "{ \"schemaVersion\": 1, \"observ";
        File.WriteAllText(RoutesPath, Broken);
        var routes = RoutesBook.Load(RoutesPath);
        routes.Observations.Entries.Should().BeEmpty();
        File.ReadAllText(RoutesPath).Should().Be(Broken, "loading leaves the file as it is");

        _overlay.IsRoutesTabSelected = true;
        await Runner(routes, Raw(View.Offer)).RunAsync();

        var kept = Directory.GetFiles(_folder, "routes.json.unreadable-*").Should().ContainSingle().Subject;
        File.ReadAllText(kept).Should().Be(Broken);
        RoutesBook.Load(RoutesPath).Observations.Entries.Should().ContainSingle();
        TestLog.Text().Should().Contain($"Routes: {RoutesPath} couldn't be read; kept as {Path.GetFileName(kept)}.");
    }

    [Fact]
    public async Task A_restart_reads_the_observations_and_goes_on_counting()
    {
        await Scan(routesSelected: true, Raw(View.Offer, scu: 8));

        var restarted = await Scan(routesSelected: true, Raw(View.Offer, scu: 4));

        restarted.Observations.Entries.Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Fact]
    public async Task A_routes_json_that_cannot_be_written_costs_neither_the_scan_nor_the_plan()
    {
        Directory.CreateDirectory(RoutesPath); // a folder where the file should be: every write fails

        var routes = await Scan(routesSelected: false, Raw(View.Offer));

        _overlay.Missions.Should().ContainSingle();
        _overlay.Notices.Should().Contain(n => n.StartsWith("Scanned #1", StringComparison.Ordinal), "the notice line stays silent about routes.json");
        routes.Observations.Entries.Should().ContainSingle("kept in memory for the next try");
        TestLog.Text().Should().Contain($"Routes: {RoutesPath} not saved");
    }
}
