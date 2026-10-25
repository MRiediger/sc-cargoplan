using System.Text;
using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class MissionFileWriterTests
{
    private static MissionFile RoundTrip(IReadOnlyList<MissionStop> stops, FillOrder fillOrder = FillOrder.BowFirst)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, stops, fillOrder);
        stream.Position = 0;
        return MissionFileReader.Read(stream, "roundtrip");
    }

    private static string WrittenText(IReadOnlyList<MissionStop> stops, FillOrder fillOrder = FillOrder.BowFirst)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, stops, fillOrder);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonDocument Written(IReadOnlyList<MissionStop> stops, FillOrder fillOrder = FillOrder.BowFirst) =>
        JsonDocument.Parse(WrittenText(stops, fillOrder));

    [Theory]
    [InlineData("example_missions")]
    [InlineData("overflow_missions")]
    [InlineData("split_required")]
    [InlineData("split_and_shared")]
    [InlineData("crate_mix")]
    [InlineData("fill_order_stern")]
    public void Write_then_Read_gives_the_same_stops(string fixture)
    {
        // Schema 1 fixtures come back as schema 3 with their explicit crates, schema 2 and 3 as they were (D23).
        var stops = MissionFixtures.LoadMissions(fixture);

        // MissionStop holds its items in a list, which compares by reference; equivalence compares the content.
        RoundTrip(stops).Stops.Should().BeEquivalentTo(stops, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Write_keeps_route_order_even_if_the_list_is_not_sorted()
    {
        MissionStop riker = new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]);
        MissionStop shubin = new("Shubin", 2, [new MissionItem("Quartz", 2, CrateSize.Two)]);

        RoundTrip([shubin, riker]).Stops.Select(s => s.Destination).Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void Write_writes_schema_version_3_even_for_a_v1_file_and_leaves_the_stream_open()
    {
        using var stream = new MemoryStream();

        MissionFileWriter.Write(stream, MissionFixtures.LoadMissions("example_missions"), FillOrder.BowFirst);

        stream.CanRead.Should().BeTrue();
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        document.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(3);
        MissionFileReader.CurrentSchemaVersion.Should().Be(3);
    }

    // --- Fill order (D72) ---

    [Theory]
    [InlineData(FillOrder.BowFirst, "bowFirst")]
    [InlineData(FillOrder.SternFirst, "sternFirst")]
    public void Write_always_writes_the_fill_order_right_after_the_schema_version(FillOrder fillOrder, string expected)
    {
        using var document = Written(MissionFixtures.CrateMixMissions, fillOrder);

        document.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("schemaVersion", "fillOrder", "stops");
        document.RootElement.GetProperty("fillOrder").GetString().Should().Be(expected);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Write_then_Read_keeps_the_fill_order(FillOrder fillOrder)
    {
        RoundTrip(MissionFixtures.CrateMixMissions, fillOrder).FillOrder.Should().Be(fillOrder);
    }

    [Fact]
    public void Read_then_Write_gives_a_schema_3_file_back_byte_for_byte()
    {
        var file = MissionFixtures.LoadFile("fill_order_stern");

        var written = WrittenText(file.Stops, file.FillOrder!.Value);

        written.ReplaceLineEndings("\n").Should().Be(MissionFixtures.Text("fill_order_stern"));
    }

    // --- Crates and broken input ---

    [Fact]
    public void Write_writes_crates_only_for_an_overridden_item()
    {
        using var document = Written(MissionFixtures.CrateMixMissions);

        var items = document.RootElement.GetProperty("stops").EnumerateArray()
            .SelectMany(s => s.GetProperty("items").EnumerateArray()).ToList();
        items.Select(i => i.TryGetProperty("crates", out _)).Should().Equal(false, false, true);
        items[2].GetProperty("crates").EnumerateArray().Select(c => c.GetInt32()).Should().Equal(2, 2, 2);
        items[0].GetProperty("maxCrateSize").GetInt32().Should().Be(4);
    }

    [Fact]
    public void Write_without_stops_throws_because_the_file_could_not_be_read_back()
    {
        using var stream = new MemoryStream();

        var act = () => MissionFileWriter.Write(stream, [], FillOrder.BowFirst);

        act.Should().Throw<ArgumentException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public void Write_with_a_duplicate_destination_throws_because_the_file_could_not_be_read_back()
    {
        MissionStop riker = new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]);
        MissionStop rikerAgain = new("riker", 2, [new MissionItem("Quartz", 2, CrateSize.Two)]);
        using var stream = new MemoryStream();

        var act = () => MissionFileWriter.Write(stream, [riker, rikerAgain], FillOrder.BowFirst);

        act.Should().Throw<ArgumentException>().WithMessage("*riker*");
        stream.Length.Should().Be(0);
    }
}
