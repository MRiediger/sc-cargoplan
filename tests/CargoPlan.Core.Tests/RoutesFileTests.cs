using System.Text;
using System.Text.RegularExpressions;
using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>routes.json schema 3 (T13.6: route and runs; schema 2 of T13.3 and 1 of T13.1 read), docs/routes-format.md, strict like plan.json.</summary>
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

        json.Should().Contain("\"schemaVersion\": 3")
            .And.Contain("\"contractor\": \"redWind\"")
            .And.Contain("\"rank\": \"trainee\"")
            .And.Contain("\"size\": \"xs\"")
            .And.Contain("\"area\": \"stellar\"")
            .And.NotContain("\"direct\"", "an unknown direct is left out, as plan.json leaves out what isn't there");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 4, "observations": [] }""", "schemaVersion 4")]
    [InlineData("""{ "observations": [] }""", "schemaVersion")]
    [InlineData("""{ "schemaVersion": 1 }""", "observations")]
    [InlineData("""{ "schemaVersion": 2, "observations": [], "runs": [] }""", "schema 3")]
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
        Write(read.Observations).Should().Contain("\"schemaVersion\": 3", "the next save writes the current schema");
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

    [Fact]
    public void A_route_and_its_runs_survive_a_round_trip_beside_the_observations()
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, Direct: false, Reward: 325_250, ["Waste"]);
        var observations = Observations.Empty.Record(ObservationsTests.Scan(key, Monday, ("HUR-L3", "Waste", 113)));
        var route = SomeRoute() with { DepartedUtc = Monday.AddMinutes(12), At = 2 };
        RouteRun[] runs = [SomeRun(), SomeRun() with { Name = "Baijini ring", DepartedUtc = null }];

        var read = Read(Write(observations, route, runs));

        read.Observations.Entries.Should().BeEquivalentTo(observations.Entries);
        read.Route.Should().BeEquivalentTo(route, options => options.WithStrictOrdering());
        read.Route!.StartedUtc.Kind.Should().Be(DateTimeKind.Utc);
        read.Runs.Should().BeEquivalentTo(runs, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Without_a_route_and_runs_the_file_names_neither()
    {
        var json = Write(Observations.Empty);

        json.Should().NotContain("\"route\"").And.NotContain("\"runs\"");
        (Read(json).Route, Read(json).Runs.Count).Should().Be((null, 0));
    }

    [Fact]
    public void The_route_spells_the_words_of_the_hauling_data_and_crates_as_numbers()
    {
        var json = Write(Observations.Empty, SomeRoute(), [SomeRun()]);

        json.Should().Contain("\"kind\": \"localRing\"")
            .And.Contain("\"system\": \"stanton\"")
            .And.Contain("\"kind\": \"bulk\"")
            .And.Contain("\"kind\": \"return\"")
            .And.Contain("\"container\": 16")
            .And.Contain("\"filledBy\": \"EVERUSHARBOR | HUR-L1:WASTE | 16 | 325250\"")
            .And.Contain("\"template\": \"t-bulk\"");
    }

    [Theory]
    [InlineData("\"kind\": \"localRing\"", "\"kind\": \"loop\"", "loop")]
    [InlineData("\"system\": \"stanton\"", "\"system\": \"hurston\"", "hurston")]
    [InlineData("\"min\": 41", "\"min\": 90", "90")]
    [InlineData("\"container\": 16", "\"container\": 3", "container 3")]
    [InlineData("\"id\": 2", "\"id\": 1", "twice")]
    [InlineData("\"at\": 0", "\"at\": 9", "at 9")]
    [InlineData("\"name\": \"Everus ring\"", "\"name\": \"Everus ring\", \"colour\": \"red\"", "colour")]
    [InlineData("\"acceptAt\": \"HUR-L1 Green Glade Station\",", "", "accept")]
    public void A_route_not_in_the_format_is_refused_with_its_reason(string from, string to, string reason)
    {
        var json = Write(Observations.Empty, SomeRoute(), []);
        json.Should().Contain(from, "the test changes the file there");

        var read = () => Read(ReplaceFirst(json, from, to));

        read.Should().Throw<RoutesFormatException>().WithMessage($"*{reason}*");
    }

    [Fact]
    public void A_contract_accepted_on_the_way_keeps_its_station_and_a_custom_route_its_kind()
    {
        // T13.7b (D125 addendum): acceptAt for any role accepted at a station, collectAt only for a way back; additive in
        // schema 3.
        var onTheWay = new Placeholder(3, RecipeKind.Direct, "t-way", "Junior Rank - Direct Medium Cargo Haul", 87_250,
            [new HaulLeg("Stims", 60, 91, Models.CrateSize.Eight)], ["Port Tressler"], AcceptAt: "Baijini Point");
        var route = SomeRoute() with { Kind = LoopKind.Custom, Placeholders = [.. SomeRoute().Placeholders, onTheWay] };

        var json = Write(Observations.Empty, route, [SomeRun() with { Kind = LoopKind.Custom }]);
        var read = Read(json);

        json.Should().Contain("\"kind\": \"custom\"").And.Contain("\"schemaVersion\": 3");
        read.Route!.Kind.Should().Be(LoopKind.Custom);
        read.Route.Placeholders[2].Should().BeEquivalentTo(onTheWay);
        read.Runs.Single().Kind.Should().Be(LoopKind.Custom);
    }

    [Fact]
    public void Only_a_way_back_collects()
    {
        var json = Write(Observations.Empty, SomeRoute(), []);
        var bulkWithCollect = ReplaceFirst(json, "\"filledBy\": \"EVERUSHARBOR | HUR-L1:WASTE | 16 | 325250\"",
            "\"acceptAt\": \"HUR-L4 Melodic Fields Station\", \"collectAt\": [], \"filledBy\": \"EVERUSHARBOR | HUR-L1:WASTE | 16 | 325250\"");

        var read = () => Read(bulkWithCollect);

        read.Should().Throw<RoutesFormatException>().WithMessage("*collect*");
    }

    [Fact]
    public void A_run_names_its_ship_and_stations_and_one_written_without_them_is_still_read()
    {
        // The loop's identity for measured times (T13.6c): written always; a run of the branch before (gate 6b) has none.
        var json = Write(Observations.Empty, null, [SomeRun()]);
        json.Should().Contain("\"ship\": \"hull_b\"").And.Contain("\"stations\": [");

        var older = json.Replace("\"ship\": \"hull_b\",", "", StringComparison.Ordinal);
        older = older.Remove(older.IndexOf("\"stations\"", StringComparison.Ordinal), older.IndexOf(']', older.IndexOf("\"stations\"", StringComparison.Ordinal)) + 2 - older.IndexOf("\"stations\"", StringComparison.Ordinal));

        var run = Read(older).Runs.Single();
        (run.ShipId, run.Stations).Should().Be((null, null));
    }

    [Fact]
    public void A_direct_run_keeps_rank_size_and_reward_of_its_contract_and_a_ring_run_names_none()
    {
        // The key of a direct loop (T13.6c, P1): its outbound contract's rank, size and reward; "reward" is the run's sum
        // already, so they come in an object of their own, and an unknown reward is left out.
        var direct = SomeRun() with
        {
            Name = "Everus Harbor → HUR-L1 · Senior Direct Small",
            Kind = LoopKind.Direct,
            Landings = 2,
            Stations = ["HUR-L1 Green Glade Station"],
            Direct = new DirectContract(HaulRank.Senior, HaulSize.Small, 90_000),
        };
        var unknown = direct with { Direct = new DirectContract(HaulRank.Member, HaulSize.Xs, null) };

        var json = Write(Observations.Empty, null, [SomeRun(), direct, unknown]);

        json.Should().Contain("\"direct\": {").And.Contain("\"rank\": \"senior\"").And.Contain("\"size\": \"small\"").And.Contain("\"reward\": 90000");
        json.Split("\"direct\": {").Length.Should().Be(3, "the two direct runs only");
        Read(json).Runs.Should().BeEquivalentTo([SomeRun(), direct, unknown], options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData("direct", "direct")]
    [InlineData("finishedUtc", "finishedUtc")]
    [InlineData("kind", "loop")]
    [InlineData("member", "colour")]
    public void A_broken_run_is_left_out_with_a_line_and_the_rest_of_the_file_reads(string broken, string reason)
    {
        // K1 of the T13.6c approval: each run alone; the observations, the route and the other runs stay.
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, Direct: false, Reward: 325_250, ["Waste"]);
        var observations = Observations.Empty.Record(ObservationsTests.Scan(key, Monday, ("HUR-L3", "Waste", 113)));
        RouteRun[] runs =
        [
            SomeRun() with { Name = "Run A" },
            SomeRun() with { Name = "Run B", Direct = broken == "direct" ? new DirectContract(HaulRank.Senior, HaulSize.Small, 90_000) : null },
            SomeRun() with { Name = "Run C" },
        ];
        var json = Write(observations, SomeRoute(), runs);
        var at = json.IndexOf("\"name\": \"Run B\"", StringComparison.Ordinal);
        json = broken switch
        {
            "finishedUtc" => new Regex("\"finishedUtc\": \"[^\"]*\",\\s*").Replace(json, "", 1, at),
            "kind" => new Regex("\"kind\": \"localRing\"").Replace(json, "\"kind\": \"loop\"", 1, at),
            "member" => new Regex("\"name\": \"Run B\"").Replace(json, "\"name\": \"Run B\", \"colour\": \"red\"", 1, at),
            _ => json,
        };

        var read = Read(json);

        read.Runs.Select(r => r.Name).Should().Equal("Run A", "Run C");
        read.Dropped.Should().ContainSingle().Which.Should().Contain("runs[1]").And.Contain(reason);
        read.Observations.Entries.Should().BeEquivalentTo(observations.Entries);
        read.Route.Should().BeEquivalentTo(SomeRoute(), options => options.WithStrictOrdering());
    }

    [Fact]
    public void A_route_departed_before_it_started_and_more_runs_than_kept_are_refused()
    {
        var early = () => Read(Write(Observations.Empty, SomeRoute() with { DepartedUtc = Monday.AddHours(-1) }, []));
        var many = () => Read(Write(Observations.Empty, null, [.. Enumerable.Repeat(SomeRun(), RouteRun.MaxKept + 1)]));

        early.Should().Throw<RoutesFormatException>().WithMessage("*departedUtc*");
        many.Should().Throw<RoutesFormatException>().WithMessage($"*{RouteRun.MaxKept}*");
    }

    [Fact]
    public void A_schema_2_file_is_read_and_written_as_schema_3()
    {
        const string json = """{ "schemaVersion": 2, "observations": [] }""";

        var read = Read(json);

        (read.Route, read.Runs.Count).Should().Be((null, 0));
        Write(read.Observations).Should().Contain("\"schemaVersion\": 3");
    }

    // The Everus ring cut short: a bulk filled by scan #3's key, a way back that collects, an extra.
    internal static Route SomeRoute() => new(
        "Everus ring", Monday, "Everus Harbor", LoopKind.LocalRing, StarSystem.Stanton,
        ["HUR-L1 Green Glade Station", "HUR-L4 Melodic Fields Station"],
        [
            new Placeholder(1, RecipeKind.Bulk, "t-bulk", "Senior Rank - Medium Cargo Haul", 325_250,
                [new HaulLeg("Waste", 41, 72, Models.CrateSize.Sixteen)], ["HUR-L1 Green Glade Station"],
                FilledBy: "EVERUSHARBOR | HUR-L1:WASTE | 16 | 325250"),
            new Placeholder(2, RecipeKind.Return, "t-back", "Junior Rank - Small Cargo Haul", 72_500,
                [new HaulLeg("Waste", 5, 30, Models.CrateSize.Four)], ["Everus Harbor"],
                AcceptAt: "HUR-L1 Green Glade Station", CollectAt: ["HUR-L4 Melodic Fields Station"]),
        ],
        ["EVERUSHARBOR | HUR-L2:IRON | 4 | 34750"]);

    internal static RouteRun SomeRun() => new(
        "Everus ring", Monday, Monday.AddMinutes(10), Monday.AddMinutes(95), "Everus Harbor", LoopKind.LocalRing, StarSystem.Stanton,
        Landings: 6, Crates: 50, LongJumps: 4, Reward: 877_000, Filled: 9, Total: 10, Extras: 1,
        ShipId: "hull_b", Stations: ["HUR-L1 Green Glade Station", "HUR-L4 Melodic Fields Station"]);

    private static string ReplaceFirst(string text, string from, string to)
    {
        var at = text.IndexOf(from, StringComparison.Ordinal);
        return string.Concat(text.AsSpan(0, at), to, text.AsSpan(at + from.Length));
    }

    private static string Write(Observations observations, Route? route, IReadOnlyList<RouteRun> runs)
    {
        using var stream = new MemoryStream();
        RoutesFile.Write(stream, observations, route, runs);
        return Encoding.UTF8.GetString(stream.ToArray());
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
