using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class ShipLoaderTests
{
    // --- Shipped ships (ships/ next to the test assembly) ---

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

        // Three cells between the plates since K5.2, as Fleetyards gives it (D78); one before.
        ship.Racks.Select(r => r.Origin).Should().Equal(
            new ShipPoint(0, 1, 3),
            new ShipPoint(0, 1, 0),
            new ShipPoint(0, -4, 3),
            new ShipPoint(0, -4, 0));
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

        ship.Bounds.Should().Be(new ShipBox(new ShipPoint(0, -4, 0), new ShipPoint(4, 3, 5)));
    }

    // Position and origin are kept separately (D14); for the Hull A they must tell the same story.
    [Fact]
    public void Load_hull_a_origins_agree_with_positions()
    {
        var racks = ShipLoader.Load("hull_a").Racks;

        racks.Where(r => r.Position is { Side: RackSide.Port })
            .Should().AllSatisfy(r => r.Bounds.Min.Y.Should().BePositive());
        racks.Where(r => r.Position is { Side: RackSide.Starboard })
            .Should().AllSatisfy(r => r.Bounds.Max.Y.Should().BeLessThanOrEqualTo(0));

        foreach (var side in racks.GroupBy(r => r.Position?.Side))
        {
            var upper = side.Single(r => r.Position is { Level: RackLevel.Upper });
            var lower = side.Single(r => r.Position is { Level: RackLevel.Lower });
            upper.Bounds.Min.Z.Should().BeGreaterThanOrEqualTo(lower.Bounds.Max.Z);
        }
    }

    [Fact]
    public void LoadAll_contains_hull_a()
    {
        ShipLoader.LoadAll().Ships.Select(s => s.Id).Should().Contain(ShipLoader.FallbackShipId);
    }

    // Gate for the ship files (T7.1b converts ~96): every file in ships/ loads, none is skipped.
    [Fact]
    public void LoadAll_loads_every_shipped_file_without_problems()
    {
        var catalog = ShipLoader.LoadAll();

        catalog.Problems.Should().BeEmpty();
        catalog.Ships.Should().HaveCount(Directory.GetFiles(ShipLoader.DefaultFolder, "*.json").Length);
    }

    public static TheoryData<string> ShippedShipIds => [.. ShipLoader.ListShips(ShipLoader.DefaultFolder)];

    // Gate for community PRs: every ship file in ships/ must load; one case per file names the broken one.
    [Theory]
    [MemberData(nameof(ShippedShipIds))]
    public void Every_shipped_ship_loads(string shipId)
    {
        // Lowercase snake_case: no casing surprises between Windows and Linux file systems.
        shipId.Should().MatchRegex("^[a-z0-9]+(_[a-z0-9]+)*$");

        var ship = ShipLoader.Load(shipId);

        ship.Id.Should().Be(shipId);
    }

    // A rack's minimum (K3.3): the Hull C's spindle takes 32s only, the MPUV Tractor 16s, the CSV-SM 4s (Fleetyards limits.min).
    [Fact]
    public void Exactly_three_shipped_ships_have_a_minimum()
    {
        ShipLoader.LoadAll().Ships.Where(s => s.Racks.Any(r => r.MinCrateSize is not null)).Select(s => s.Id)
            .Should().Equal("csv_sm", "hull_c", "mpuv_tractor");
    }

    [Theory]
    [MemberData(nameof(ShippedShipIds))]
    public void A_shipped_minimum_is_one_the_rack_takes(string shipId)
    {
        foreach (var rack in ShipLoader.Load(shipId).Racks.Where(r => r.MinCrateSize is not null))
        {
            rack.CanHold(rack.MinCrateSize!.Value).Should().BeTrue($"rack {rack.Number} of {shipId} has minCrateSize {(int)rack.MinCrateSize}");
        }
    }

    [Fact]
    public void Load_reads_the_minimum_and_without_one_every_size_counts()
    {
        var rack = TestShips.Load("min_crate_size").Racks.Single();

        rack.MinCrateSize.Should().Be(CrateSize.Sixteen);
        (rack.CanHold(CrateSize.Eight), rack.CanHold(CrateSize.Sixteen)).Should().Be((false, true));
        TestShips.Load("max_crate_size").Racks.Should().OnlyContain(r => r.MinCrateSize == null && r.CanHold(CrateSize.One));
    }

    // Schema 5 marks maxCrateSize as a door limit (D70); the shipped files say so, schema 4 stays readable.
    [Theory]
    [MemberData(nameof(ShippedShipIds))]
    public void A_shipped_file_is_schema_5(string shipId)
    {
        File.ReadAllText(System.IO.Path.Combine(ShipLoader.DefaultFolder, shipId + ".json")).Should().Contain("\"schemaVersion\": 5,");
    }

    // maxCrateSize is a door smaller than the grid (D70): a value the grid enforces anyway says nothing and is left out.
    [Theory]
    [MemberData(nameof(ShippedShipIds))]
    public void A_shipped_max_crate_size_is_below_what_the_grid_takes(string shipId)
    {
        foreach (var rack in ShipLoader.Load(shipId).Racks.Where(r => r.MaxCrateSize is not null))
        {
            var gridTakes = (int)Enum.GetValues<CrateSize>().Where(rack.FitsGrid).Max();
            gridTakes.Should().BeGreaterThan((int)rack.MaxCrateSize!.Value, $"rack {rack.Number} of {shipId} has maxCrateSize {(int)rack.MaxCrateSize}");
        }
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

    // --- The ships folder (D56) ---

    [Fact]
    public void LoadAll_without_folder_throws_ShipFolderException_naming_the_folder()
    {
        using var folder = new ShipFolder();
        Directory.Delete(folder.Path);

        var act = () => ShipLoader.LoadAll(folder.Path);

        act.Should().Throw<ShipFolderException>()
            .WithMessage("Ships folder missing next to the exe – unzip the release again.*")
            .Which.Message.Should().Contain(folder.Path);
    }

    [Fact]
    public void Load_without_folder_throws_ShipFolderException()
    {
        using var folder = new ShipFolder();
        Directory.Delete(folder.Path);

        var act = () => ShipLoader.Load(folder.Path, "hull_a");

        act.Should().Throw<ShipFolderException>();
    }

    [Theory]
    [InlineData("malformed")]            // JSON error
    [InlineData("id_mismatch")]          // id ≠ file name
    [InlineData("unknown_stack_axis")]   // format invalid
    [InlineData("overlapping_racks")]    // racks overlap
    public void LoadAll_skips_a_broken_file_and_names_it(string shipId)
    {
        using var folder = new ShipFolder();
        folder.AddShipped("hull_a");
        folder.AddTestShip(shipId);

        var catalog = ShipLoader.LoadAll(folder.Path);

        catalog.Ships.Select(s => s.Id).Should().Equal("hull_a");
        var problem = catalog.Problems.Should().ContainSingle().Which;
        problem.ShipId.Should().Be(shipId);
        problem.Message.Should().Contain($"'{shipId}.json'");
    }

    [Fact]
    public void LoadAll_without_hull_a_throws_ShipFolderException()
    {
        using var folder = new ShipFolder();
        folder.AddTestShip("spike_uneven");

        var act = () => ShipLoader.LoadAll(folder.Path);

        act.Should().Throw<ShipFolderException>()
            .WithMessage("Ship file hull_a.json missing – unzip the release again.*");
    }

    [Fact]
    public void LoadAll_with_broken_hull_a_throws_ShipFolderException_with_the_reason()
    {
        using var folder = new ShipFolder();
        folder.Write("hull_a", "{");
        folder.AddTestShip("spike_uneven");

        var act = () => ShipLoader.LoadAll(folder.Path);

        act.Should().Throw<ShipFolderException>()
            .WithMessage("Ship file hull_a.json is invalid – unzip the release again.*'hull_a.json'*")
            .WithInnerException<ShipFormatException>();
    }

    // Ordinal: the same order on every machine and culture; "2" sorts before "_".
    [Fact]
    public void LoadAll_sorts_ships_by_id_ordinal()
    {
        using var folder = new ShipFolder();
        folder.AddShipped("hull_a");
        folder.AddRenamed("spike_uneven", "b_ship");
        folder.AddRenamed("spike_uneven", "a_ship");
        folder.AddRenamed("spike_uneven", "hull2");

        var catalog = ShipLoader.LoadAll(folder.Path);

        catalog.Ships.Select(s => s.Id).Should().Equal("a_ship", "b_ship", "hull2", "hull_a");
    }

    [Fact]
    public void LoadAll_ignores_files_that_are_not_json()
    {
        using var folder = new ShipFolder();
        folder.AddShipped("hull_a");
        File.WriteAllText(Path.Combine(folder.Path, "README.txt"), "not a ship");
        File.WriteAllText(Path.Combine(folder.Path, "hull_a.json.bak"), "{");

        var catalog = ShipLoader.LoadAll(folder.Path);

        catalog.Ships.Select(s => s.Id).Should().Equal("hull_a");
        catalog.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Catalog_Get_of_a_skipped_ship_throws_ShipNotFoundException_listing_loaded_ids()
    {
        using var folder = new ShipFolder();
        folder.AddShipped("hull_a");
        folder.AddTestShip("malformed");

        var act = () => ShipLoader.LoadAll(folder.Path).Get("malformed");

        act.Should().Throw<ShipNotFoundException>().Which.AvailableShipIds.Should().Equal("hull_a");
    }

    /// <summary>A ships folder of its own per test, deleted afterwards.</summary>
    private sealed class ShipFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("cargoplan-ships-").FullName;

        public void AddShipped(string shipId) =>
            File.Copy(System.IO.Path.Combine(ShipLoader.DefaultFolder, shipId + ".json"), FileOf(shipId));

        public void AddTestShip(string shipId) => File.Copy(TestShips.PathOf(shipId), FileOf(shipId));

        // A copy of a test ship under another id; the id inside must match the file name.
        public void AddRenamed(string shipId, string newId) =>
            Write(newId, File.ReadAllText(TestShips.PathOf(shipId)).Replace($"\"id\": \"{shipId}\"", $"\"id\": \"{newId}\""));

        public void Write(string shipId, string json) => File.WriteAllText(FileOf(shipId), json);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private string FileOf(string shipId) => System.IO.Path.Combine(Path, shipId + ".json");
    }

    // --- Test ships (TestShips/) ---

    [Theory]
    [InlineData("malformed")]               // not valid JSON
    [InlineData("sum_mismatch")]            // racks sum to 60, totalScu is 64
    [InlineData("zero_capacity_rack")]      // rack with 0 SCU
    [InlineData("unknown_property")]        // typo "capacitySCU"
    [InlineData("missing_property")]        // no "name"
    [InlineData("null_name")]               // "name": null
    [InlineData("id_mismatch")]             // "id" differs from file name
    [InlineData("unsupported_schema")]      // "schemaVersion": 6
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
    [InlineData("max_crate_size_invalid")]  // "maxCrateSize": 12
    [InlineData("min_crate_size_above_max")]   // "minCrateSize": 16, "maxCrateSize": 8
    [InlineData("min_crate_size_above_grid")]  // "minCrateSize": 16 on a 2x2x2 grid
    [InlineData("blank_manufacturer")]      // "manufacturer": " "
    public void Load_invalid_ship_throws_ShipFormatException_naming_the_file(string shipId)
    {
        var act = () => TestShips.Load(shipId);

        var ex = act.Should().Throw<ShipFormatException>().Which;
        ex.FileName.Should().Be($"{shipId}.json");
        ex.Message.Should().Contain($"'{shipId}.json'");
    }

    [Fact]
    public void Load_sum_mismatch_reports_the_rack_sum()
    {
        var act = () => TestShips.Load("sum_mismatch");

        act.Should().Throw<ShipFormatException>()
            .WithMessage("*60*64*")
            .WithInnerException<ArgumentException>();
    }

    [Fact]
    public void Load_unsupported_schema_names_the_version()
    {
        var act = () => TestShips.Load("unsupported_schema");

        act.Should().Throw<ShipFormatException>().WithMessage("*schemaVersion 6*expected 4 or 5*");
    }

    [Theory]
    [InlineData("previous_schema", "*schemaVersion 3*expected 4 or 5*")]
    [InlineData("old_schema", "*schemaVersion 2*expected 4 or 5*")]
    public void Load_old_schema_names_version_and_expected_version(string shipId, string expectedMessage)
    {
        var act = () => TestShips.Load(shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void Load_overlapping_racks_names_both_rack_numbers()
    {
        var act = () => TestShips.Load("overlapping_racks");

        act.Should().Throw<ShipFormatException>()
            .WithMessage("*Rack 2 ('rack-1')*[0, 1, 0]*Rack 4 ('rack-3')*[0, 1, 0]*overlap*")
            .WithInnerException<ArgumentException>();
    }

    [Theory]
    [InlineData("missing_origin", "*origin*")]              // required since schemaVersion 3
    [InlineData("origin_wrong_length", "*rack-0*origin*got 2*")]
    public void Load_bad_origin_names_the_field(string shipId, string expectedMessage)
    {
        var act = () => TestShips.Load(shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void Load_grid_capacity_mismatch_names_rack_and_numbers()
    {
        var act = () => TestShips.Load("grid_capacity_mismatch");

        act.Should().Throw<ShipFormatException>().WithMessage("*rack-0*4×2×2*16*12*");
    }

    [Fact]
    public void Load_zero_grid_axis_names_rack_and_axis()
    {
        var act = () => TestShips.Load("zero_grid_axis");

        act.Should().Throw<ShipFormatException>().WithMessage("*rack-0*width*");
    }

    [Fact]
    public void Load_unknown_side_names_rack_and_value()
    {
        var act = () => TestShips.Load("unknown_side");

        act.Should().Throw<ShipFormatException>().WithMessage("*rack-0*left*");
    }

    [Theory]
    [InlineData("missing_stack_axis", "*stackAxis*")]   // required since schemaVersion 4
    [InlineData("unknown_stack_axis", "*Rack 1 ('rack-0')*stackAxis 'up'*\"+x\"*")]
    public void Load_bad_stack_axis_names_the_field(string shipId, string expectedMessage)
    {
        var act = () => TestShips.Load(shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void Load_unknown_property_names_the_property()
    {
        var act = () => TestShips.Load("unknown_property");

        act.Should().Throw<ShipFormatException>().WithMessage("*capacitySCU*");
    }

    // --- Optional fields (D56): no schema bump, files without them load as before ---

    [Fact]
    public void Load_reads_max_crate_size_and_ship_metadata()
    {
        var ship = TestShips.Load("max_crate_size");

        ship.Racks.Select(r => r.MaxCrateSize).Should().Equal(CrateSize.Sixteen, null);
        (ship.Manufacturer, ship.FleetyardsSlug, ship.Source, ship.Notes)
            .Should().Be(("RSI", "test-ship", "Fleetyards", "Rack 1 takes crates up to 16 SCU."));
    }

    // Schema 5 (D70) marks maxCrateSize as a door limit; the field reads the same in both, so a schema 4 file needs no migration.
    [Fact]
    public void Schema_4_and_5_read_alike()
    {
        using var folder = new ShipFolder();
        folder.Write("max_crate_size", File.ReadAllText(TestShips.PathOf("max_crate_size")).Replace("\"schemaVersion\": 4", "\"schemaVersion\": 5"));

        var five = ShipLoader.Load(folder.Path, "max_crate_size");
        var four = TestShips.Load("max_crate_size");

        five.Racks.Should().Equal(four.Racks);
        five.Racks.Select(r => r.MaxCrateSize).Should().Equal(CrateSize.Sixteen, null);
    }

    // Ship drafts with a generated rack arrangement leave it out: a made-up "port lower" would show in the tooltip (T7.1b).
    [Fact]
    public void Load_reads_position_only_where_given()
    {
        var ship = TestShips.Load("max_crate_size");

        ship.Racks.Select(r => r.Position).Should().Equal(new RackPosition(RackSide.Port, RackLevel.Lower), null);
    }

    [Fact]
    public void Load_without_optional_fields_leaves_them_null()
    {
        var ship = TestShips.Load("spike_uneven");

        ship.Racks.Should().OnlyContain(r => r.MaxCrateSize == null);
        new[] { ship.Manufacturer, ship.FleetyardsSlug, ship.Source, ship.Notes }.Should().OnlyContain(v => v == null);
    }

    // The 4×2×2 grid takes no crate above 16 SCU anyway; a limit would only repeat it (ship-format.md).
    [Fact]
    public void Load_hull_a_has_its_metadata_and_no_max_crate_size()
    {
        var ship = ShipLoader.Load("hull_a");

        (ship.Manufacturer, ship.FleetyardsSlug, ship.Source).Should().Be(("MISC", "misc-hull-a", "in game"));
        ship.Racks.Should().OnlyContain(r => r.MaxCrateSize == null);
    }

    [Theory]
    [InlineData("max_crate_size_invalid", "*Rack 1 ('rack-0')*maxCrateSize 12*1, 2, 4, 8, 16, 24, 32*")]
    [InlineData("blank_manufacturer", "*manufacturer*")]
    public void Load_bad_optional_field_names_the_field(string shipId, string expectedMessage)
    {
        var act = () => TestShips.Load(shipId);

        act.Should().Throw<ShipFormatException>().WithMessage(expectedMessage);
    }

    // --- DTO mapping ---

    [Fact]
    public void RackDto_with_zero_capacity_throws_on_mapping()
    {
        var dto = new RackDto("rack-0", 0, new GridDto(4, 2, 2), [0, 0, 0], "+z", Position: new PositionDto("port", "upper"));

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
        var dto = new RackDto("rack-0", 16, new GridDto(4, 2, 2), [0, 0, 0], stackAxis, Position: new PositionDto("port", "upper"));

        dto.ToRack(0).StackAxis.Should().Be(expected);
    }

    [Theory]
    [InlineData("+Z")]   // case-sensitive like every other field
    [InlineData("z")]    // the sign is required
    [InlineData("4")]    // no numbers for enums
    public void RackDto_rejects_unknown_stack_axis(string stackAxis)
    {
        var dto = new RackDto("rack-0", 16, new GridDto(4, 2, 2), [0, 0, 0], stackAxis, Position: new PositionDto("port", "upper"));

        var act = () => dto.ToRack(0);

        act.Should().Throw<ArgumentException>().WithMessage($"*Rack 1 ('rack-0')*'{stackAxis}'*");
    }
}
