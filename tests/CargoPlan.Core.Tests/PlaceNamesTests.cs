using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The order and short names of places (T13.2, Martin's picture gate): hubs first, then the L stations planet by planet
/// (Hurston, Crusader, ArcCorp, microTech), the rest alphabetical; an L station shows as its code "HUR-L1".
/// </summary>
public class PlaceNamesTests
{
    [Theory]
    [InlineData("HUR-L1 Green Glade Station", "HUR-L1")]
    [InlineData("ARC-L5 Yellow Core Station", "ARC-L5")]
    [InlineData("MIC-L12 Some Station", "MIC-L12")]
    [InlineData("Everus Harbor", "Everus Harbor")]
    [InlineData("Covalex Distribution Centre S1DC06", "Covalex Distribution Centre S1DC06")]
    [InlineData("HUR-L1", "HUR-L1")]
    [InlineData("Hur-L1 Green Glade Station", "Hur-L1 Green Glade Station")]
    public void An_L_station_shows_as_its_code_everything_else_in_full(string name, string shown) =>
        PlaceNames.Short(name).Should().Be(shown);

    [Fact]
    public void Hubs_come_first_then_the_L_stations_per_planet_then_the_rest_alphabetical()
    {
        string[] places =
        [
            "Clio", "MIC-L1 Shallow Frontier Station", "Port Tressler", "ARC-L2 Lively Pathway Station", "Baijini Point",
            "HUR-L2 Faithful Dream Station", "astor's Clearing", "CRU-L1 Ambitious Dream Station", "Everus Harbor",
            "HUR-L10 Far Station", "HUR-L1 Green Glade Station", "Seraphim Station", "Checkmate",
        ];

        places.Order(PlaceNames.Order).Should().Equal(
            "Everus Harbor", "Seraphim Station", "Baijini Point", "Port Tressler",
            "HUR-L1 Green Glade Station", "HUR-L2 Faithful Dream Station", "HUR-L10 Far Station",
            "CRU-L1 Ambitious Dream Station", "ARC-L2 Lively Pathway Station", "MIC-L1 Shallow Frontier Station",
            "astor's Clearing", "Checkmate", "Clio");
    }

    [Fact]
    public void A_place_as_the_card_writes_it_is_its_positions_short_name_else_as_it_is()
    {
        // The legend's header (Martin, T13.6): "From Everus Harbor · to HUR-L5 · #1"; the card writes "High Course Station".
        var positions = new Positions(
        [
            new StarPosition("1", "HUR-L5 High Course Station", StarSystem.Stanton, "Manmade", null, 0, 0, 0, []),
            new StarPosition("2", "Everus Harbor", StarSystem.Stanton, "Manmade", null, 0, 0, 0, []),
            new StarPosition("3", "Ruin Station", StarSystem.Pyro, "Manmade", null, 0, 0, 0, []),
        ]);

        PlaceNames.ShortOf("High Course Station", positions).Should().Be("HUR-L5");
        PlaceNames.ShortOf("Everus Harbor", positions).Should().Be("Everus Harbor");
        PlaceNames.ShortOf("Ruin Station", positions).Should().Be("Ruin Station");
        PlaceNames.ShortOf("Riker", positions).Should().Be("Riker", "no position answers to it");
        PlaceNames.ShortOf("High Course Station", Positions.Empty).Should().Be("High Course Station", "without hauling data");
    }

    [Fact]
    public void The_order_follows_the_board_hubs_table() =>
        BoardHubs.ByArea.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => h.Value).Order(PlaceNames.Order)
            .Should().Equal(BoardHubs.ByArea.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => h.Value));
}
