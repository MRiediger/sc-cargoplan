using System.Text;
using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>routes.json schema 2 (T13.3, K1; schema 1 of T13.1 read), docs/routes-format.md: observations only, strict like plan.json.</summary>
public class RoutesFileTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Observations_survive_a_round_trip()
    {
        var covalex = new ObservationKey(HaulContractor.Covalex, HaulRank.Experienced, HaulSize.Medium, Direct: true, Reward: 160_250, ["Pressurized Ice"]);
        var redWind = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Xs, Direct: null, Reward: 532_500, ["Carbon"]);
        var observations = Observations.Empty
            .Record(ObservationsTests.Scan(covalex, Monday, ("HUR-L3", "Pressurized Ice", 109)))
            .Record(ObservationsTests.Scan(covalex, Monday.AddDays(2), ("HUR-L3", "Pressurized Ice", 142)))
            .Record(ObservationsTests.Scan(redWind, Monday, ("Ruin Station", "Carbon", 200)) with { Area = AreaLevel.Stellar });

        var read = RoundTrip(observations);

        read.Entries.Should().BeEquivalentTo(observations.Entries, options => options.WithStrictOrdering());
        read.Entries.Select(e => e.FirstUtc.Kind).Should().AllBeEquivalentTo(DateTimeKind.Utc);
    }

    [Fact]
    public void No_observations_round_trip_as_an_empty_list()
    {
        Write(Observations.Empty).Should().Contain("\"observations\": []");
        RoundTrip(Observations.Empty).Entries.Should().BeEmpty();
    }

    [Fact]
    public void The_file_spells_the_words_of_the_hauling_data_format()
    {
        // docs/hauling-data-format.md: contractor covalex | redWind, rank trainee…master, size xs…bulk.
        var key = new ObservationKey(HaulContractor.RedWind, HaulRank.Trainee, HaulSize.Xs, Direct: null, Reward: 41_750, ["Waste"]);
        var json = Write(Observations.Empty.Record(ObservationsTests.Scan(key, Monday, ("Ruin Station", "Waste", 4)) with { Area = AreaLevel.Stellar }));

        json.Should().Contain("\"schemaVersion\": 2")
            .And.Contain("\"contractor\": \"redWind\"")
            .And.Contain("\"rank\": \"trainee\"")
            .And.Contain("\"size\": \"xs\"")
            .And.Contain("\"area\": \"stellar\"")
            .And.NotContain("\"direct\"", "an unknown direct is left out, as plan.json leaves out what isn't there");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 3, "observations": [] }""", "schemaVersion 3")]
    [InlineData("""{ "observations": [] }""", "schemaVersion")]
    [InlineData("""{ "schemaVersion": 1 }""", "observations")]
    [InlineData("""{ "schemaVersion": 1, "observations": [], "route": {} }""", "route")]
    [InlineData("not json", "invalid")]
    [InlineData("""{ "schemaVersion": 1, "observations": [ { "contractor": "crusader", "rank": "senior", "size": "medium", "reward": 1, "pickups": [], "destinations": [], "scu": [], "count": 1, "firstUtc": "2026-11-23T10:00:00Z", "lastUtc": "2026-11-23T10:00:00Z" } ] }""", "crusader")]
    [InlineData("""{ "schemaVersion": 1, "observations": [ { "contractor": "covalex", "rank": "senior", "size": "medium", "reward": 1, "pickups": [], "destinations": [], "scu": [ { "commodity": "Tin", "min": 9, "max": 3 } ], "count": 1, "firstUtc": "2026-11-23T10:00:00Z", "lastUtc": "2026-11-23T10:00:00Z" } ] }""", "Tin")]
    [InlineData("""{ "schemaVersion": 1, "observations": [ { "contractor": "covalex", "rank": "senior", "size": "medium", "reward": 1, "pickups": [], "destinations": [], "scu": [], "count": 0, "firstUtc": "2026-11-23T10:00:00Z", "lastUtc": "2026-11-23T10:00:00Z" } ] }""", "count")]
    public void A_file_not_in_the_format_is_refused_with_its_reason(string json, string reason)
    {
        var read = () => RoutesFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "routes.json");

        read.Should().Throw<RoutesFormatException>().WithMessage($"*routes.json*{reason}*");
    }

    [Fact]
    public void Two_entries_of_one_key_are_refused()
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, Direct: false, Reward: 325_250, ["Waste"]);
        var json = Write(Observations.Empty.Record(ObservationsTests.Scan(key, Monday, ("HUR-L3", "Waste", 113))));
        // The one entry: from the first brace in the observations list to the list's end.
        var entry = json[json.IndexOf('{', json.IndexOf('['))..json.LastIndexOf(']')].Trim();
        var twice = json.Replace(entry, $"{entry}, {entry}", StringComparison.Ordinal);

        var read = () => RoutesFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(twice)), "routes.json");

        read.Should().Throw<RoutesFormatException>().WithMessage("*twice*");
    }

    [Fact]
    public void Schema_1_keeps_entries_of_one_commodity_and_leaves_out_the_others_by_name()
    {
        // K1 of the T13.3 approval: schema 1 keyed without commodities; an entry of several may be several templates.
        const string json = """
            { "schemaVersion": 1, "observations": [
              { "contractor": "covalex", "rank": "rookie", "size": "medium", "direct": true, "reward": 50500, "pickups": [ "Everus Harbor" ],
                "destinations": [ "Green Glade Station" ], "scu": [ { "commodity": "Iron (Ore)", "min": 94, "max": 94 } ], "count": 1,
                "firstUtc": "2026-11-23T10:00:00Z", "lastUtc": "2026-11-23T10:00:00Z" },
              { "contractor": "covalex", "rank": "senior", "size": "medium", "direct": false, "reward": 325250, "pickups": [ "Everus Harbor" ],
                "destinations": [ "Green Glade Station" ], "scu": [ { "commodity": "Quantum Fuel", "min": 90, "max": 90 },
                { "commodity": "Waste", "min": 3, "max": 3 } ], "count": 2,
                "firstUtc": "2026-11-23T10:00:00Z", "lastUtc": "2026-11-23T10:00:00Z" } ] }
            """;

        var read = Read(json);

        read.Observations.Entries.Should().ContainSingle().Which.Key.Should().Be(
            new ObservationKey(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Medium, Direct: true, Reward: 50_500, ["Iron (Ore)"]));
        read.Dropped.Should().ContainSingle().Which.Should().Contain("covalex senior medium 325250").And.Contain("2 commodities");
        Write(read.Observations).Should().Contain("\"schemaVersion\": 2", "the next save writes schema 2");
    }

    [Fact]
    public void Schema_2_keeps_an_entry_of_several_commodities()
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, Direct: false, Reward: 325_250, ["Quantum Fuel", "Waste"]);
        var observations = Observations.Empty.Record(ObservationsTests.Scan(key, Monday, ("HUR-L3", "Quantum Fuel", 90), ("HUR-L3", "Waste", 3)));

        var read = Read(Write(observations));

        read.Observations.Entries.Should().ContainSingle().Which.Key.Should().Be(key);
        read.Dropped.Should().BeEmpty();
    }

    [Fact]
    public void Reading_folds_the_names_of_one_place()
    {
        const string json = """
            { "schemaVersion": 2, "observations": [
              { "contractor": "covalex", "rank": "rookie", "size": "medium", "direct": true, "reward": 50500, "pickups": [ "Everus Harbor" ],
                "destinations": [ "Green Glade", "Green Glade Station" ], "scu": [ { "commodity": "Iron (Ore)", "min": 94, "max": 94 } ],
                "count": 2, "firstUtc": "2026-11-23T10:00:00Z", "lastUtc": "2026-11-23T10:00:00Z" } ] }
            """;

        Read(json).Observations.Entries.Single().Destinations.Should().Equal("Green Glade Station");
    }

    private static RoutesRead Read(string json) => RoutesFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "routes.json");

    private static string Write(Observations observations)
    {
        using var stream = new MemoryStream();
        RoutesFile.Write(stream, observations);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Observations RoundTrip(Observations observations) =>
        Read(Write(observations)).Observations;
}
