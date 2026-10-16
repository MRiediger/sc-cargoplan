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
    [InlineData("unsupported_schema")]          // "schemaVersion": 2
    [InlineData("malformed")]                   // not valid JSON
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
    [InlineData("unsupported_schema", "*schemaVersion 2*")]
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

        var stops = MissionFileReader.Read(stream, "bom.json");

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

            var stops = MissionFileReader.Read(path);

            stops.Should().BeEquivalentTo(MissionFixtures.ExampleMissions, o => o.WithStrictOrdering());
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

    // --- Serialization (export in T3.6 writes the same format) ---

    [Fact]
    public void Serialized_stops_read_back_unchanged()
    {
        var original = MissionFixtures.OverflowMissions;

        var json = JsonSerializer.SerializeToUtf8Bytes(
            MissionFileDto.FromStops(original), MissionJsonContext.Default.MissionFileDto);
        using var stream = new MemoryStream(json);
        var roundTripped = MissionFileReader.Read(stream, "roundtrip.json");

        roundTripped.Should().BeEquivalentTo(original, o => o.WithStrictOrdering());
    }

    [Fact]
    public void FromStops_writes_stops_in_route_order()
    {
        Stop[] stops =
        [
            new("Orison", 2, [new CargoItem("Agricium", 8, CrateSize.Eight)]),
            new("Riker", 1, [new CargoItem("Titanium", 12, CrateSize.Four)]),
        ];

        var dto = MissionFileDto.FromStops(stops);

        dto.SchemaVersion.Should().Be(MissionFileReader.SupportedSchemaVersion);
        dto.Stops.Select(s => s.Destination).Should().Equal("Riker", "Orison");
        dto.Stops[0].Items[0].Should().Be(new CargoItemDto("Titanium", 12, 4));
    }
}
