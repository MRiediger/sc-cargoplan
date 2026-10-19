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
    public void Load_hull_a_assigns_origins_in_rack_order()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Racks.Select(r => r.Origin).Should().Equal(
            new ShipPoint(0, 1, 3),
            new ShipPoint(0, 1, 0),
            new ShipPoint(0, -2, 3),
            new ShipPoint(0, -2, 0));
    }

    // In the game the Hull A stacks from the spindle outwards (D26).
    [Fact]
    public void Load_hull_a_stacks_away_from_the_spindle()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Racks.Select(r => r.StackAxis).Should().Equal(StackAxis.PlusY, StackAxis.PlusY, StackAxis.MinusY, StackAxis.MinusY);
    }

    [Fact]
    public void Load_hull_a_bounds_span_all_racks()
    {
        var ship = ShipLoader.Load("hull_a");

        ship.Bounds.Should().Be(new ShipBox(new ShipPoint(0, -2, 0), new ShipPoint(4, 3, 5)));
    }

    // Position and origin are kept separately (D14); for the Hull A they must tell the same story.
    [Fact]
    public void Load_hull_a_origins_agree_with_positions()
    {
        var racks = ShipLoader.Load("hull_a").Racks;

        racks.Where(r => r.Position.Side == RackSide.Port)
            .Should().AllSatisfy(r => r.Bounds.Min.Y.Should().BePositive());
        racks.Where(r => r.Position.Side == RackSide.Starboard)
            .Should().AllSatisfy(r => r.Bounds.Max.Y.Should().BeLessThanOrEqualTo(0));

        foreach (var side in racks.GroupBy(r => r.Position.Side))
        {
            var upper = side.Single(r => r.Position.Level == RackLevel.Upper);
            var lower = side.Single(r => r.Position.Level == RackLevel.Lower);
            upper.Bounds.Min.Z.Should().BeGreaterThanOrEqualTo(lower.Bounds.Max.Z);
        }
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
    [InlineData("unsupported_schema")]      // "schemaVersion": 5
    [InlineData("previous_schema")]         // "schemaVersion": 3, no "stackAxis"
    [InlineData("old_schema")]              // "schemaVersion": 2, no "origin"
    [InlineData("missing_schema")]          // no "schemaVersion"
    [InlineData("missing_origin")]          // v3 rack without "origin"
    [InlineData("origin_wrong_length")]     // "origin": [0, 0]
    [InlineData("missing_stack_axis")]      // v4 rack without "stackAxis"
    [InlineData("unknown_stack_axis")]      // "stackAxis": "up"
    [InlineData("overlapping_racks")]       // Hull A with Rack 4 on Rack 2's origin
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

        act.Should().Throw<ShipFormatException>().WithMessage("*schemaVersion 5*");
    }

    [Theory]
    [InlineData("previous_schema", "*schemaVersion 3*expected 4*")]
    [InlineData("old_schema", "*schemaVersion 2*expected 4*")]
    public void Load_old_schema_names_version_and_expected_version(string shipId, string expectedMessage)
    {
        var act = () => ShipLoader.Load(TestShips, shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void Load_overlapping_racks_names_both_rack_numbers()
    {
        var act = () => ShipLoader.Load(TestShips, "overlapping_racks");

        act.Should().Throw<ShipFormatException>()
            .WithMessage("*Rack 2 ('rack-1')*[0, 1, 0]*Rack 4 ('rack-3')*[0, 1, 0]*overlap*")
            .WithInnerException<ArgumentException>();
    }

    [Theory]
    [InlineData("missing_origin", "*origin*")]              // required since schemaVersion 3
    [InlineData("origin_wrong_length", "*rack-0*origin*got 2*")]
    public void Load_bad_origin_names_the_field(string shipId, string expectedMessage)
    {
        var act = () => ShipLoader.Load(TestShips, shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
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

    [Theory]
    [InlineData("missing_stack_axis", "*stackAxis*")]   // required since schemaVersion 4
    [InlineData("unknown_stack_axis", "*Rack 1 ('rack-0')*stackAxis 'up'*\"+x\"*")]
    public void Load_bad_stack_axis_names_the_field(string shipId, string expectedMessage)
    {
        var act = () => ShipLoader.Load(TestShips, shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
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
        var dto = new RackDto("rack-0", 0, new GridDto(4, 2, 2), new PositionDto("port", "upper"), [0, 0, 0], "+z");

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

    [Theory]
    [InlineData("+x", StackAxis.PlusX)]
    [InlineData("-x", StackAxis.MinusX)]
    [InlineData("+y", StackAxis.PlusY)]
    [InlineData("-y", StackAxis.MinusY)]
    [InlineData("+z", StackAxis.PlusZ)]
    [InlineData("-z", StackAxis.MinusZ)]
    public void RackDto_maps_every_stack_axis(string stackAxis, StackAxis expected)
    {
        var dto = new RackDto("rack-0", 16, new GridDto(4, 2, 2), new PositionDto("port", "upper"), [0, 0, 0], stackAxis);

        dto.ToRack(0).StackAxis.Should().Be(expected);
    }

    [Theory]
    [InlineData("+Z")]   // case-sensitive like every other field
    [InlineData("z")]    // the sign is required
    [InlineData("4")]    // no numbers for enums
    public void RackDto_rejects_unknown_stack_axis(string stackAxis)
    {
        var dto = new RackDto("rack-0", 16, new GridDto(4, 2, 2), new PositionDto("port", "upper"), [0, 0, 0], stackAxis);

        var act = () => dto.ToRack(0);

        act.Should().Throw<ArgumentException>().WithMessage($"*Rack 1 ('rack-0')*'{stackAxis}'*");
    }
}
