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
            r.Slots.Should().BeEmpty();
        });
    }

    [Fact]
    public void Load_assigns_rack_index_from_array_order()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Racks.Select(r => r.Index).Should().Equal(0, 1, 2, 3);
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

    [Fact]
    public void Load_maps_slots_and_crate_sizes()
    {
        var ship = ShipLoader.Load(TestShips, "valid_with_slots");

        var slots = ship.Racks[0].Slots;
        slots.Should().HaveCount(2);
        slots[0].Id.Should().Be("a1");
        slots[0].AllowedSizes.Should().BeEquivalentTo([CrateSize.One, CrateSize.Two, CrateSize.Four]);
        slots[1].AllowedSizes.Should().BeEquivalentTo([CrateSize.Eight]);
    }

    [Theory]
    [InlineData("malformed")]               // not valid JSON
    [InlineData("sum_mismatch")]            // racks sum to 60, totalScu is 64
    [InlineData("zero_capacity_rack")]      // rack with 0 SCU
    [InlineData("unknown_property")]        // typo "capacitySCU"
    [InlineData("missing_property")]        // no "name"
    [InlineData("null_name")]               // "name": null
    [InlineData("id_mismatch")]             // "id" differs from file name
    [InlineData("unsupported_schema")]      // "schemaVersion": 2
    [InlineData("missing_schema")]          // no "schemaVersion"
    [InlineData("invalid_crate_size")]      // allowed size 3
    [InlineData("empty_allowed_sizes")]     // slot without sizes
    [InlineData("duplicate_rack_id")]       // two racks with the same id
    [InlineData("no_racks")]                // empty rack list
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

        act.Should().Throw<ShipFormatException>().WithMessage("*schemaVersion 2*");
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
        var dto = new RackDto("rack-0", 0, []);

        var act = () => dto.ToRack(0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*rack-0*");
    }

    [Fact]
    public void SlotDto_with_undefined_crate_size_throws_on_mapping()
    {
        var dto = new SlotDto("a1", [2, 3]);

        var act = () => dto.ToSlot();

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*3*");
    }
}
