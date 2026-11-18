using CargoPlan.App.ShipData;
using CargoPlan.Core.ShipData;

namespace CargoPlan.App.Tests;

/// <summary>The ship data check's decisions (T12.3, D112), without the network: whether to ask, and what to make of the manifest.</summary>
public class ShipDataGateTests
{
    private static ShipDataVersion V(string tag) => ShipDataVersion.TryParse(tag, out var v) ? v : throw new ArgumentException(tag);

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void It_asks_when_switched_on_and_a_dev_build_only_with_a_source(bool checkOnStartup, bool isDevBuild, bool hasSource, bool asks) =>
        ShipDataGate.ShouldCheck(checkOnStartup, isDevBuild, hasSource).Should().Be(asks);

    [Fact]
    public void A_newer_readable_release_is_offered() =>
        ShipDataGate.Offer(V("ships-2026.11"), V("ships-2026.11.1"), schemaVersion: 6, skipped: null)
            .Should().Be(new ShipDataOffer(V("ships-2026.11.1")));

    [Theory]
    [InlineData("ships-2026.11.1", "ships-2026.11.1")]
    [InlineData("ships-2026.12", "ships-2026.11.1")]
    [InlineData("ships-2026.11", "ships-2026.10")]
    public void The_same_or_an_older_release_is_not(string active, string found) =>
        ShipDataGate.Offer(V(active), V(found), 6, null).Should().BeOfType<NoShipDataUpdate>()
            .Which.Reason.Should().Be($"nothing newer than {active}");

    [Fact]
    public void The_skipped_release_is_not_offered_a_newer_one_is()
    {
        ShipDataGate.Offer(V("ships-2026.11"), V("ships-2026.11.1"), 6, "ships-2026.11.1")
            .Should().Be(new NoShipDataUpdate("ships-2026.11.1 skipped (shipData.skippedVersion)", V("ships-2026.11.1")));
        ShipDataGate.Offer(V("ships-2026.11"), V("ships-2026.12"), 6, "ships-2026.11.1")
            .Should().Be(new ShipDataOffer(V("ships-2026.12")));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(3)]
    public void A_newer_release_of_a_schema_the_app_cannot_read_needs_an_app_update(int schemaVersion) =>
        ShipDataGate.Offer(V("ships-2026.11"), V("ships-2026.12"), schemaVersion, null)
            .Should().Be(new ShipDataNeedsApp(V("ships-2026.12"), schemaVersion));

    [Fact]
    public void An_unknown_schema_waits_even_when_that_release_is_skipped()
    {
        // No Skip on that line (P6): a skip set before the schema step can't hide it.
        ShipDataGate.Offer(V("ships-2026.11"), V("ships-2026.12"), 7, "ships-2026.12").Should().BeOfType<ShipDataNeedsApp>();
    }

    [Fact]
    public void An_older_release_of_an_unknown_schema_says_nothing() =>
        ShipDataGate.Offer(V("ships-2026.12"), V("ships-2026.11.1"), 7, null).Should().BeOfType<NoShipDataUpdate>();

    [Fact]
    public void The_needs_app_update_text_names_the_release() =>
        ShipDataGate.NeedsAppText(V("ships-2026.12")).Should().Be("Ship data 2026.12 needs an app update");
}
