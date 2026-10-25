using System.Text;
using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class MissionFileReaderTests
{
    private const int HullAScu = 64;

    // --- Fixtures ---

    [Fact]
    public void ExampleMissions_has_three_stops_below_ship_capacity()
    {
        var stops = MissionFixtures.ExampleMissions;

        stops.Select(s => s.Destination).Should().Equal("Riker", "Shubin", "Orison");
        stops.Select(s => s.TotalScu).Should().Equal(12, 10, 8);
        stops.Sum(s => s.TotalScu).Should().BeLessThan(HullAScu);
    }

    [Fact]
    public void ExampleMissions_maps_items()
    {
        var riker = MissionFixtures.ExampleMissions[0];

        riker.Items.Should().ContainSingle()
            .Which.Should().Be(new CargoItem("Titanium", 12, CrateSize.Four));
    }

    [Fact]
    public void OverflowMissions_exceeds_ship_capacity()
    {
        MissionFixtures.OverflowMissions.Sum(s => s.TotalScu).Should().BeGreaterThan(HullAScu);
    }

    [Fact]
    public void SplitRequired_has_one_stop_of_20_scu_in_4_scu_crates()
    {
        var stop = MissionFixtures.SplitRequired.Should().ContainSingle().Which;

        stop.TotalScu.Should().Be(20);
        stop.Items.Should().AllSatisfy(i => i.CrateSize.Should().Be(CrateSize.Four));
    }

    // --- Schema 1 (still read, D23) ---

    [Fact]
    public void Read_v1_item_has_its_crate_size_as_max_and_its_crates_as_explicit_list()
    {
        var titanium = MissionFixtures.LoadMissions("example_missions")[0].Items.Should().ContainSingle().Which;

        titanium.MaxCrateSize.Should().Be(CrateSize.Four);
        titanium.Crates.Should().Equal(CrateSize.Four, CrateSize.Four, CrateSize.Four);
    }

    // --- Schema 2 (D23) ---

    [Fact]
    public void Read_v2_item_without_crates_derives_its_mix()
    {
        var greenGlade = MissionFixtures.CrateMixMissions[0];

        greenGlade.Items.Should().AllSatisfy(i => i.Crates.Should().BeNull());
        greenGlade.Items[0].CrateList.Should().Equal(CrateSize.Four, CrateSize.Two);
        greenGlade.Items[1].CrateList.Should().Equal(CrateSize.Four, CrateSize.Four, CrateSize.Four, CrateSize.Two, CrateSize.One);
    }

    [Fact]
    public void Read_v2_item_with_crates_keeps_the_override()
    {
        var titanium = MissionFixtures.CrateMixMissions[1].Items.Should().ContainSingle().Which;

        titanium.Should().BeEquivalentTo(new MissionItem("Titanium", 6, CrateSize.Four, [CrateSize.Two, CrateSize.Two, CrateSize.Two]));
    }

    [Fact]
    public void Read_v2_file_expands_into_one_cargo_item_per_crate_size()
    {
        var stops = MissionFixtures.Load("crate_mix");

        stops[0].Items.Should().Equal(
            new CargoItem("Stims", 4, CrateSize.Four),
            new CargoItem("Stims", 2, CrateSize.Two),
            new CargoItem("Scrap", 12, CrateSize.Four),
            new CargoItem("Scrap", 2, CrateSize.Two),
            new CargoItem("Scrap", 1, CrateSize.One));
        stops[1].Items.Should().Equal(new CargoItem("Titanium", 6, CrateSize.Two));
    }

    // --- Schema 3: fill order (D72) ---

    [Fact]
    public void Read_v3_file_gives_its_fill_order()
    {
        var file = MissionFixtures.LoadFile("fill_order_stern");

        file.FillOrder.Should().Be(FillOrder.SternFirst);
        file.Stops.Select(s => s.Destination).Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void Read_v3_file_without_fill_order_gives_none_and_the_same_stops()
    {
        var file = MissionFixtures.LoadFile("schema3_without_fill_order");

        file.FillOrder.Should().BeNull();
        file.Stops.Should().BeEquivalentTo(MissionFixtures.LoadFile("fill_order_stern").Stops, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData("example_missions")]   // schema 1
    [InlineData("crate_mix")]          // schema 2
    public void Read_v1_and_v2_files_give_no_fill_order(string fixture)
    {
        MissionFixtures.LoadFile(fixture).FillOrder.Should().BeNull();
    }

    [Fact]
    public void Read_assigns_order_from_array_position_starting_at_1()
    {
        MissionFixtures.ExampleMissions.Select(s => s.Order).Should().Equal(1, 2, 3);
    }

    // --- Invalid files ---

    [Theory]
    [InlineData("empty_stops")]                 // "stops": []
    [InlineData("stop_without_items")]          // "items": []
    [InlineData("unknown_property")]            // typo "SCU"
    [InlineData("blank_destination")]           // "destination": "  "
    [InlineData("duplicate_destination")]       // "Riker" and "riker"
    [InlineData("invalid_crate_size")]          // crate size 3
    [InlineData("scu_not_multiple_of_crate")]   // 20 SCU in 8 SCU crates
    [InlineData("blank_commodity")]             // "commodity": ""
    [InlineData("missing_property")]            // no "crateSize"
    [InlineData("missing_schema")]              // no "schemaVersion"
    [InlineData("unsupported_schema")]          // "schemaVersion": 4
    [InlineData("malformed")]                   // not valid JSON
    [InlineData("crates_sum_mismatch")]         // v2: 6 SCU as 2+2
    [InlineData("crate_above_max")]             // v2: an 8 SCU crate at max 4
    [InlineData("crate_unknown_size")]          // v2: 3 SCU crates
    [InlineData("crate_size_in_v2")]            // v2 with the v1 field "crateSize"
    [InlineData("fill_order_in_v2")]            // v2 with the v3 field "fillOrder"
    [InlineData("unknown_fill_order")]          // v3: "fillOrder": "portFirst"
    public void Read_invalid_file_throws_MissionFormatException_naming_the_source(string name)
    {
        var resourceName = MissionFixtures.InvalidResourceName(name);
        using var stream = MissionFixtures.Open(resourceName);

        var act = () => MissionFileReader.Read(stream, resourceName);

        var ex = act.Should().Throw<MissionFormatException>().Which;
        ex.SourceName.Should().Be(resourceName);
        ex.Message.Should().Contain(resourceName);
    }

    [Theory]
    [InlineData("stop_without_items", "*Riker*no items*")]
    [InlineData("unknown_property", "*SCU*")]
    [InlineData("duplicate_destination", "*riker*more than once*")]
    [InlineData("scu_not_multiple_of_crate", "*Riker*Titanium*20 SCU*8 SCU crates*")]
    [InlineData("unsupported_schema", "*schemaVersion 4*expected 1 or 2 or 3*")]
    [InlineData("crates_sum_mismatch", "*Riker*Titanium*add up to 4 SCU, not 6*")]
    [InlineData("crate_above_max", "*Riker*Titanium*8 SCU crates are larger than the max crate size of 4 SCU*")]
    [InlineData("crate_unknown_size", "*Riker*Titanium*3 SCU is not a crate size*")]
    [InlineData("crate_size_in_v2", "*crateSize*")]
    [InlineData("fill_order_in_v2", "*fillOrder*new in schema 3*the file says 2*")]
    [InlineData("unknown_fill_order", "*fillOrder 'portFirst' is unknown*bowFirst*sternFirst*")]
    public void Read_invalid_file_explains_the_problem(string name, string expectedMessage)
    {
        var resourceName = MissionFixtures.InvalidResourceName(name);
        using var stream = MissionFixtures.Open(resourceName);

        var act = () => MissionFileReader.Read(stream, resourceName);

        act.Should().Throw<MissionFormatException>().WithMessage(expectedMessage);
    }

    // --- Stream and path ---

    [Fact]
    public void Read_accepts_utf8_byte_order_mark()
    {
        // Notepad and Windows PowerShell 5.1 (Set-Content -Encoding UTF8) write a BOM.
        const string json = """{ "schemaVersion": 1, "stops": [ { "destination": "Riker", "items": [ { "commodity": "Titanium", "scu": 4, "crateSize": 4 } ] } ] }""";
        using var stream = new MemoryStream([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(json)]);

        var stops = MissionFileReader.Read(stream, "bom.json").Stops;

        stops.Should().ContainSingle().Which.Destination.Should().Be("Riker");
    }

    [Fact]
    public void Read_path_reads_the_same_as_stream()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            using (var source = MissionFixtures.Open(MissionFixtures.ResourceName("example_missions")))
            using (var target = File.Create(path))
            {
                source.CopyTo(target);
            }

            var stops = MissionFileReader.Read(path).Stops;

            stops.Should().BeEquivalentTo(MissionFixtures.LoadMissions("example_missions"), o => o.WithStrictOrdering());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_path_names_the_file_in_format_errors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, """{ "schemaVersion": 1, "stops": [] }""");

            var act = () => MissionFileReader.Read(path);

            act.Should().Throw<MissionFormatException>().Which.SourceName.Should().Be(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_missing_path_throws_FileNotFoundException()
    {
        var act = () => MissionFileReader.Read(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        act.Should().Throw<FileNotFoundException>();
    }

    // --- Serialization (export in T3.8 writes the same format) ---

    [Theory]
    [InlineData("overflow_missions")]
    [InlineData("crate_mix")]
    public void Serialized_stops_read_back_unchanged(string fixture)
    {
        var original = MissionFixtures.LoadMissions(fixture);

        var json = JsonSerializer.SerializeToUtf8Bytes(
            MissionFileDto.FromMissionStops(original, FillOrder.BowFirst), MissionJsonContext.Default.MissionFileDto);
        using var stream = new MemoryStream(json);
        var roundTripped = MissionFileReader.Read(stream, "roundtrip.json").Stops;

        roundTripped.Should().BeEquivalentTo(original, o => o.WithStrictOrdering());
    }

    [Fact]
    public void FromStops_writes_stops_in_route_order()
    {
        MissionStop[] stops =
        [
            new("Orison", 2, [new MissionItem("Agricium", 8, CrateSize.Eight)]),
            new("Riker", 1, [new MissionItem("Titanium", 12, CrateSize.Four)]),
        ];

        var dto = MissionFileDto.FromMissionStops(stops, FillOrder.SternFirst);

        dto.SchemaVersion.Should().Be(MissionFileReader.CurrentSchemaVersion);
        dto.FillOrder.Should().Be("sternFirst");
        dto.Stops.Select(s => s.Destination).Should().Equal("Riker", "Orison");
        dto.Stops[0].Items[0].Should().Be(new MissionItemDto("Titanium", 12, 4));
    }
}
