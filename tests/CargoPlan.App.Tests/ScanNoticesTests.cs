using CargoPlan.App.Scanning;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

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
    [InlineData(ScanReportKind.AlreadyInPlan, "Already in plan (#3)")]
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
}
