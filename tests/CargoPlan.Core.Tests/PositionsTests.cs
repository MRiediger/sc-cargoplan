using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>A scanned place to a position (T13.3, hauling-data-format.md "Names"): the B1 key against name, name without L code and contract names.</summary>
public class PositionsTests
{
    private static readonly StarPosition GreenGlade = Position("HUR-L1 Green Glade Station", StarSystem.Stanton);
    private static readonly StarPosition Everus = Position("Everus Harbor", StarSystem.Stanton);
    private static readonly StarPosition RuinStation = Position("Ruin Station", StarSystem.Pyro, "Ruin Station above Pyro VI");
    private static readonly StarPosition NyxGatewayPyro = Position("Nyx Gateway", StarSystem.Pyro);

    private static readonly Positions All = new([GreenGlade, Everus, RuinStation, NyxGatewayPyro]);

    [Theory]
    [InlineData("HUR-L1 Green Glade Station")]
    [InlineData("HUR-L1 Green Glade")]
    [InlineData("Green Glade Station")]
    [InlineData("Green Glade")]
    [InlineData("Green GIade Statlon")]
    [InlineData("green glade station")]
    public void An_L_station_answers_to_its_name_with_and_without_code_and_Station(string scanned) =>
        All.Resolve(StarSystem.Stanton, scanned).Should().Be(GreenGlade);

    [Fact]
    public void A_place_answers_to_its_contract_names() =>
        All.Resolve(StarSystem.Pyro, "Ruin Station above Pyro VI").Should().Be(RuinStation);

    [Theory]
    [InlineData(StarSystem.Stanton, "Green Glad")]
    [InlineData(StarSystem.Stanton, "HUR-L1")]
    [InlineData(StarSystem.Stanton, "Ruin Station")]
    [InlineData(StarSystem.Pyro, "Everus Harbor")]
    [InlineData(StarSystem.Stanton, "Nyx Gateway")]
    public void A_name_no_position_of_the_system_answers_to_is_none(StarSystem system, string scanned) =>
        All.Resolve(system, scanned).Should().BeNull();

    [Fact]
    public void A_name_two_positions_answer_to_is_none_not_a_guess()
    {
        var positions = new Positions([Position("Riker", StarSystem.Stanton), Position("Riker Station", StarSystem.Stanton)]);

        positions.Resolve(StarSystem.Stanton, "Riker").Should().BeNull();
    }

    [Fact]
    public void The_keys_of_a_position_are_its_three_name_forms_and_contract_names_each_once()
    {
        Positions.KeysOf(GreenGlade).Should().Equal("HUR-IIGREENGIADE", "GREENGIADE");
        Positions.KeysOf(RuinStation).Should().Equal("RUIN", "RUINSTATIONABOVEPYROVI");
    }

    private static StarPosition Position(string name, StarSystem system, params string[] contractNames) =>
        new(Guid.NewGuid().ToString("N"), name, system, "Manmade", null, 0, 0, 0, contractNames);
}
