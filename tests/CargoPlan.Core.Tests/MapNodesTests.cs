using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The map's nodes (T13.7, K1 of the approval): a station is a node of its own, every other place joins the node of the
/// nearest planet, "Hurston (surface)". Positions built here, not read (A10).
/// </summary>
public sealed class MapNodesTests
{
    private static readonly StarPosition Hurston = P("Hurston", "Planet", 12.85, 0);
    private static readonly StarPosition Crusader = P("Crusader", "Planet", -18.96, -2.66);
    private static readonly StarPosition Everus = P("Everus Harbor", "Manmade", 12.849, 0.001);
    private static readonly StarPosition GreenGlade = P("HUR-L1 Green Glade Station", "Manmade_VisibleOnInteraction", 11.56, 0);
    private static readonly StarPosition Lorville = P("Lorville", "LandingZone", 12.85, 0);
    private static readonly StarPosition Ita = P("Ita", "Moon", 12.83, 0.11);
    private static readonly StarPosition Daymar = P("Daymar Outpost", "Outpost", -18.93, -2.61);
    private static readonly StarPosition Ruptura = P("Ruptura PAF-I", "Outpost", 12.91, 0.04);

    private static readonly Positions All = new([Hurston, Crusader, Everus, GreenGlade, Lorville, Ita, Daymar, Ruptura]);

    [Fact]
    public void A_station_is_its_own_node_and_a_board_hub_is_a_hub()
    {
        var nodes = MapNodes.For(["Everus Harbor", "HUR-L1 Green Glade Station"], All, StarSystem.Stanton);

        nodes["Everus Harbor"].Should().BeEquivalentTo(new { Key = "Everus Harbor", Label = "Everus Harbor", Kind = MapNodeKind.Hub });
        nodes["HUR-L1 Green Glade Station"].Should().BeEquivalentTo(new { Key = "HUR-L1 Green Glade Station", Label = "HUR-L1", Kind = MapNodeKind.Station });
        nodes["HUR-L1 Green Glade Station"].Places.Should().Equal("HUR-L1 Green Glade Station");
    }

    [Fact]
    public void Surface_places_and_moons_join_the_node_of_the_nearest_planet_at_the_planets_position()
    {
        var nodes = MapNodes.For(["Lorville", "Ita", "Ruptura PAF-I", "Daymar Outpost", "Hurston"], All, StarSystem.Stanton);

        var hurston = nodes["Lorville"];
        hurston.Should().BeSameAs(nodes["Ita"]).And.BeSameAs(nodes["Ruptura PAF-I"]).And.BeSameAs(nodes["Hurston"]);
        hurston.Should().BeEquivalentTo(new { Key = "Hurston (surface)", Label = "Hurston (surface)", Kind = MapNodeKind.Surface, Hurston.X, Hurston.Y });
        hurston.Places.Should().Equal(["Hurston", "Ita", "Lorville", "Ruptura PAF-I"], "the places in place order, each once");
        nodes["Daymar Outpost"].Key.Should().Be("Crusader (surface)");
    }

    [Fact]
    public void A_place_without_a_position_of_the_system_is_no_node()
    {
        MapNodes.For(["Nowhere", "Everus Harbor"], All, StarSystem.Stanton).Keys.Should().Equal("Everus Harbor");
        MapNodes.For(["Everus Harbor"], All, StarSystem.Pyro).Should().BeEmpty();
    }

    [Fact]
    public void Without_a_planet_a_surface_place_is_a_node_of_its_own()
    {
        var positions = new Positions([P("Lonely Outpost", "Outpost", 5, 5)]);

        MapNodes.For(["Lonely Outpost"], positions, StarSystem.Stanton)["Lonely Outpost"].Kind.Should().Be(MapNodeKind.Station);
    }

    internal static StarPosition P(string name, string type, double xGm, double yGm, StarSystem system = StarSystem.Stanton) =>
        new(Guid.NewGuid().ToString("N"), name, system, type, null, xGm * 1e9, yGm * 1e9, 0, []);
}
