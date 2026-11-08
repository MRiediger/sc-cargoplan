using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>The row axis in plan files (schema 9) and mission files (schema 4, D88), the rules of the fill order (D72).</summary>
public class FillRowsFormatTests
{
    private static readonly Ship Raft = FrozenShips.Load("raft");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan Planned(FillRows rows)
    {
        Mission[] missions = [new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])])];
        return LoadPlanner.Plan(Raft, missions, ["Riker"], FillOrder.SternFirst, fillRows: rows);
    }

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json, Ship ship)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return PlanFile.Read(stream, "test.json", ship);
    }

    private static string GoldenBow()
    {
        using var reader = new StreamReader(MissionFixtures.Open("plans/golden_example_missions_bow.json"));
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <summary>The golden as it would have been before schema 9: no fillRows, the older number.</summary>
    private static string OlderGolden(int schema) => GoldenBow()
        .Replace("  \"fillRows\": \"lengthwise\",\n  \"grouping\": \"none\",\n  \"sectionGap\": 0,\n", "", StringComparison.Ordinal)
        .Replace("\"schemaVersion\": 9", $"\"schemaVersion\": {schema}", StringComparison.Ordinal);

    // --- Plan file ---

    [Theory]
    [InlineData(FillRows.Lengthwise, "lengthwise")]
    [InlineData(FillRows.Crosswise, "crosswise")]
    public void A_plan_file_always_writes_the_row_axis_and_reads_it_back(FillRows rows, string word)
    {
        var json = Written(Planned(rows));

        json.Should().Contain($"\"fillRows\": \"{word}\"");
        Read(json, Raft).Plan.FillRows.Should().Be(rows);
    }

    [Fact]
    public void The_row_axis_stands_right_behind_the_fill_order()
    {
        var json = Written(Planned(FillRows.Crosswise)).ReplaceLineEndings("\n");

        json.Should().Contain("\"fillOrder\": \"sternFirst\",\n  \"fillRows\": \"crosswise\",\n  \"grouping\": \"none\",\n  \"sectionGap\": 0,\n  \"contractTrackerWatermark\"");
    }

    [Fact]
    public void A_schema_8_file_reads_lengthwise_and_writes_back_as_schema_9()
    {
        var saved = Read(OlderGolden(8), HullA);

        saved.Plan.FillRows.Should().Be(FillRows.Lengthwise);
        Written(saved).ReplaceLineEndings("\n").Should().Be(GoldenBow());
    }

    [Fact]
    public void Martins_RAFT_plan_reads_as_schema_8_with_his_crates_where_they_lie()
    {
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");

        var saved = PlanFile.Read(stream, "raft", Raft);

        saved.Plan.FillRows.Should().Be(FillRows.Lengthwise);
        saved.Missions.Should().HaveCount(4);
        saved.Plan.Crates.Sum(c => (int)c.CrateSize).Should().Be(66);
        saved.Plan.Crates.First().Position.Should().Be(new GridPosition(10, 0, 0));
    }

    [Fact]
    public void A_fillRows_in_a_schema_8_file_is_an_error()
    {
        var node = JsonNode.Parse(OlderGolden(8))!;
        node["fillRows"] = "crosswise";

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*fillRows*new in schema 9*the file says 8*");
    }

    [Fact]
    public void A_schema_9_file_without_fillRows_is_an_error()
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node.AsObject().Remove("fillRows");

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*fillRows*schema 9 requires*");
    }

    [Fact]
    public void An_unknown_row_axis_in_a_plan_file_is_an_error()
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node["fillRows"] = "diagonal";

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*fillRows 'diagonal' is unknown*lengthwise*crosswise*");
    }

    // --- Grouping (D89): schema 9 and 4 carry it, optional on reading ---

    [Theory]
    [InlineData(PlanGrouping.None, "none")]
    [InlineData(PlanGrouping.ByDestination, "byDestination")]
    public void A_plan_file_always_writes_the_grouping_and_reads_it_back(PlanGrouping grouping, string word)
    {
        Mission[] missions = [new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])])];
        var json = Written(LoadPlanner.Plan(Raft, missions, ["Riker"], FillOrder.BowFirst, 0, FillRows.Crosswise, grouping));

        json.Should().Contain($"\"grouping\": \"{word}\"");
        Read(json, Raft).Plan.Grouping.Should().Be(grouping);
    }

    [Fact]
    public void A_schema_9_file_without_grouping_means_none()
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node.AsObject().Remove("grouping");

        Read(node.ToJsonString(), HullA).Plan.Grouping.Should().Be(PlanGrouping.None);
    }

    [Fact]
    public void A_grouping_in_a_schema_8_file_is_an_error()
    {
        var node = JsonNode.Parse(OlderGolden(8))!;
        node["grouping"] = "byDestination";

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*grouping*new in schema 9*the file says 8*");
    }

    [Fact]
    public void An_unknown_grouping_in_a_plan_file_is_an_error()
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node["grouping"] = "byStop";

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*grouping 'byStop' is unknown*none*byDestination*");
    }

    [Fact]
    public void Section_notes_survive_the_file()
    {
        Rack rack = new("rack-0", 0, 4, new Dimensions(2, 2, 1), null, new ShipPoint(0, 0, 0));
        Ship ship = new("flat", "Flat", 4, [rack]);
        Mission[] missions =
        [
            new(1, [new MissionStop("A", 1, [new MissionItem("Ice", 1, CrateSize.One)])]),
            new(2, [new MissionStop("B", 1, [new MissionItem("Food", 1, CrateSize.One)])]),
            new(3, [new MissionStop("C", 1, [new MissionItem("Gold", 1, CrateSize.One)])]),
        ];
        var saved = LoadPlanner.Plan(ship, missions, ["A", "B", "C"], FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.ByDestination);

        var read = Read(Written(saved), ship);

        read.Plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message).Should().Equal("Rack 1: C shares a section with B");
    }

    [Fact]
    public void The_note_about_no_sections_survives_the_file()
    {
        Rack rack = new("rack-0", 0, 8, new Dimensions(2, 2, 2), null, new ShipPoint(0, 0, 0));
        Ship ship = new("flat", "Flat", 8, [rack]);
        Mission[] missions =
        [
            new(1, [new MissionStop("A", 1, [new MissionItem("Ice", 4, CrateSize.Four)])]),
            new(2, [new MissionStop("B", 1, [new MissionItem("Food", 4, CrateSize.Four)])]),
        ];
        var saved = LoadPlanner.Plan(ship, missions, ["A", "B"], FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.ByDestination);

        Read(Written(saved), ship).Plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message).Should().Equal("Rack 1: sections don't fit – laid as with None");
    }

    [Theory]
    [InlineData(PlanGrouping.None, "none")]
    [InlineData(PlanGrouping.ByDestination, "byDestination")]
    public void Export_always_writes_the_grouping_and_import_reads_it(PlanGrouping grouping, string word)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, Stops, FillOrder.SternFirst, FillRows.Crosswise, grouping);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        json.Should().Contain($"\"grouping\": \"{word}\"");
        ReadMissions(json).Grouping.Should().Be(grouping);
    }

    [Fact]
    public void A_mission_file_without_the_field_names_no_grouping()
    {
        ReadMissions($$"""{ "schemaVersion": 4, {{StopsJson}} }""").Grouping.Should().BeNull();
    }

    [Fact]
    public void A_grouping_in_a_schema_3_mission_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 3, "grouping": "byDestination", {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*grouping*new in schema 4*the file says 3*");
    }

    [Fact]
    public void An_unknown_grouping_in_a_mission_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 4, "grouping": "byStop", {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*grouping 'byStop' is unknown*none*byDestination*");
    }

    // --- Section gap (K9.2b): schema 9 and 4 carry it, optional on reading ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_plan_file_always_writes_the_gap_and_reads_it_back(int gap)
    {
        Mission[] missions = [new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])])];
        var json = Written(LoadPlanner.Plan(Raft, missions, ["Riker"], FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.ByDestination, gap));

        json.Should().Contain($"\"sectionGap\": {gap}");
        Read(json, Raft).Plan.SectionGap.Should().Be(gap);
    }

    [Fact]
    public void A_schema_9_file_without_a_gap_means_none()
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node.AsObject().Remove("sectionGap");

        Read(node.ToJsonString(), HullA).Plan.SectionGap.Should().Be(0);
    }

    [Fact]
    public void A_gap_in_a_schema_8_file_is_an_error()
    {
        var node = JsonNode.Parse(OlderGolden(8))!;
        node["sectionGap"] = 1;

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*sectionGap*new in schema 9*the file says 8*");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void A_gap_out_of_range_in_a_plan_file_is_an_error(int gap)
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node["sectionGap"] = gap;

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*sectionGap*0, 1 or 2*");
    }

    [Fact]
    public void Export_always_writes_the_gap_and_import_reads_it()
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, Stops, FillOrder.SternFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 2);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        json.Should().Contain("\"sectionGap\": 2");
        ReadMissions(json).SectionGap.Should().Be(2);
    }

    [Fact]
    public void A_mission_file_without_the_field_names_no_gap()
    {
        ReadMissions($$"""{ "schemaVersion": 4, {{StopsJson}} }""").SectionGap.Should().BeNull();
    }

    [Fact]
    public void A_gap_in_a_schema_3_mission_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 3, "sectionGap": 1, {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*sectionGap*new in schema 4*the file says 3*");
    }

    [Fact]
    public void A_gap_out_of_range_in_a_mission_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 4, "sectionGap": 3, {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*sectionGap*0, 1 or 2*");
    }

    // --- Mission file ---

    private static readonly MissionStop[] Stops = [new("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])];

    private static MissionFile ReadMissions(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return MissionFileReader.Read(stream, "test.json");
    }

    private static string WrittenMissions(FillRows rows)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, Stops, FillOrder.SternFirst, rows);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private const string StopsJson = "\"stops\": [ { \"destination\": \"Riker\", \"items\": [ { \"commodity\": \"Titanium\", \"scu\": 8, \"maxCrateSize\": 4 } ] } ]";

    [Theory]
    [InlineData(FillRows.Lengthwise, "lengthwise")]
    [InlineData(FillRows.Crosswise, "crosswise")]
    public void Export_always_writes_the_row_axis_and_import_reads_it(FillRows rows, string word)
    {
        var json = WrittenMissions(rows);

        json.Should().Contain("\"schemaVersion\": 4").And.Contain($"\"fillRows\": \"{word}\"");
        var file = ReadMissions(json);
        file.FillRows.Should().Be(rows);
        file.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Fact]
    public void A_schema_4_file_without_the_field_names_no_row_axis()
    {
        ReadMissions($$"""{ "schemaVersion": 4, {{StopsJson}} }""").FillRows.Should().BeNull();
    }

    [Fact]
    public void A_schema_3_file_names_no_row_axis()
    {
        var file = MissionFixtures.LoadFile("fill_order_stern");

        file.FillRows.Should().BeNull();
        file.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Fact]
    public void A_fillRows_in_a_schema_3_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 3, "fillRows": "crosswise", {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*fillRows*new in schema 4*the file says 3*");
    }

    [Fact]
    public void An_unknown_row_axis_in_a_mission_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 4, "fillRows": "diagonal", {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*fillRows 'diagonal' is unknown*lengthwise*crosswise*");
    }
}
