using System.Text;
using System.Text.Json;
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

    // Schema 2: crates with positions, missions as an embedded mission file, as saved before D42.
    private const string V2FixtureName = "plans/split_and_shared_v2.json";

    private static SavedPlan Planned(IReadOnlyList<MissionStop> missions, FillOrder fillOrder = FillOrder.BowFirst) =>
        SavedPlan.FromStops(missions, LoadPlanner.Plan(HullA, missions.ToStops(), fillOrder), CrateCap.Of(HullA));

    private static string FixtureText(string name)
    {
        using var reader = new StreamReader(MissionFixtures.Open(name));
        return reader.ReadToEnd();
    }

    private static string V1FixtureText() => FixtureText(V1FixtureName);

    /// <summary>The split-and-shared plan as schema 2 wrote it.</summary>
    private static string V2Text() => FixtureText(V2FixtureName);

    /// <summary>The split-and-shared plan as schema 4 writes it.</summary>
    private static string V4Text() => Written(Planned(MissionFixtures.SplitAndSharedMissions));

    /// <summary>
    /// Riker 12 Titanium typed, a capture of Shubin 8 Quartz with an assumed crate size, one of three listed
    /// destinations and a reward of 50,250, a capture that didn't fit, watermark 41.
    /// </summary>
    private static SavedPlan WithCaptures()
    {
        static MissionStop Stop(string destination, string commodity, int scu, CrateSize max) => new(destination, 1, [new MissionItem(commodity, scu, max)]);
        SavedPlan empty = new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA), 41);
        var typed = LoadPlanner.PlaceInto(empty, HullA, [Stop("Riker", "Titanium", 12, CrateSize.Four)]);
        var captured = LoadPlanner.PlaceInto(typed, HullA, [Stop("Shubin", "Quartz", 8, CrateSize.Four)], new ContractSource([36, 37], true, 3, 50_250));
        // More than the Hull A holds, so it waits (a 32 SCU crate would be capped to 16s and fit, D70).
        return LoadPlanner.PlaceInto(captured, HullA, [Stop("Orison", "Gold", 72, CrateSize.Eight)], new ContractSource([40]));
    }

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
        actual.Watermark.Should().Be(expected.Watermark);
        actual.Plan.ShipId.Should().Be(expected.Plan.ShipId);
        actual.Plan.FillOrder.Should().Be(expected.Plan.FillOrder);
        actual.Plan.Crates.Should().Equal(expected.Plan.Crates);
        actual.Plan.Warnings.Should().Equal(expected.Plan.Warnings);
    }

    // --- Schema 3 round trip ---

    [Fact]
    public void Write_then_Read_keeps_missions_crates_hints_and_delivered()
    {
        var planned = Planned(MissionFixtures.SplitAndSharedMissions);
        SavedPlan saved = new(planned.Missions, LoadPlanner.SetDelivered(planned.Plan, HullA, "Riker", true), planned.CrateCap);

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
        SavedPlan saved = new(planned.Missions, LoadPlanner.MoveCrate(planned.Plan, HullA, gold, "rack-3", new GridPosition(0, 1, 1)), planned.CrateCap);

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
        loaded.Missions[0].Stops[0].Items[0].Crates.Should().BeNull();
        loaded.Missions[1].Stops[0].Items[0].Crates.Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two);
    }

    [Fact]
    public void Write_then_Read_keeps_captures_unplaced_missions_and_the_watermark()
    {
        var saved = WithCaptures();

        var loaded = RoundTrip(saved);

        ShouldMatch(loaded, saved);
        loaded.Missions[1].Contract.Should().BeEquivalentTo(new ContractSource([36, 37], true, 3, 50_250));
        loaded.Missions[2].Contract!.Reward.Should().BeNull();
        loaded.Missions[2].Unplaced.Should().BeTrue();
        loaded.Watermark.Should().Be(41);
    }

    [Fact]
    public void Write_produces_camel_case_schema_5_with_missions_watermark_and_crate_positions()
    {
        var node = JsonNode.Parse(Written(Planned(MissionFixtures.LoadMissions("example_missions"))))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(5);
        node["shipId"]!.GetValue<string>().Should().Be("hull_a");
        node["fillOrder"]!.GetValue<string>().Should().Be("bowFirst");
        node["contractTrackerWatermark"]!.GetValue<long>().Should().Be(0);
        var mission = node["missions"]![0]!;
        mission["id"]!.GetValue<int>().Should().Be(1);
        mission["unplaced"]!.GetValue<bool>().Should().BeFalse();
        mission.AsObject().Should().NotContainKey("contract");
        mission["stops"]![0]!["destination"]!.GetValue<string>().Should().Be("Riker");
        mission["stops"]![0]!["items"]![0]!["maxCrateSize"]!.GetValue<int>().Should().Be(4);

        var crate = node["crates"]![0]!;
        crate["item"]!.GetValue<int>().Should().Be(0);
        crate["mission"]!.GetValue<int>().Should().Be(1);
        crate["destination"]!.GetValue<string>().Should().Be("Riker");
        crate["commodity"]!.GetValue<string>().Should().Be("Titanium");
        crate["crateSize"]!.GetValue<int>().Should().Be(4);
        crate["rackId"]!.GetValue<string>().Should().Be("rack-0");
        crate["position"]!.AsArray().Select(v => v!.GetValue<int>()).Should().Equal(2, 0, 0);
        crate["delivered"]!.GetValue<bool>().Should().BeFalse();
        node.AsObject().Should().NotContainKey("placements");
    }

    [Fact]
    public void Write_writes_a_capture_with_its_ids()
    {
        var contract = JsonNode.Parse(Written(WithCaptures()))!["missions"]![1]!["contract"]!;

        contract["contractIds"]!.AsArray().Select(v => v!.GetValue<long>()).Should().Equal(36, 37);
        contract["crateSizeAssumed"]!.GetValue<bool>().Should().BeTrue();
        contract["listedDestinations"]!.GetValue<int>().Should().Be(3);
        contract["reward"]!.GetValue<long>().Should().Be(50_250);
    }

    [Fact]
    public void Write_leaves_out_what_a_mission_doesnt_have()
    {
        var missions = JsonNode.Parse(Written(WithCaptures()))!["missions"]!;

        missions[0]!.AsObject().Should().NotContainKey("contract");
        missions[0]!["stops"]![0]!["items"]![0]!.AsObject().Should().NotContainKey("crates");
        missions[2]!["contract"]!.AsObject().Should().NotContainKey("listedDestinations").And.NotContainKey("reward");
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

    // --- A plan from before D64 ---

    [Fact]
    public void A_plan_from_before_D64_loads_as_saved_with_its_hints_derived_anew()
    {
        // The eight-stop plan as the route-order planner wrote it: four stops in rack 4, with both of its route notes.
        var text = FixtureText("plans/before_b2_eight_stops.json");
        using var document = JsonDocument.Parse(text);
        var saved = Read(text);

        saved.Plan.Crates.Select(c => (c.Destination, c.RackId, c.Position, c.Delivered)).Should().Equal(
            document.RootElement.GetProperty("crates").EnumerateArray().Select(c => (
                c.GetProperty("destination").GetString()!,
                c.GetProperty("rackId").GetString()!,
                new GridPosition(c.GetProperty("position")[0].GetInt32(), c.GetProperty("position")[1].GetInt32(), c.GetProperty("position")[2].GetInt32()),
                c.GetProperty("delivered").GetBoolean())));
        saved.Plan.Warnings.Select(w => w.Message).Should().NotContain(m => m.Contains("route order") || m.Contains("unloads earlier"));
        saved.Plan.Warnings.Should().Contain(new PlanWarning("rack-3", "Rack 4: crate for Everus Harbor sits on cargo for Area18"));
        LoadPlanner.Validate(saved.Plan, HullA).Should().BeEmpty();
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
    public void Schema_1_is_written_back_as_schema_5()
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

    // --- Schema 2 migration (D42) ---

    [Fact]
    public void Schema_2_loads_one_mission_per_stop_with_its_crates()
    {
        var saved = Read(V2Text());

        ShouldMatch(saved, Planned(MissionFixtures.SplitAndSharedMissions));
        saved.Missions.Select(m => m.Stops.Single().Destination).Should().Equal(MissionFixtures.SplitAndSharedMissions.Select(s => s.Destination));
        saved.Plan.Crates.Should().OnlyContain(c => saved.MissionOf(c.Mission).Stops[0].Destination == c.Destination);
        saved.Watermark.Should().Be(0);
    }

    [Fact]
    public void Schema_2_keeps_ticks_and_positions()
    {
        var node = JsonNode.Parse(V2Text())!;
        foreach (var crate in node["crates"]!.AsArray().Where(c => c!["destination"]!.GetValue<string>() == "Riker"))
        {
            crate!["delivered"] = true;
        }

        var saved = Read(node.ToJsonString());

        saved.Plan.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => c.Delivered);
        saved.Plan.Crates.Select(c => (c.RackId, c.Position)).Should().Equal(Read(V2Text()).Plan.Crates.Select(c => (c.RackId, c.Position)));
    }

    [Fact]
    public void Schema_2_is_written_back_as_schema_5()
    {
        var saved = Read(V2Text());

        var node = JsonNode.Parse(Written(saved))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(PlanFile.SupportedSchemaVersion);
        ShouldMatch(Read(node.ToJsonString()), saved);
    }

    // --- Red, not refused (D24) ---

    [Fact]
    public void Read_loads_overlapping_crates_and_Validate_shows_them()
    {
        // Overlap and support are the player's business to see, not a broken file: the rack turns red.
        var node = JsonNode.Parse(V4Text())!;
        node["crates"]![1]!["position"] = node["crates"]![0]!["position"]!.DeepClone();

        var saved = Read(node.ToJsonString());

        LoadPlanner.Validate(saved.Plan, HullA).Should().Contain(w => w.RackId == "rack-0" && w.Message.Contains("overlap"));
    }

    // --- SavedPlan ---

    [Fact]
    public void SavedPlan_rejects_a_plan_made_from_other_missions()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => SavedPlan.FromStops(MissionFixtures.LoadMissions("example_missions"), plan, CrateCap.Of(HullA));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SavedPlan_checks_the_crates_against_the_expanded_items()
    {
        Mission[] missions = [new(1, [new MissionStop("Riker", 1, [new MissionItem("Stims", 15, CrateSize.Four)])])];
        Crate Stims(CrateSize size, int length, int width, int stack) => new("Riker", "Stims", size, "rack-0", new GridPosition(length, width, stack), Mission: 1);
        Crate[] crates =
        [
            Stims(CrateSize.Four, 2, 0, 0), Stims(CrateSize.Four, 0, 0, 0), Stims(CrateSize.Four, 2, 0, 1),
            Stims(CrateSize.Two, 0, 0, 1), Stims(CrateSize.One, 0, 1, 1),
        ];

        var complete = () => new SavedPlan(missions, new LoadPlan("hull_a", crates, []), CrateCap.Of(HullA));
        var withoutTheOne = () => new SavedPlan(missions, new LoadPlan("hull_a", crates[..4], []), CrateCap.Of(HullA));

        complete.Should().NotThrow();
        withoutTheOne.Should().Throw<ArgumentException>().WithMessage("*Riker, Stims: 0 of 1 crates of 1 SCU placed*");
    }

    [Fact]
    public void SavedPlan_allows_no_missions()
    {
        // The plan after its last mission was removed, and before the first capture (D41, D42).
        var act = () => new SavedPlan([], new LoadPlan("hull_a", [], []), CrateCap.Of(HullA));

        act.Should().NotThrow();
    }

    [Fact]
    public void SavedPlan_rejects_a_crate_of_an_unknown_mission()
    {
        var saved = Planned(MissionFixtures.SplitAndSharedMissions);

        var act = () => new SavedPlan(saved.Missions, saved.Plan with { Crates = [saved.Plan.Crates[0] with { Mission = 9 }, .. saved.Plan.Crates.Skip(1)] }, saved.CrateCap);

        act.Should().Throw<ArgumentException>().WithMessage("*mission 9*");
    }

    [Fact]
    public void SavedPlan_rejects_crates_of_an_unplaced_mission()
    {
        var saved = Planned(MissionFixtures.SplitAndSharedMissions);

        var act = () => new SavedPlan([saved.Missions[0] with { Unplaced = true }, .. saved.Missions.Skip(1)], saved.Plan, saved.CrateCap);

        act.Should().Throw<ArgumentException>().WithMessage("*didn't fit*");
    }

    [Fact]
    public void SavedPlan_rejects_a_mission_id_twice()
    {
        var saved = Planned(MissionFixtures.SplitAndSharedMissions);
        var twice = new Mission(saved.Missions[0].Id, saved.Missions[1].Stops);

        var act = () => new SavedPlan([saved.Missions[0], twice, .. saved.Missions.Skip(2)], saved.Plan, saved.CrateCap);

        act.Should().Throw<ArgumentException>().WithMessage("*id 1*more than once*");
    }

    [Fact]
    public void SavedPlan_rejects_one_destination_spelt_two_ways()
    {
        MissionStop Stop(string destination) => new(destination, 1, [new MissionItem("Gold", 4, CrateSize.Four)]);

        var act = () => new SavedPlan([new Mission(1, [Stop("Riker")]), new Mission(2, [Stop("riker")], Unplaced: true)], new LoadPlan("hull_a", [], []), CrateCap.Of(HullA));

        act.Should().Throw<ArgumentException>().WithMessage("*spelt more than one way*");
    }

    [Fact]
    public void SavedPlan_stops_merge_missions_per_destination_in_unload_order_then_unplaced()
    {
        var saved = WithCaptures();

        saved.Stops.Select(s => (s.Destination, s.Order)).Should().Equal(("Riker", 1), ("Shubin", 2), ("Orison", 3));
    }

    // --- Broken files ---

    public static TheoryData<string, string> Broken => new()
    {
        { "not json", "*plan.json*" },
        { "null", "*schemaVersion*" },
        { """{ "schemaVersion": 5 }""", "*schemaVersion 5*expected 1 or 2 or 3 or 4*" },
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
        ["unknown_rack"] = n => n["crates"]![0]!["rackId"] = "rack-9",
        ["unknown_warning_rack"] = n => n["warnings"]![0]!["rackId"] = "rack-9",
        ["missing_crate"] = n => n["crates"]!.AsArray().RemoveAt(0),
        ["foreign_destination"] = n => n["crates"]![0]!["destination"] = "Area18",
        ["invalid_crate_size"] = n => n["crates"]![0]!["crateSize"] = 3,
        ["no_stops"] = n => n["missions"]!["stops"] = new JsonArray(),
        ["short_position"] = n => n["crates"]![0]!["position"] = new JsonArray(0, 0),
        ["negative_position"] = n => n["crates"]![0]!["position"] = new JsonArray(0, -1, 0),
        ["unknown_fill_order"] = n => n["fillOrder"] = "diagonal",
    };

    [Theory]
    [InlineData("inner_schema", "*missions: schemaVersion 3*expected 1 or 2*")]
    [InlineData("unknown_property", "*opacity*")]
    [InlineData("missing_delivered", "*delivered*")]
    [InlineData("unknown_rack", "*rack-9*")]
    [InlineData("unknown_warning_rack", "*rack-9*")]
    [InlineData("missing_crate", "*Riker, Titanium: 3 of 4 crates of 4 SCU placed*")]
    [InlineData("foreign_destination", "*Area18*")]
    [InlineData("invalid_crate_size", "*3 SCU is not a crate size*")]
    [InlineData("no_stops", "*no stops*")]
    [InlineData("short_position", "*position needs 3 values*")]
    [InlineData("negative_position", "*position needs 3 values*")]
    [InlineData("unknown_fill_order", "*fillOrder 'diagonal'*")]
    public void Read_invalid_schema_2_plan_throws_PlanFormatException_naming_the_source_and_reason(string change, string expectedMessage)
    {
        var node = JsonNode.Parse(V2Text())!;
        Changes[change](node);

        var act = () => Read(node.ToJsonString(), "broken.json");

        var ex = act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage).Which;
        ex.SourceName.Should().Be("broken.json");
        ex.Message.Should().Contain("broken.json");
    }

    // A crate outside its rack is no broken file but racks that changed since (K3.2, K1): read, then planned anew.
    [Fact]
    public void Schema_2_with_a_crate_outside_its_rack_reads_as_racks_changed()
    {
        var node = JsonNode.Parse(V2Text())!;
        node["crates"]![0]!["position"] = new JsonArray(3, 0, 0);

        Read(node.ToJsonString()).CrateCap.Reason.Should().Be(CapReason.RacksChanged);
    }

    /// <summary>One change to the schema 4 file each; the rest stays valid, so the change is what fails.</summary>
    private static readonly Dictionary<string, Action<JsonNode>> V3Changes = new()
    {
        ["unknown_property"] = n => n["opacity"] = 0.5,
        ["missing_watermark"] = n => n.AsObject().Remove("contractTrackerWatermark"),
        ["negative_watermark"] = n => n["contractTrackerWatermark"] = -1,
        ["missing_unplaced"] = n => n["missions"]![0]!.AsObject().Remove("unplaced"),
        ["mission_id_0"] = n => n["missions"]![0]!["id"] = 0,
        ["mission_without_stops"] = n => n["missions"]![0]!["stops"] = new JsonArray(),
        ["crate_of_unknown_mission"] = n => n["crates"]![0]!["mission"] = 9,
        ["crate_without_mission"] = n => n["crates"]![0]!.AsObject().Remove("mission"),
        ["missing_crate"] = n => n["crates"]!.AsArray().RemoveAt(0),
        ["contract_without_ids"] = n => n["missions"]![0]!["contract"] = new JsonObject { ["contractIds"] = new JsonArray(), ["crateSizeAssumed"] = false },
        ["unknown_rack"] = n => n["crates"]![0]!["rackId"] = "rack-9",
    };

    [Theory]
    [InlineData("unknown_property", "*opacity*")]
    [InlineData("missing_watermark", "*contractTrackerWatermark*")]
    [InlineData("negative_watermark", "*watermark*")]
    [InlineData("missing_unplaced", "*unplaced*")]
    [InlineData("mission_id_0", "*mission id*")]
    [InlineData("mission_without_stops", "*Mission 1*no stop*")]
    [InlineData("crate_of_unknown_mission", "*mission 9*")]
    [InlineData("crate_without_mission", "*mission 0*")]
    [InlineData("missing_crate", "*Riker, Titanium: 3 of 4 crates of 4 SCU placed*")]
    [InlineData("contract_without_ids", "*at least one ContractTracker id*")]
    [InlineData("unknown_rack", "*rack-9*")]
    public void Read_invalid_schema_4_plan_throws_PlanFormatException(string change, string expectedMessage)
    {
        var node = JsonNode.Parse(V4Text())!;
        V3Changes[change](node);

        var act = () => Read(node.ToJsonString(), "broken.json");

        act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage).Which.SourceName.Should().Be("broken.json");
    }

    // --- A plan for another ship (D59): read without its grid, the overlay plans it anew ---

    [Fact]
    public void Schema_3_for_another_ship_loads_with_its_ship_id_and_missions()
    {
        var node = JsonNode.Parse(V4Text())!;
        node["shipId"] = "c2_hercules";
        node["crates"]![0]!["rackId"] = "rack-9";

        var saved = Read(node.ToJsonString());

        saved.Plan.ShipId.Should().Be("c2_hercules");
        saved.Missions.Should().BeEquivalentTo(Planned(MissionFixtures.SplitAndSharedMissions).Missions, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Schema_2_for_another_ship_loads_one_mission_per_stop()
    {
        var node = JsonNode.Parse(V2Text())!;
        node["shipId"] = "cutlass_black";

        var saved = Read(node.ToJsonString());

        saved.Plan.ShipId.Should().Be("cutlass_black");
        saved.Missions.Should().HaveCount(4);
    }

    [Fact]
    public void Schema_3_for_another_ship_still_checks_the_crates_against_the_missions()
    {
        var node = JsonNode.Parse(V4Text())!;
        node["shipId"] = "c2_hercules";
        node["crates"]!.AsArray().RemoveAt(0);

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*Riker, Titanium: 3 of 4 crates of 4 SCU placed*");
    }

    // --- ReadShipId: which ship the start takes when settings.json names none (T7.2) ---

    [Theory]
    [InlineData("""{ "schemaVersion": 3, "shipId": "c2_hercules" }""", "c2_hercules")]
    [InlineData("""{ "schemaVersion": 3 }""", null)]
    [InlineData("""{ "shipId": 5 }""", null)]
    [InlineData("""[ "hull_a" ]""", null)]
    [InlineData("not json", null)]
    public void ReadShipId_gives_the_ship_id_or_null(string json, string? expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        PlanFile.ReadShipId(stream).Should().Be(expected);
    }
}
