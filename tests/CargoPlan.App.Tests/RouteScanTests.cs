using System.IO;
using System.Security.Cryptography;
using CargoPlan.App.Plans;
using CargoPlan.App.Routing;
using CargoPlan.App.Scanning;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The route in the Cargo tab (T13.6, D125): an accepted scan fills its placeholder, an accepted contract without one is
/// an extra, the notice says so; filled is read from the plan, so an undo opens it again. Through the scan runner, as
/// the player's F3.
/// </summary>
public sealed class RouteScanTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static readonly DateTime Monday = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    private const string Title = "Junior Rank - Small Cargo Haul";

    // The Junior Small to Riker the raw text below is; the other placeholder's template is never scanned.
    private static readonly HaulTemplate Junior = new(
        "junior", "debug", HaulContractor.Covalex, Title, HaulRank.Junior, HaulSize.Small, false, HaulShape.AToB, 1, StarSystem.Stanton,
        "Stanton1", HaulMissionType.Stellar, null, 50_250, [new HaulLeg("Stims", 4, 16, CrateSize.Four)], [], ["Riker"]);

    private static readonly Positions Places = new([.. new[] { "Everus Harbor", "Riker" }.Select(n => new StarPosition(n, n, StarSystem.Stanton, "Manmade", null, 0, 0, 0, []))]);

    private static readonly Route Planned = new(
        "Everus ring", Monday, "Everus Harbor", LoopKind.LocalRing, StarSystem.Stanton, ["Riker"],
        [
            new Placeholder(1, RecipeKind.Bulk, "senior", "Senior Rank - Medium Cargo Haul", 325_250, [new HaulLeg("Waste", 41, 72, CrateSize.Sixteen)], ["Riker"]),
            new Placeholder(2, RecipeKind.Small, "junior", Title, 50_250, [new HaulLeg("Stims", 4, 16, CrateSize.Four)], ["Riker"]),
        ],
        []);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private readonly List<Route?> _saved = [];

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
    }

    [Fact]
    public async Task An_accepted_scan_fills_its_placeholder_and_the_notice_counts_it()
    {
        var overlay = Overlay(Planned);

        await Scan(overlay, Raw(View.Accepted));

        overlay.Notices.Should().Equal("Scanned #1 · From Everus Harbor · 8 SCU Stims to Riker · plan: small ✓ 1/2");
        _saved.Should().ContainSingle().Which!.Placeholders[1].FilledBy.Should().Be(overlay.Missions.Single().Contract!.Scan.Key);
        var route = overlay.ActiveRoute!;
        route.Header.Should().Be("▾ Plan: Everus ring · 1/2 filled · Σ 375,500 out · +0 back");
        route.Rows.Select(r => (r.Text, r.Filled)).Should().Equal(
            ("Bulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · Riker", false),
            ("✓ #1 · Small · Junior Rank - Small Cargo Haul · 50,250 aUEC · Riker", true));
    }

    [Fact]
    public void A_way_back_says_where_to_accept_and_collect_apart_and_the_tool_tip_holds_commodities_and_bands()
    {
        // Gate 6a (K2, K3): role, title, reward and places in the line; "accept at" a part of its own (orange in the
        // block), "collect at" after it; commodities and SCU bands only in the tool tip.
        var back = new Placeholder(3, RecipeKind.Return, "back", Title, 72_500,
            [new HaulLeg("Waste", 5, 30, CrateSize.Four), new HaulLeg("Waste", 5, 30, CrateSize.Four)], ["Everus Harbor"],
            "HUR-L1 Green Glade Station", ["HUR-L4 Melodic Fields Station", "HUR-L3 Thundering Express Station"]);
        var overlay = Overlay(Planned with { Placeholders = [.. Planned.Placeholders, back] });

        var row = overlay.ActiveRoute!.BackRows.Single();
        var open = overlay.ActiveRoute.Rows[0];

        (row.Text, row.Accept, row.Collect).Should().Be(
            ("Back · Junior Rank - Small Cargo Haul · 72,500 aUEC", " · accept at HUR-L1", " · collect at HUR-L4, HUR-L3"));
        row.ToolTip.Should().Contain("Waste 5–30 SCU in 4s").And.Contain("10–60 SCU in 4s");
        (open.Accept, open.Collect).Should().Be((null, null));
        open.ToolTip.Should().Contain("Waste 41–72 SCU in 16s").And.Contain("to Riker");
    }

    [Fact]
    public async Task An_accepted_contract_without_a_placeholder_is_an_extra_and_counts_for_the_route()
    {
        var overlay = Overlay(Planned, templates: []);

        await Scan(overlay, Raw(View.Accepted));

        overlay.Notices.Should().Equal("Scanned #1 · From Everus Harbor · 8 SCU Stims to Riker · not in plan (extra)");
        overlay.ActiveRoute!.ExtraRows.Should().Equal("+ #1 extra · 50,250 aUEC");
        _saved.Should().ContainSingle().Which!.Extras.Should().ContainSingle();
    }

    [Fact]
    public async Task The_same_accepted_scan_again_is_already_in_the_cargo_and_the_plan()
    {
        var overlay = Overlay(Planned);

        await Scan(overlay, Raw(View.Accepted), Raw(View.Accepted));

        overlay.Notices.Should().Equal("Already in cargo (#1) · plan ✓");
        _saved.Should().ContainSingle("the second scan changes nothing");
    }

    [Fact]
    public async Task Start_plan_takes_the_accepted_cargo_into_its_placeholders_and_the_rest_as_extras_without_a_rescan()
    {
        // Gate 6a (Martin, 25 Nov, option 1): accepted first, then Start plan; K3's rescan stays the fallback.
        var overlay = Overlay(null);
        await Scan(overlay, Raw(View.Accepted));
        overlay.ApplyScan(ApplyScanTests.Scanned(accepted: true, "41,750", "Deliver 0/12 SCU of Stims to Teasa Spaceport.", "Collect Stims from Everus Harbor."));

        overlay.ActiveRoute!.Start(Planned).Should().BeTrue();

        overlay.ActiveRoute.Header.Should().StartWith("▾ Plan: Everus ring · 1/2 filled");
        overlay.ActiveRoute.Rows[1].Text.Should().StartWith("✓ #1 · Small");
        overlay.ActiveRoute.ExtraRows.Should().Equal("+ #2 extra · 41,750 aUEC");
        TestLog.Text().Should().Contain("Routes: start: #1 → placeholder 2").And.Contain("Routes: start: #2 extra");
        _saved.Should().ContainSingle().Which!.Extras.Should().ContainSingle();

        await Scan(overlay, Raw(View.Accepted));
        overlay.Notices.Should().Equal("Already in cargo (#1) · plan ✓");
    }

    [Fact]
    public async Task A_contract_the_start_cannot_tell_apart_is_an_extra_until_its_rescan_names_the_rank()
    {
        // K3 stays the fallback: a Member twin of the Junior Small (same reward, legs, places) is a placeholder too, so at
        // the start the cargo fits both; the accepted scan's title says Junior.
        var member = Junior with { Uuid = "member", Rank = HaulRank.Member, Title = "Member Rank - Small Cargo Haul" };
        var route = Planned with { Placeholders = [.. Planned.Placeholders, new Placeholder(3, RecipeKind.Small, "member", member.Title, 50_250, member.Legs, ["Riker"])] };
        var overlay = Overlay(null, [Junior, member]);
        await Scan(overlay, Raw(View.Accepted));

        overlay.ActiveRoute!.Start(route).Should().BeTrue();
        overlay.ActiveRoute.ExtraRows.Should().Equal("+ #1 extra · 50,250 aUEC");

        await Scan(overlay, Raw(View.Accepted));

        overlay.Notices.Should().Equal("Already in cargo (#1) · plan: small ✓ 1/3");
        overlay.ActiveRoute.Rows[1].Filled.Should().BeTrue();
        overlay.ActiveRoute.ExtraRows.Should().BeEmpty();
    }

    [Fact]
    public async Task An_undo_opens_the_placeholder_and_routes_json_stays_as_it_was()
    {
        var overlay = Overlay(Planned);
        await Scan(overlay, Raw(View.Accepted));

        overlay.UndoCommand.Execute(null);

        overlay.ActiveRoute!.Rows[1].Filled.Should().BeFalse();
        overlay.ActiveRoute.Header.Should().StartWith("▾ Plan: Everus ring · 0/2 filled");
        _saved.Should().ContainSingle("filled is read from the plan, not written back");

        overlay.RedoCommand.Execute(null);
        overlay.ActiveRoute.Rows[1].Filled.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, "Scanned #1 · From Everus Harbor · 8 SCU Stims to Riker · rescan after accepting · plan: small")]
    [InlineData(true, "Observed (Plan tab): Covalex Junior Small · 50,250 aUEC · fits as small · plan: small – cargo unchanged")]
    public async Task An_offer_of_an_open_placeholders_template_says_so_and_fills_nothing(bool planTab, string notice)
    {
        var overlay = Overlay(Planned);
        overlay.IsRoutesTabSelected = planTab;

        await Scan(overlay, Raw(View.Offer));

        overlay.Notices.Should().Equal(notice);
        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_a_route_the_notices_are_as_before()
    {
        var overlay = Overlay(null);

        await Scan(overlay, Raw(View.Offer), Raw(View.Accepted));

        overlay.Notices.Should().Equal("Updated #1 · confirmed");
        (overlay.ActiveRoute!.HasRoute, overlay.ActiveRoute.Header).Should().Be((false, null));
        _saved.Should().BeEmpty();
    }

    [Fact]
    public void Ending_the_route_takes_the_block_and_routes_json_route_away_and_leaves_the_cargo()
    {
        var overlay = Overlay(Planned);
        overlay.ShowPlan(TestPlans.SplitAndShared());
        var cargo = overlay.Saved;

        overlay.ActiveRoute!.EndCommand.Execute(null);

        (overlay.ActiveRoute.HasRoute, overlay.ActiveRoute.Rows.Count).Should().Be((false, 0));
        _saved.Should().Equal([null]);
        overlay.Saved.Should().BeSameAs(cargo);
    }

    [Fact]
    public void A_second_route_replaces_the_first_only_when_the_player_says_yes()
    {
        var overlay = Overlay(Planned);
        var route = overlay.ActiveRoute!;
        var asked = new List<string>();
        route.ConfirmReplace = question => { asked.Add(question); return false; };
        var other = Planned with { Name = "Baijini ring" };

        route.Start(other).Should().BeFalse();
        route.ConfirmReplace = _ => true;
        route.Start(other).Should().BeTrue();

        asked.Should().Equal(ActiveRouteViewModel.ReplaceQuestion);
        route.Header.Should().StartWith("▾ Plan: Baijini ring");
        _saved.Should().Equal([other]);
    }

    [Fact]
    public void Start_plan_makes_the_route_of_the_loop_and_shows_the_Cargo_tab_unless_the_player_keeps_the_old_one()
    {
        var overlay = Overlay(null);
        overlay.IsRoutesTabSelected = true;
        var loop = new LoopCandidate(LoopKind.LocalRing, StarSystem.Stanton, "Everus Harbor", ["Riker"],
            [new RecipeRole(RecipeKind.Small, Junior, ["Riker"], 16, 4)], 50_250, false, 4, 0, 0, 0, 1);

        overlay.StartPlan(new PlanStart(loop, "Everus Harbor ring", ObservedEdges.Empty));

        overlay.IsRoutesTabSelected.Should().BeFalse("the player goes to the terminal");
        overlay.ActiveRoute!.Header.Should().Be("▾ Plan: Everus Harbor ring · 0/1 filled · Σ 50,250 out · +0 back");
        _saved.Should().ContainSingle().Which!.Placeholders.Single().TemplateUuid.Should().Be("junior");

        overlay.IsRoutesTabSelected = true;
        overlay.StartPlan(new PlanStart(loop, "Other ring", ObservedEdges.Empty));
        (overlay.IsRoutesTabSelected, overlay.ActiveRoute.Header).Should().Be((true, "▾ Plan: Everus Harbor ring · 0/1 filled · Σ 50,250 out · +0 back"),
            "no was said, the first route stays and so does the tab");
    }

    [Fact]
    public void The_Plan_tabs_Start_plan_button_reaches_the_overlay()
    {
        var overlay = Overlay(null);
        var routes = overlay.Routes!;
        routes.NextCommand.Execute("view");
        routes.NextCommand.Execute("view");
        routes.ShowLoops(routes.LoopInputs!, routes.LoopInputs!.Run());

        routes.StartPlanCommand.Execute(null);

        overlay.ActiveRoute!.Route!.Name.Should().Be(routes.LoopRows.First(r => r.Loop.HasNumbers).Title);
    }

    [Fact]
    public void The_map_follows_the_active_route_from_its_start_to_its_end()
    {
        var overlay = Overlay(null);
        var routes = overlay.Routes!;
        routes.PreviousCommand.Execute("view");
        routes.IsMap.Should().BeTrue();

        overlay.ActiveRoute!.Start(Planned).Should().BeTrue();
        routes.Map!.Graph.Active.Legs.Should().NotBeEmpty().And.HaveCount(Planned.Stations.Count + 1, "hub, every station, hub");

        overlay.ActiveRoute.EndCommand.Execute(null);
        routes.Map!.Graph.Active.Legs.Should().BeEmpty();
    }

    [Fact]
    public async Task The_map_draws_an_extra_of_the_route_from_its_pickup_to_its_stop()
    {
        // Gate finding 7a/7b: the scan makes an extra (no placeholder), the overlay hands its way to the map.
        var overlay = Overlay(Planned with { Placeholders = [] });
        var routes = overlay.Routes!;
        routes.PreviousCommand.Execute("view");

        await Scan(overlay, Raw(View.Accepted));

        overlay.ActiveRoute!.ExtraWays.Should().Equal(new MapExtra("Everus Harbor", "Riker", 1, 50_250));
        routes.Map!.Graph.Extras.Should().ContainSingle().Which.Should().BeEquivalentTo(new { From = "Everus Harbor", To = "Riker" });
    }

    [Fact]
    public void Starting_and_ending_the_route_leave_plan_json_byte_for_byte()
    {
        // D118: the route lives in routes.json; plan.json stays schema 11 and isn't even written.
        var planPath = Path.Combine(_folder, "plan.json");
        Sta.Run(() =>
        {
            var overlay = Overlay(null);
            var saver = new PlanSaver(new System.Windows.Window(), overlay, planPath, false, null);
            overlay.ShowPlan(TestPlans.SplitAndShared());
            saver.Flush();
            var before = SHA256.HashData(File.ReadAllBytes(planPath));
            var written = File.GetLastWriteTimeUtc(planPath);

            overlay.ActiveRoute!.Start(Planned).Should().BeTrue();
            saver.Flush();
            overlay.ActiveRoute.EndCommand.Execute(null);
            saver.Flush();

            SHA256.HashData(File.ReadAllBytes(planPath)).Should().Equal(before);
            File.GetLastWriteTimeUtc(planPath).Should().Be(written, "the saver didn't write at all");
            _saved.Should().Equal(Planned, null);
        });
    }

    [Fact]
    public async Task A_way_back_scanned_where_the_bar_stands_but_planned_elsewhere_says_so()
    {
        // E(c) (6b): the Junior Small to Riker is planned as the way back from Orison; the bar stands at Riker.
        var back = new Placeholder(3, RecipeKind.Return, "junior", Title, 50_250, Junior.Legs, ["Everus Harbor"], "Orison", []);
        var overlay = Overlay(Planned with { Stations = ["Riker", "Orison"], Placeholders = [Planned.Placeholders[0], back], At = "Riker", DepartedUtc = Monday });

        await Scan(overlay, Raw(View.Accepted));

        overlay.Notices.Should().Equal("Scanned #1 · From Everus Harbor · 8 SCU Stims to Riker · plan: back ✓ 1/2 (planned at Orison)");
    }

    [Fact]
    public void Finishing_the_route_leaves_plan_json_byte_for_byte()
    {
        // E1 of the approval: Finish too; the run goes to routes.json, plan.json isn't written.
        var planPath = Path.Combine(_folder, "plan.json");
        Sta.Run(() =>
        {
            var overlay = Overlay(Planned with { At = "Everus Harbor", DepartedUtc = Monday });
            var saver = new PlanSaver(new System.Windows.Window(), overlay, planPath, false, null);
            overlay.ShowPlan(TestPlans.SplitAndShared());
            saver.Flush();
            var before = SHA256.HashData(File.ReadAllBytes(planPath));
            var written = File.GetLastWriteTimeUtc(planPath);

            overlay.ActiveRoute!.IsAtEnd.Should().BeTrue();
            overlay.ActiveRoute.FinishPlanCommand.Execute(null);
            saver.Flush();

            SHA256.HashData(File.ReadAllBytes(planPath)).Should().Equal(before);
            File.GetLastWriteTimeUtc(planPath).Should().Be(written, "the saver didn't write at all");
            overlay.ActiveRoute.HasRoute.Should().BeFalse();
        });
    }

    private OverlayViewModel Overlay(Route? route, IReadOnlyList<HaulTemplate>? templates = null)
    {
        var data = new HaulingLoad.Loaded(new HaulingData(templates ?? [Junior], Places), []);
        return new(HullA)
        {
            Routes = new RoutesViewModel(data, new ShipDataVersion(2026, 11, 2), RoutesSettings.Default, null),
            ActiveRoute = new ActiveRouteViewModel(route, _saved.Add, data),
        };
    }

    /// <summary>A Junior Small to Riker in our OCR's reading order, offer or accepted (RoutesSwitchTests' text).</summary>
    private static string Raw(View view) =>
        $"{Title}\n{(view == View.Accepted ? "ABANDON\n" : "")}DETAILS\nReward 50,250\n"
        + (view == View.Offer ? "Contract Availability 2h 1m\n" : "Contract Deadline N/A\n")
        + "PRIMARY OBJECTIVES\nDeliver 0/8 SCU of Stims to Riker.\nCollect Stims from Everus Harbor."
        + (view == View.Offer ? "\nACCEPT OFFER" : "\nSHARE UNTRACK");

    private async Task Scan(OverlayViewModel overlay, params string[] raws)
    {
        var queue = new Queue<string>(raws);
        var runner = new ScanRunner(
            () => Task.FromResult(new ScanAttempt(ScanAttemptOutcome.Read, queue.Dequeue(), TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero)),
            overlay, new ScanLog(Path.Combine(_folder, "scans.log")), action => { action(); return Task.CompletedTask; }, _ => Task.CompletedTask);
        foreach (var _ in raws)
        {
            await runner.RunAsync();
        }
    }
}
