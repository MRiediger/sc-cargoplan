using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class PlanFileTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    // Schema 1: placements without positions, as saved before D24. Kept as the migration's input.
    private const string V1FixtureName = "plans/split_and_shared.json";

    private static SavedPlan Planned(IReadOnlyList<MissionStop> missions, FillOrder fillOrder = FillOrder.BowFirst) =>
        new(missions, LoadPlanner.Plan(HullA, missions.ToStops(), fillOrder));

    private static string V1FixtureText()
    {
        using var reader = new StreamReader(MissionFixtures.Open(V1FixtureName));
        return reader.ReadToEnd();
    }

    /// <summary>The split-and-shared plan as schema 2 writes it.</summary>
    private static string V2Text() => Written(Planned(MissionFixtures.SplitAndSharedMissions));

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json, string sourceName = "plan.json")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return PlanFile.Read(stream, sourceName, HullA);
    }

    private static SavedPlan RoundTrip(SavedPlan saved) => Read(Written(saved), "roundtrip.json");

    private static void ShouldMatch(SavedPlan actual, SavedPlan expected)
    {
        actual.Missions.Should().BeEquivalentTo(expected.Missions, o => o.WithStrictOrdering());
        actual.Plan.ShipId.Should().Be(expected.Plan.ShipId);
        actual.Plan.FillOrder.Should().Be(expected.Plan.FillOrder);
        actual.Plan.Crates.Should().Equal(expected.Plan.Crates);
        actual.Plan.Warnings.Should().Equal(expected.Plan.Warnings);
    }

    // --- Schema 2 round trip ---

    [Fact]
    public void Write_then_Read_keeps_missions_crates_hints_and_delivered()
    {
        var planned = Planned(MissionFixtures.SplitAndSharedMissions);
        SavedPlan saved = new(planned.Missions, LoadPlanner.SetDelivered(planned.Plan, HullA, "Riker", true));

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        loaded.Plan.PlacementsForStop("Riker").Should().OnlyContain(p => p.Delivered);
        loaded.Plan.PlacementsForStop("Shubin").Should().OnlyContain(p => !p.Delivered);
    }

    [Fact]
    public void Write_then_Read_keeps_a_moved_crate()
    {
        // The player's override survives a restart.
        var planned = Planned(MissionFixtures.SplitAndSharedMissions);
        var gold = planned.Plan.Crates.First(c => c.Commodity == "Gold");
        SavedPlan saved = new(planned.Missions, LoadPlanner.MoveCrate(planned.Plan, HullA, gold, "rack-3", new GridPosition(0, 0, 1)));

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        LoadPlanner.Validate(loaded.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Write_then_Read_keeps_the_fill_order()
    {
        var saved = Planned(MissionFixtures.SplitAndSharedMissions, FillOrder.SternFirst);

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        loaded.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Fact]
    public void Write_then_Read_keeps_derived_and_overridden_crates()
    {
        var saved = Planned(MissionFixtures.CrateMixMissions);

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        loaded.Missions[0].Items[0].Crates.Should().BeNull();
        loaded.Missions[1].Items[0].Crates.Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two);
    }

    [Fact]
    public void Write_produces_camel_case_schema_2_with_fill_order_and_crate_positions()
    {
        var node = JsonNode.Parse(Written(Planned(MissionFixtures.LoadMissions("example_missions"))))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(2);
        node["shipId"]!.GetValue<string>().Should().Be("hull_a");
        node["fillOrder"]!.GetValue<string>().Should().Be("bowFirst");
        node["missions"]!["schemaVersion"]!.GetValue<int>().Should().Be(MissionFileReader.CurrentSchemaVersion);
        node["missions"]!["stops"]![0]!["items"]![0]!["maxCrateSize"]!.GetValue<int>().Should().Be(4);

        var crate = node["crates"]![0]!;
        crate["destination"]!.GetValue<string>().Should().Be("Riker");
        crate["commodity"]!.GetValue<string>().Should().Be("Titanium");
        crate["crateSize"]!.GetValue<int>().Should().Be(4);
        crate["rackId"]!.GetValue<string>().Should().Be("rack-0");
        crate["position"]!.AsArray().Select(v => v!.GetValue<int>()).Should().Equal(2, 0, 0);
        crate["delivered"]!.GetValue<bool>().Should().BeFalse();
        node.AsObject().Should().NotContainKey("placements");
    }

    [Fact]
    public void Write_writes_stern_first_as_its_name()
    {
        JsonNode.Parse(Written(Planned(MissionFixtures.SplitAndSharedMissions, FillOrder.SternFirst)))!["fillOrder"]!
            .GetValue<string>().Should().Be("sternFirst");
    }

    [Fact]
    public void Write_leaves_the_stream_open()
    {
        using var stream = new MemoryStream();

        PlanFile.Write(stream, Planned(MissionFixtures.LoadMissions("example_missions")));

        stream.CanWrite.Should().BeTrue();
    }

    // --- Schema 1 migration (D24) ---

    [Fact]
    public void Schema_1_fixture_loads_with_the_crates_of_a_fresh_plan()
    {
        // An upgrade keeps the plan of a running route: the placements are laid out exactly as the planner lays them out.
        using var stream = MissionFixtures.Open(V1FixtureName);

        var saved = PlanFile.Read(stream, V1FixtureName, HullA);

        ShouldMatch(saved, Planned(MissionFixtures.SplitAndSharedMissions));
    }

    [Fact]
    public void Schema_1_keeps_the_ticks()
    {
        var node = JsonNode.Parse(V1FixtureText())!;
        node["placements"]![0]!["delivered"] = true;
        node["placements"]![1]!["delivered"] = true;

        var saved = Read(node.ToJsonString());

        saved.Plan.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => c.Delivered);
        saved.Plan.Crates.Where(c => c.Destination != "Riker").Should().OnlyContain(c => !c.Delivered);
    }

    [Fact]
    public void Schema_1_is_written_back_as_schema_2()
    {
        var saved = Read(V1FixtureText());

        var node = JsonNode.Parse(Written(saved))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(PlanFile.SupportedSchemaVersion);
        ShouldMatch(Read(node.ToJsonString()), saved);
    }

    [Fact]
    public void Schema_1_with_an_overloaded_rack_is_refused()
    {
        // Allowed in schema 1 (D10), but 20 SCU have no layout in a 16 SCU grid. The file stays for the player.
        var node = JsonNode.Parse(V1FixtureText())!;
        node["placements"]![1]!["rackId"] = "rack-0";

        var act = () => Read(node.ToJsonString(), "old.json");

        act.Should().Throw<PlanFormatException>().WithMessage("*old.json*rack 1*20/16 SCU*");
    }

    [Theory]
    [InlineData("unknown_rack", "*rack-9*")]
    [InlineData("scu_mismatch", "*Riker, Titanium: 3 of 4 crates of 4 SCU placed*")]
    [InlineData("foreign_ship", "*'cutlass_black'*'hull_a'*")]
    public void Schema_1_invalid_plan_throws_PlanFormatException(string change, string expectedMessage)
    {
        var node = JsonNode.Parse(V1FixtureText())!;
        Action<JsonNode> apply = change switch
        {
            "unknown_rack" => n => n["placements"]![0]!["rackId"] = "rack-9",
            "scu_mismatch" => n => n["placements"]![0]!["item"]!["scu"] = 12,
            _ => n => n["shipId"] = "cutlass_black",
        };
        apply(node);

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage);
    }

    // --- Red, not refused (D24) ---

    [Fact]
    public void Read_loads_overlapping_crates_and_Validate_shows_them()
    {
        // Overlap and support are the player's business to see, not a broken file: the rack turns red.
        var node = JsonNode.Parse(V2Text())!;
        node["crates"]![1]!["position"] = node["crates"]![0]!["position"]!.DeepClone();

        var saved = Read(node.ToJsonString());

        LoadPlanner.Validate(saved.Plan, HullA).Should().Contain(w => w.RackId == "rack-0" && w.Message.Contains("overlap"));
    }

    // --- SavedPlan ---

    [Fact]
    public void SavedPlan_rejects_a_plan_made_from_other_missions()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => new SavedPlan(MissionFixtures.LoadMissions("example_missions"), plan);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SavedPlan_checks_the_crates_against_the_expanded_items()
    {
        MissionStop[] missions = [new("Riker", 1, [new MissionItem("Stims", 15, CrateSize.Four)])];
        Crate Stims(CrateSize size, int length, int width, int stack) => new("Riker", "Stims", size, "rack-0", new GridPosition(length, width, stack));
        Crate[] crates =
        [
            Stims(CrateSize.Four, 2, 0, 0), Stims(CrateSize.Four, 0, 0, 0), Stims(CrateSize.Four, 2, 0, 1),
            Stims(CrateSize.Two, 0, 0, 1), Stims(CrateSize.One, 0, 1, 1),
        ];

        var complete = () => new SavedPlan(missions, new LoadPlan("hull_a", crates, []));
        var withoutTheOne = () => new SavedPlan(missions, new LoadPlan("hull_a", crates[..4], []));

        complete.Should().NotThrow();
        withoutTheOne.Should().Throw<ArgumentException>().WithMessage("*Riker, Stims: 0 of 1 crates of 1 SCU placed*");
    }

    [Fact]
    public void SavedPlan_rejects_no_missions()
    {
        // Such a file could not be read back: a mission file needs at least one stop.
        var act = () => new SavedPlan([], new LoadPlan("hull_a", [], []));

        act.Should().Throw<ArgumentException>();
    }

    // --- Broken files ---

    public static TheoryData<string, string> Broken => new()
    {
        { "not json", "*plan.json*" },
        { "null", "*schemaVersion*" },
        { """{ "schemaVersion": 3 }""", "*schemaVersion 3*expected 1 or 2*" },
        { """{ "shipId": "hull_a" }""", "*schemaVersion*" },
    };

    [Theory]
    [MemberData(nameof(Broken))]
    public void Read_broken_json_throws_PlanFormatException(string json, string expectedMessage)
    {
        var act = () => Read(json);

        var ex = act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage).Which;
        ex.SourceName.Should().Be("plan.json");
    }

    /// <summary>One change to the schema 2 file each; the rest stays valid, so the change is what fails.</summary>
    private static readonly Dictionary<string, Action<JsonNode>> Changes = new()
    {
        ["inner_schema"] = n => n["missions"]!["schemaVersion"] = 3,
        ["unknown_property"] = n => n["opacity"] = 0.5,
        ["missing_delivered"] = n => n["crates"]![0]!.AsObject().Remove("delivered"),
        ["foreign_ship"] = n => n["shipId"] = "cutlass_black",
        ["unknown_rack"] = n => n["crates"]![0]!["rackId"] = "rack-9",
        ["unknown_warning_rack"] = n => n["warnings"]![0]!["rackId"] = "rack-9",
        ["missing_crate"] = n => n["crates"]!.AsArray().RemoveAt(0),
        ["foreign_destination"] = n => n["crates"]![0]!["destination"] = "Area18",
        ["invalid_crate_size"] = n => n["crates"]![0]!["crateSize"] = 3,
        ["no_stops"] = n => n["missions"]!["stops"] = new JsonArray(),
        ["short_position"] = n => n["crates"]![0]!["position"] = new JsonArray(0, 0),
        ["negative_position"] = n => n["crates"]![0]!["position"] = new JsonArray(0, -1, 0),
        ["outside"] = n => n["crates"]![0]!["position"] = new JsonArray(3, 0, 0),
        ["unknown_fill_order"] = n => n["fillOrder"] = "diagonal",
    };

    [Theory]
    [InlineData("inner_schema", "*missions: schemaVersion 3*expected 1 or 2*")]
    [InlineData("unknown_property", "*opacity*")]
    [InlineData("missing_delivered", "*delivered*")]
    [InlineData("foreign_ship", "*'cutlass_black'*'hull_a'*")]
    [InlineData("unknown_rack", "*rack-9*")]
    [InlineData("unknown_warning_rack", "*rack-9*")]
    [InlineData("missing_crate", "*Riker, Titanium: 3 of 4 crates of 4 SCU placed*")]
    [InlineData("foreign_destination", "*Area18*")]
    [InlineData("invalid_crate_size", "*3 SCU is not a crate size*")]
    [InlineData("no_stops", "*no stops*")]
    [InlineData("short_position", "*position needs 3 values*")]
    [InlineData("negative_position", "*position needs 3 values*")]
    [InlineData("outside", "*4 SCU crate of Titanium for Riker at [3, 0, 0] lies outside rack 1 (4×2×2)*")]
    [InlineData("unknown_fill_order", "*fillOrder 'diagonal'*")]
    public void Read_invalid_plan_throws_PlanFormatException_naming_the_source_and_reason(string change, string expectedMessage)
    {
        var node = JsonNode.Parse(V2Text())!;
        Changes[change](node);

        var act = () => Read(node.ToJsonString(), "broken.json");

        var ex = act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage).Which;
        ex.SourceName.Should().Be("broken.json");
        ex.Message.Should().Contain("broken.json");
    }
}
