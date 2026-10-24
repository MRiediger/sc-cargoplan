using System.Text;
using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

/// <summary>Pickup locations on contract items (D68): a list without amounts, the same normalization for every entry point.</summary>
public class PickupTests
{
    // --- Pickups: one rule for the editor, the mission file and ContractTracker ---

    [Theory]
    [InlineData("Lorville", new[] { "Lorville" })]
    [InlineData(" A / B / a ", new[] { "A", "B" })]
    [InlineData("A//B/", new[] { "A", "B" })]
    [InlineData("Everus Harbor DROP OFF LOCATIONS (ANY ORDER)", new[] { "Everus Harbor DROP OFF LOCATIONS (ANY ORDER)" })]
    public void Parse_splits_at_the_slash_trims_and_drops_blanks_and_repeats(string text, string[] expected)
    {
        Pickups.Parse(text).Should().Equal(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" / ")]
    public void Parse_of_nothing_is_null(string? text)
    {
        Pickups.Parse(text).Should().BeNull();
    }

    [Fact]
    public void Normalized_keeps_the_first_spelling_and_the_order()
    {
        Pickups.Normalized(["  Baijini Point", "Lorville", "baijini point"]).Should().Equal("Baijini Point", "Lorville");
        Pickups.Normalized([" ", ""]).Should().BeNull();
        Pickups.Normalized(null).Should().BeNull();
    }

    [Fact]
    public void Text_joins_with_a_slash_and_is_empty_without_pickup()
    {
        Pickups.Text(["A", "B", "C"]).Should().Be("A / B / C");
        Pickups.Text(null).Should().BeEmpty();
    }

    // --- MissionItem ---

    [Fact]
    public void An_item_without_pickup_has_none()
    {
        new MissionItem("Stims", 4, CrateSize.Four).Pickup.Should().BeNull();
        new MissionItem("Stims", 4, CrateSize.Four, Pickup: []).Pickup.Should().BeNull();
    }

    /// <param name="pickup">The places separated by "|", which no test place contains.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" Lorville")]
    [InlineData("Lorville|lorville")]
    public void An_item_refuses_a_pickup_list_that_is_not_normalized(string pickup)
    {
        var act = () => new MissionItem("Stims", 4, CrateSize.Four, Pickup: pickup.Split('|'));

        act.Should().Throw<ArgumentException>().WithParameterName("Pickup");
    }

    [Fact]
    public void WithCrateList_keeps_the_pickup()
    {
        MissionItem stims = new("Stims", 4, CrateSize.Four, Pickup: ["Lorville"]);

        stims.WithCrateList([CrateSize.Two, CrateSize.Two]).Pickup.Should().Equal("Lorville");
    }

    // --- Mission: the item index a crate names ---

    [Fact]
    public void Items_lists_every_item_with_its_stop_in_list_order()
    {
        Mission mission = new(1,
        [
            new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four), new MissionItem("Quartz", 2, CrateSize.Two)]),
            new("Shubin", 2, [new MissionItem("Gold", 8, CrateSize.Eight)]),
        ]);

        mission.Items.Select(i => (i.Stop.Destination, i.Item.Commodity))
            .Should().Equal(("Riker", "Titanium"), ("Riker", "Quartz"), ("Shubin", "Gold"));
    }

    [Fact]
    public void WithItem_replaces_one_item_and_keeps_the_rest()
    {
        Mission mission = new(3,
        [
            new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]),
            new("Shubin", 2, [new MissionItem("Gold", 8, CrateSize.Eight)]),
        ], new ContractSource([7]));

        var changed = mission.WithItem(1, new MissionItem("Gold", 8, CrateSize.Eight, Pickup: ["Lorville"]));

        changed.Items[1].Item.Pickup.Should().Equal("Lorville");
        changed.Items[0].Item.Should().BeSameAs(mission.Items[0].Item);
        (changed.Id, changed.Contract).Should().Be((3, mission.Contract));
    }

    // --- Mission file: optional, schema 2 stays (D68) ---

    private static IReadOnlyList<MissionStop> RoundTrip(IReadOnlyList<MissionStop> stops)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, stops);
        stream.Position = 0;
        return MissionFileReader.Read(stream, "roundtrip");
    }

    private static string Written(IReadOnlyList<MissionStop> stops)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, stops);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IReadOnlyList<MissionStop> Read(string json) =>
        MissionFileReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "missions.json");

    [Fact]
    public void Write_then_Read_keeps_the_pickup()
    {
        MissionStop everus = new("Everus Harbor", 1, [new MissionItem("Stims", 7, CrateSize.Four, Pickup: ["A", "B", "C"])]);

        RoundTrip([everus])[0].Items[0].Pickup.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Write_leaves_out_pickup_for_an_item_without_one()
    {
        MissionStop riker = new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]);

        using var document = JsonDocument.Parse(Written([riker]));
        document.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        document.RootElement.GetProperty("stops")[0].GetProperty("items")[0].TryGetProperty("pickup", out _).Should().BeFalse();
    }

    [Fact]
    public void Read_normalizes_the_pickup_list()
    {
        var stops = Read("""
            { "schemaVersion": 2, "stops": [ { "destination": "Riker", "items": [
              { "commodity": "Titanium", "scu": 4, "maxCrateSize": 4, "pickup": [" Lorville ", "", "lorville", "Area18"] } ] } ] }
            """);

        stops[0].Items[0].Pickup.Should().Equal("Lorville", "Area18");
    }

    [Fact]
    public void Read_of_an_empty_pickup_list_is_no_pickup()
    {
        var stops = Read("""
            { "schemaVersion": 2, "stops": [ { "destination": "Riker", "items": [
              { "commodity": "Titanium", "scu": 4, "maxCrateSize": 4, "pickup": [] } ] } ] }
            """);

        stops[0].Items[0].Pickup.Should().BeNull();
    }
}
