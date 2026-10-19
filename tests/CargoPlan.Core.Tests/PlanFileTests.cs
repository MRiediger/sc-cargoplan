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

    private const string FixtureName = "plans/split_and_shared.json";

    private static SavedPlan Planned(IReadOnlyList<MissionStop> missions) => new(missions, LoadPlanner.Plan(HullA, missions.ToStops()));

    private static string FixtureText()
    {
        using var reader = new StreamReader(MissionFixtures.Open(FixtureName));
        return reader.ReadToEnd();
    }

    private static SavedPlan Read(string json, string sourceName = "plan.json")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return PlanFile.Read(stream, sourceName, HullA);
    }

    private static SavedPlan RoundTrip(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        stream.Position = 0;
        return PlanFile.Read(stream, "roundtrip.json", HullA);
    }

    private static void ShouldMatch(SavedPlan actual, SavedPlan expected)
    {
        actual.Missions.Should().BeEquivalentTo(expected.Missions, o => o.WithStrictOrdering());
        actual.Plan.ShipId.Should().Be(expected.Plan.ShipId);
        actual.Plan.Placements.Should().Equal(expected.Plan.Placements);
        actual.Plan.Warnings.Should().Equal(expected.Plan.Warnings);
    }

    // --- Fixture and round trip ---

    [Fact]
    public void Fixture_is_the_planned_split_and_shared_route()
    {
        // Keeps the fixture in step with the planner: --plan shows exactly what planning the missions gives.
        using var stream = MissionFixtures.Open(FixtureName);

        var saved = PlanFile.Read(stream, FixtureName, HullA);

        ShouldMatch(saved, Planned(MissionFixtures.SplitAndSharedMissions));
    }

    [Fact]
    public void Write_then_Read_keeps_missions_placements_hints_and_delivered()
    {
        var planned = Planned(MissionFixtures.SplitAndSharedMissions);
        SavedPlan saved = new(planned.Missions, LoadPlanner.SetDelivered(planned.Plan, HullA, "Riker", true));

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        loaded.Plan.PlacementsForStop("Riker").Should().OnlyContain(p => p.Delivered);
        loaded.Plan.PlacementsForStop("Shubin").Should().OnlyContain(p => !p.Delivered);
    }

    [Fact]
    public void Write_then_Read_keeps_a_moved_and_overloaded_placement()
    {
        // The player's override survives a restart, even one Validate complains about (D10).
        var planned = Planned(MissionFixtures.SplitAndSharedMissions);
        var lorville = planned.Plan.PlacementsForStop("Lorville").Single();
        SavedPlan saved = new(planned.Missions, LoadPlanner.MovePlacement(planned.Plan, HullA, lorville, "rack-0"));

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        LoadPlanner.Validate(loaded.Plan, HullA).Should().NotBeEmpty();
    }

    [Fact]
    public void Write_produces_camel_case_with_schema_version_ship_and_delivered()
    {
        using var stream = new MemoryStream();

        PlanFile.Write(stream, Planned(MissionFixtures.LoadMissions("example_missions")));

        var json = Encoding.UTF8.GetString(stream.ToArray());
        json.Should().Contain("\"schemaVersion\": 1").And.Contain("\"shipId\": \"hull_a\"")
            .And.Contain("\"missions\"").And.Contain("\"rackId\": \"rack-0\"").And.Contain("\"delivered\": false");
    }

    [Fact]
    public void Write_gives_schema_2_missions_inside_a_schema_1_plan_file()
    {
        // The fixture embeds schema 1 missions; a save turns them into schema 2, the plan file keeps its version (D23).
        using var stream = new MemoryStream();

        PlanFile.Write(stream, Planned(MissionFixtures.SplitAndSharedMissions));

        var node = JsonNode.Parse(Encoding.UTF8.GetString(stream.ToArray()))!;
        node["schemaVersion"]!.GetValue<int>().Should().Be(PlanFile.SupportedSchemaVersion);
        node["missions"]!["schemaVersion"]!.GetValue<int>().Should().Be(MissionFileReader.CurrentSchemaVersion);
        node["missions"]!["stops"]![0]!["items"]![0]!["maxCrateSize"]!.GetValue<int>().Should().Be(4);
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
    public void Write_leaves_the_stream_open()
    {
        using var stream = new MemoryStream();

        PlanFile.Write(stream, Planned(MissionFixtures.LoadMissions("example_missions")));

        stream.CanWrite.Should().BeTrue();
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
    public void SavedPlan_checks_the_placements_against_the_expanded_items()
    {
        MissionStop[] missions = [new("Riker", 1, [new MissionItem("Stims", 15, CrateSize.Four)])];
        Placement[] expanded =
        [
            new("Riker", "rack-0", new CargoItem("Stims", 12, CrateSize.Four), false),
            new("Riker", "rack-0", new CargoItem("Stims", 2, CrateSize.Two), false),
            new("Riker", "rack-0", new CargoItem("Stims", 1, CrateSize.One), false),
        ];

        var complete = () => new SavedPlan(missions, new LoadPlan("hull_a", expanded, []));
        var withoutTheOne = () => new SavedPlan(missions, new LoadPlan("hull_a", expanded[..2], []));

        complete.Should().NotThrow();
        withoutTheOne.Should().Throw<ArgumentException>().WithMessage("*Riker*Stims (1 SCU crates)*0 of 1 SCU*");
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
        { """{ "schemaVersion": 2 }""", "*schemaVersion 2*" },
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

    /// <summary>One change to the fixture each; the rest stays valid, so the change is what fails.</summary>
    private static readonly Dictionary<string, Action<JsonNode>> Changes = new()
    {
        ["inner_schema"] = n => n["missions"]!["schemaVersion"] = 3,
        ["unknown_property"] = n => n["opacity"] = 0.5,
        ["missing_delivered"] = n => n["placements"]![0]!.AsObject().Remove("delivered"),
        ["foreign_ship"] = n => n["shipId"] = "cutlass_black",
        ["unknown_rack"] = n => n["placements"]![0]!["rackId"] = "rack-9",
        ["unknown_warning_rack"] = n => n["warnings"]![0]!["rackId"] = "rack-9",
        ["scu_mismatch"] = n => n["placements"]![0]!["item"]!["scu"] = 12,
        ["foreign_destination"] = n => n["placements"]![0]!["destination"] = "Area18",
        ["invalid_crate_size"] = n => n["placements"]![0]!["item"]!["crateSize"] = 3,
        ["no_stops"] = n => n["missions"]!["stops"] = new JsonArray(),
    };

    [Theory]
    [InlineData("inner_schema", "*missions: schemaVersion 3*expected 1 or 2*")]
    [InlineData("unknown_property", "*opacity*")]
    [InlineData("missing_delivered", "*delivered*")]
    [InlineData("foreign_ship", "*'cutlass_black'*'hull_a'*")]
    [InlineData("unknown_rack", "*rack-9*")]
    [InlineData("unknown_warning_rack", "*rack-9*")]
    [InlineData("scu_mismatch", "*Riker*Titanium*12 of 16 SCU*")]
    [InlineData("foreign_destination", "*Area18*")]
    [InlineData("invalid_crate_size", "*3 SCU is not a crate size*")]
    [InlineData("no_stops", "*no stops*")]
    public void Read_invalid_plan_throws_PlanFormatException_naming_the_source_and_reason(string change, string expectedMessage)
    {
        var node = JsonNode.Parse(FixtureText())!;
        Changes[change](node);

        var act = () => Read(node.ToJsonString(), "broken.json");

        var ex = act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage).Which;
        ex.SourceName.Should().Be("broken.json");
        ex.Message.Should().Contain("broken.json");
    }
}
