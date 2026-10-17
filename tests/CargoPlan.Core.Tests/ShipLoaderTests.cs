using System.Reflection;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class ShipLoaderTests
{
    // Broken ship files live as embedded resources in this test assembly (TestShips/*.json).
    private static readonly Assembly TestShips = typeof(ShipLoaderTests).Assembly;

    // --- Shipped ships (CargoPlan.Core) ---

    [Fact]
    public void Load_hull_a_returns_four_racks_of_16_scu()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Id.Should().Be("hull_a");
        ship.TotalScu.Should().Be(64);
        ship.Racks.Should().HaveCount(4);
        ship.Racks.Should().AllSatisfy(r =>
        {
            r.CapacityScu.Should().Be(16);
            r.Grid.Should().Be(new Dimensions(4, 2, 2));
        });
    }

    [Fact]
    public void Load_assigns_rack_index_from_array_order()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Racks.Select(r => r.Index).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void Load_hull_a_assigns_positions_in_rack_order()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Racks.Select(r => r.Position).Should().Equal(
            new RackPosition(RackSide.Port, RackLevel.Upper),
            new RackPosition(RackSide.Port, RackLevel.Lower),
            new RackPosition(RackSide.Starboard, RackLevel.Upper),
            new RackPosition(RackSide.Starboard, RackLevel.Lower));
    }

    [Fact]
    public void ListShips_contains_hull_a()
    {
        ShipLoader.ListShips().Should().Contain("hull_a");
    }

    public static TheoryData<string> ShippedShipIds => [.. ShipLoader.ListShips()];

    // Gate for community PRs: every ship file in Core must load.
    [Theory]
    [MemberData(nameof(ShippedShipIds))]
    public void Every_shipped_ship_loads(string shipId)
    {
        // Lowercase snake_case: no casing surprises between Windows and Linux file systems.
        shipId.Should().MatchRegex("^[a-z0-9]+(_[a-z0-9]+)*$");

        var ship = ShipLoader.Load(shipId);

        ship.Id.Should().Be(shipId);
    }

    [Fact]
    public void Load_unknown_id_throws_ShipNotFoundException_listing_available_ids()
    {
        var act = () => ShipLoader.Load("hull_z");

        var ex = act.Should().Throw<ShipNotFoundException>().Which;
        ex.ShipId.Should().Be("hull_z");
        ex.AvailableShipIds.Should().Contain("hull_a");
        ex.Message.Should().Contain("hull_z").And.Contain("hull_a");
    }

    [Fact]
    public void Load_is_case_sensitive()
    {
        var act = () => ShipLoader.Load("Hull_A");

        act.Should().Throw<ShipNotFoundException>();
    }

    // --- Test ships (this assembly) ---

    [Theory]
    [InlineData("malformed")]               // not valid JSON
    [InlineData("sum_mismatch")]            // racks sum to 60, totalScu is 64
    [InlineData("zero_capacity_rack")]      // rack with 0 SCU
    [InlineData("unknown_property")]        // typo "capacitySCU"
    [InlineData("missing_property")]        // no "name"
    [InlineData("null_name")]               // "name": null
    [InlineData("id_mismatch")]             // "id" differs from file name
    [InlineData("unsupported_schema")]      // "schemaVersion": 3
    [InlineData("missing_schema")]          // no "schemaVersion"
    [InlineData("duplicate_rack_id")]       // two racks with the same id
    [InlineData("no_racks")]                // empty rack list
    [InlineData("grid_capacity_mismatch")]  // grid holds 16 cells, capacityScu is 12
    [InlineData("zero_grid_axis")]          // grid width 0
    [InlineData("unknown_side")]            // "side": "left"
    [InlineData("unknown_level")]           // "level": "middle"
    public void Load_invalid_ship_throws_ShipFormatException_naming_the_resource(string shipId)
    {
        var act = () => ShipLoader.Load(TestShips, shipId);

        var ex = act.Should().Throw<ShipFormatException>().Which;
        ex.ResourceName.Should().Be($"ships/{shipId}.json");
        ex.Message.Should().Contain($"ships/{shipId}.json");
    }

    [Fact]
    public void Load_sum_mismatch_reports_the_rack_sum()
    {
        var act = () => ShipLoader.Load(TestShips, "sum_mismatch");

        act.Should().Throw<ShipFormatException>()
            .WithMessage("*60*64*")
            .WithInnerException<ArgumentException>();
    }

    [Fact]
    public void Load_unsupported_schema_names_the_version()
    {
        var act = () => ShipLoader.Load(TestShips, "unsupported_schema");

        act.Should().Throw<ShipFormatException>().WithMessage("*schemaVersion 3*");
    }

    [Fact]
    public void Load_grid_capacity_mismatch_names_rack_and_numbers()
    {
        var act = () => ShipLoader.Load(TestShips, "grid_capacity_mismatch");

        act.Should().Throw<ShipFormatException>().WithMessage("*rack-0*4×2×2*16*12*");
    }

    [Fact]
    public void Load_zero_grid_axis_names_rack_and_axis()
    {
        var act = () => ShipLoader.Load(TestShips, "zero_grid_axis");

        act.Should().Throw<ShipFormatException>().WithMessage("*rack-0*width*");
    }

    [Fact]
    public void Load_unknown_side_names_rack_and_value()
    {
        var act = () => ShipLoader.Load(TestShips, "unknown_side");

        act.Should().Throw<ShipFormatException>().WithMessage("*rack-0*left*");
    }

    [Fact]
    public void Load_unknown_property_names_the_property()
    {
        var act = () => ShipLoader.Load(TestShips, "unknown_property");

        act.Should().Throw<ShipFormatException>().WithMessage("*capacitySCU*");
    }

    // --- DTO mapping ---

    [Fact]
    public void RackDto_with_zero_capacity_throws_on_mapping()
    {
        var dto = new RackDto("rack-0", 0, new GridDto(4, 2, 2), new PositionDto("port", "upper"));

        var act = () => dto.ToRack(0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*rack-0*");
    }

    [Theory]
    [InlineData("Port", "upper")]   // case-sensitive like every other field
    [InlineData("port", "1")]       // no numbers for enums
    public void PositionDto_rejects_unknown_values(string side, string level)
    {
        var act = () => new PositionDto(side, level).ToPosition("rack-0");

        act.Should().Throw<ArgumentException>().WithMessage("*rack-0*");
    }
}
