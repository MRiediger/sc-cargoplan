using CargoPlan.App.Scanning;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;

namespace CargoPlan.App.Tests;

/// <summary>One notice per scan result (T10.3, D104): what the player reads in the overlay's last notice line.</summary>
public sealed class ScanNoticesTests
{
    private static MissionItem Item(string commodity, int scu, params string[] pickup) =>
        new(commodity, scu, CrateSize.Four, Pickup: pickup.Length == 0 ? null : pickup);

    private static Mission Scanned(int no, bool estimated, params MissionStop[] stops) =>
        new(1, stops, new ContractSource(new ScanIdentity(no, "key", estimated)));

    [Fact]
    public void A_new_contract_with_one_destination_names_pickup_amount_commodity_and_destination()
    {
        var mission = Scanned(3, estimated: false, new MissionStop("High Course", 1, [Item("Stims", 13, "Everus Harbor")]));

        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), mission)
            .Should().Be("Scanned #3 · From Everus Harbor · 13 SCU Stims to High Course");
    }

    [Fact]
    public void A_new_contract_with_several_destinations_sums_them()
    {
        var mission = Scanned(3, estimated: false,
            new MissionStop("High Course", 1, [Item("Stims", 13, "Everus Harbor")]),
            new MissionStop("Riker", 2, [Item("Stims", 21, "Everus Harbor")]),
            new MissionStop("Lorville", 3, [Item("Stims", 12, "Everus Harbor")]));

        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), mission)
            .Should().Be("Scanned #3 · From Everus Harbor · 46 SCU Stims to 3 destinations");
    }

    [Fact]
    public void Several_commodities_leave_the_commodity_out()
    {
        var mission = Scanned(3, estimated: false,
            new MissionStop("High Course", 1, [Item("Stims", 13, "Everus Harbor"), Item("Scrap", 4, "Everus Harbor")]));

        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), mission)
            .Should().Be("Scanned #3 · From Everus Harbor · 17 SCU to High Course");
    }

    [Fact]
    public void Several_pickups_are_named_each_once()
    {
        var mission = Scanned(3, estimated: false,
            new MissionStop("Riker", 1, [Item("Stims", 8, "Everus Harbor"), Item("Scrap", 4, "Baijini Point", "everus harbor")]));

        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), mission)
            .Should().Be("Scanned #3 · From Everus Harbor / Baijini Point · 12 SCU to Riker");
    }

    [Fact]
    public void Without_a_pickup_there_is_no_from()
    {
        var mission = Scanned(3, estimated: false, new MissionStop("Riker", 1, [Item("Stims", 8)]));

        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), mission).Should().Be("Scanned #3 · 8 SCU Stims to Riker");
    }

    [Fact]
    public void An_offer_asks_for_a_rescan_after_accepting()
    {
        var mission = Scanned(3, estimated: true, new MissionStop("Riker", 1, [Item("Stims", 8, "Everus Harbor")]));

        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), mission)
            .Should().Be("Scanned #3 · From Everus Harbor · 8 SCU Stims to Riker · rescan after accepting");
    }

    [Fact]
    public void A_mission_gone_since_the_scan_leaves_the_number()
    {
        ScanNotices.Text(new ScanReport(ScanReportKind.Added, 3), null).Should().Be("Scanned #3");
    }

    [Theory]
    [InlineData(ScanReportKind.Updated, "Updated #3 · confirmed")]
    [InlineData(ScanReportKind.AlreadyInPlan, "Already in cargo (#3)")]
    [InlineData(ScanReportKind.Incomplete, "Contract text still loading – scan again")]
    [InlineData(ScanReportKind.NoContract, "No contract in frame")]
    [InlineData(ScanReportKind.NoContractInFrame, "No contract in frame")]
    [InlineData(ScanReportKind.GameNotFound, "Star Citizen window not found")]
    [InlineData(ScanReportKind.Busy, "Scan running")]
    public void Every_other_result_has_its_text(ScanReportKind kind, string expected)
    {
        ScanNotices.Text(new ScanReport(kind, kind is ScanReportKind.Updated or ScanReportKind.AlreadyInPlan ? 3 : null), null)
            .Should().Be(expected);
    }

    [Fact]
    public void Changed_quantities_say_so()
    {
        ScanNotices.Text(new ScanReport(ScanReportKind.Updated, 3, QuantitiesChanged: true), null)
            .Should().Be("Updated #3 · quantities changed");
    }

    [Fact]
    public void Deliveries_no_longer_loaded_are_named()
    {
        ScanReport report = new(ScanReportKind.Updated, 3, QuantitiesChanged: true, Cleared: ["Riker: changed by scan – no longer loaded"]);

        ScanNotices.Text(report, null).Should().Be("Updated #3 · quantities changed · Riker: changed by scan – no longer loaded");
    }

    [Fact]
    public void A_refused_update_gives_its_reason()
    {
        ScanNotices.Text(new ScanReport(ScanReportKind.Refused, 3, Reason: "no room for Riker"), null)
            .Should().Be("Scan #3 not applied: no room for Riker");
    }

    [Fact]
    public void A_failed_capture_gives_the_win32_error()
    {
        ScanNotices.Text(new ScanReport(ScanReportKind.CaptureFailed, Reason: "Win32 error 5"), null)
            .Should().Be("Scan failed: screen capture refused (Win32 error 5)");
    }

    [Fact]
    public void Unavailable_leaves_the_line_to_the_scanner()
    {
        // ContractScanner sets "Contract scan unavailable: …" in its own line (T10.1b); a second line would say it twice.
        ScanNotices.Text(new ScanReport(ScanReportKind.Unavailable), null).Should().BeNull();
    }

    [Fact]
    public void Every_kind_is_covered()
    {
        foreach (var kind in Enum.GetValues<ScanReportKind>())
        {
            var act = () => ScanNotices.Text(new ScanReport(kind, 1, Reason: "x"), null);
            act.Should().NotThrow(kind.ToString());
        }
    }

    // T13.1: with Routes selected an offer stays out of the plan; the line names the template or why there is none.
    [Theory]
    [InlineData(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Small, false, 50_250L, "Observed (Plan tab): Covalex Junior Small · 50,250 aUEC – cargo unchanged")]
    [InlineData(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Xs, true, 41_750L, "Observed (Plan tab): Covalex Rookie Direct Extra Small · 41,750 aUEC – cargo unchanged")]
    [InlineData(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, null, 532_500L, "Observed (Plan tab): Red Wind Member Medium · 532,500 aUEC – cargo unchanged")]
    public void An_observed_offer_names_its_template(HaulContractor contractor, HaulRank rank, HaulSize size, bool? direct, long reward, string expected)
    {
        var report = new ScanReport(ScanReportKind.Observed, Observed: new ObservationKey(contractor, rank, size, direct, reward, ["Stims"]));

        ScanNotices.Text(report, null).Should().Be(expected);
    }

    // T13.3b: the check before accepting, after the template; "cargo" stays the loading plan's word (E1 of T13.2).
    [Theory]
    [InlineData("bulk", "Observed (Plan tab): Covalex Senior Medium · 325,250 aUEC · fits as bulk – cargo unchanged")]
    [InlineData("too big", "Observed (Plan tab): Covalex Senior Medium · 325,250 aUEC · too big for MISC Hull B (432 SCU in 16s) – cargo unchanged")]
    [InlineData("none", "Observed (Plan tab): Covalex Senior Medium · 325,250 aUEC – cargo unchanged")]
    public void An_observed_offer_says_whether_it_fits_the_recipe(string fit, string expected)
    {
        RecipeFit? recipeFit = fit switch
        {
            "bulk" => new RecipeFit.FitsAs(RecipeKind.Bulk),
            "too big" => new RecipeFit.TooBig(432, CrateSize.Sixteen, "MISC Hull B"),
            _ => null,
        };
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 325_250, ["Waste"]);

        ScanNotices.Text(new ScanReport(ScanReportKind.Observed, Observed: key, Fit: recipeFit), null).Should().Be(expected);
    }

    [Theory]
    [InlineData(RecipeKind.Direct, "fits as direct")]
    [InlineData(RecipeKind.Small, "fits as small")]
    public void Every_kind_reads_in_the_notice(RecipeKind kind, string part)
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Small, false, 50_250, ["Stims"]);

        ScanNotices.Text(new ScanReport(ScanReportKind.Observed, Observed: key, Fit: new RecipeFit.FitsAs(kind)), null).Should().Contain($" · {part} – ");
    }

    [Fact]
    public void An_offer_not_observed_says_why_and_that_the_plan_stays()
    {
        ScanNotices.Text(new ScanReport(ScanReportKind.NotObserved, Reason: "reward unreadable"), null)
            .Should().Be("Not observed (Plan tab): reward unreadable – cargo unchanged");
    }

    [Theory]
    [InlineData(ScanReportKind.Added, "filled", "Scanned #3 · plan: bulk ✓ 2/10")]
    [InlineData(ScanReportKind.Updated, "filled", "Updated #3 · confirmed · plan: bulk ✓ 2/10")]
    [InlineData(ScanReportKind.AlreadyInPlan, "filled", "Already in cargo (#3) · plan: bulk ✓ 2/10")]
    [InlineData(ScanReportKind.AlreadyInPlan, "already", "Already in cargo (#3) · plan ✓")]
    [InlineData(ScanReportKind.Added, "extra", "Scanned #3 · not in plan (extra)")]
    [InlineData(ScanReportKind.Added, "offer", "Scanned #3 · plan: bulk")]
    public void A_scan_with_a_route_says_what_it_did_to_the_plan_last(ScanReportKind kind, string note, string expected)
    {
        // T13.6, U4: the route is "plan", the loading plan "cargo"; the route's part comes last.
        RouteNote route = note switch
        {
            "filled" => new RouteNote.Filled(RecipeKind.Bulk, 2, 10),
            "already" => new RouteNote.AlreadyFilled(),
            "extra" => new RouteNote.Extra(),
            _ => new RouteNote.Open(RecipeKind.Bulk),
        };

        ScanNotices.Text(new ScanReport(kind, 3, Route: route), null).Should().Be(expected);
    }

    [Fact]
    public void An_observed_offer_of_an_open_placeholder_says_so_before_the_cargo_stays()
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 325_250, ["Waste"]);

        ScanNotices.Text(new ScanReport(ScanReportKind.Observed, Observed: key, Fit: new RecipeFit.FitsAs(RecipeKind.Bulk), Route: new RouteNote.Open(RecipeKind.Bulk)), null)
            .Should().Be("Observed (Plan tab): Covalex Senior Medium · 325,250 aUEC · fits as bulk · plan: bulk – cargo unchanged");
    }
}
