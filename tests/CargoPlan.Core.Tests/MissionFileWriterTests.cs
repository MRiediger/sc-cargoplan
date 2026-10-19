using System.Text;
using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class MissionFileWriterTests
{
    private static IReadOnlyList<Stop> RoundTrip(IReadOnlyList<Stop> stops)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, stops);
        stream.Position = 0;
        return MissionFileReader.Read(stream, "roundtrip");
    }

    [Theory]
    [InlineData("example_missions")]
    [InlineData("overflow_missions")]
    [InlineData("split_required")]
    [InlineData("split_and_shared")]
    public void Write_then_Read_gives_the_same_stops(string fixture)
    {
        var stops = MissionFixtures.Load(fixture);

        // Stop holds its items in a list, which compares by reference; equivalence compares the content.
        RoundTrip(stops).Should().BeEquivalentTo(stops, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Write_keeps_route_order_even_if_the_list_is_not_sorted()
    {
        Stop riker = new("Riker", 1, [new CargoItem("Titanium", 4, CrateSize.Four)]);
        Stop shubin = new("Shubin", 2, [new CargoItem("Quartz", 2, CrateSize.Two)]);

        RoundTrip([shubin, riker]).Select(s => s.Destination).Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void Write_writes_schema_version_1_and_leaves_the_stream_open()
    {
        using var stream = new MemoryStream();

        MissionFileWriter.Write(stream, MissionFixtures.ExampleMissions);

        stream.CanRead.Should().BeTrue();
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        document.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(MissionFileReader.SupportedSchemaVersion);
    }

    [Fact]
    public void Write_without_stops_throws_because_the_file_could_not_be_read_back()
    {
        using var stream = new MemoryStream();

        var act = () => MissionFileWriter.Write(stream, []);

        act.Should().Throw<ArgumentException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public void Write_with_a_duplicate_destination_throws_because_the_file_could_not_be_read_back()
    {
        Stop riker = new("Riker", 1, [new CargoItem("Titanium", 4, CrateSize.Four)]);
        Stop rikerAgain = new("riker", 2, [new CargoItem("Quartz", 2, CrateSize.Two)]);
        using var stream = new MemoryStream();

        var act = () => MissionFileWriter.Write(stream, [riker, rikerAgain]);

        act.Should().Throw<ArgumentException>().WithMessage("*riker*");
        stream.Length.Should().Be(0);
    }
}
