using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Sides in plan files (schema 11) and mission files (schema 5, D116): always written, behind the section gap; older files
/// read as <c>none</c> with overflow; in an older schema the fields are an error, as the row axis was (D88).
/// </summary>
public class SidesFormatTests
{
    private static readonly Ship Raft = FrozenShips.Load("raft");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan Planned(Sides sides, bool overflow)
    {
        Mission[] missions = [new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])])];
        return LoadPlanner.Plan(Raft, missions, ["Riker"], FillOrder.SternFirst, FillRows.Lengthwise, PlanGrouping.None, 0, sides, overflow);
    }

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
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

    private const string SideLines = "  \"sides\": \"none\",\n  \"sideOverflow\": true,\n";

    /// <summary>The golden as schema 10 wrote it: the older number, no sides. Checked, so the replacements can't fall silent.</summary>
    private static string Schema10Golden()
    {
        var golden = GoldenBow();
        golden.Should().Contain("\"schemaVersion\": 11,").And.Contain(SideLines);
        return golden.Replace("\"schemaVersion\": 11,", "\"schemaVersion\": 10,", StringComparison.Ordinal).Replace(SideLines, "", StringComparison.Ordinal);
    }

    // --- Plan file ---

    [Theory]
    [InlineData(Sides.None, true, "none", "true")]
    [InlineData(Sides.PortFirst, false, "portFirst", "false")]
    [InlineData(Sides.StarboardFirst, true, "starboardFirst", "true")]
    public void A_plan_file_always_writes_the_sides_and_reads_them_back(Sides sides, bool overflow, string word, string flag)
    {
        var json = Written(Planned(sides, overflow));

        json.Should().Contain("\"schemaVersion\": 11,").And.Contain($"\"sides\": \"{word}\",\n  \"sideOverflow\": {flag},");
        var read = Read(json, Raft).Plan;
        (read.Sides, read.SideOverflow).Should().Be((sides, overflow));
    }

    [Fact]
    public void The_sides_stand_behind_the_section_gap()
    {
        Written(Planned(Sides.PortFirst, false)).Should().Contain("\"sectionGap\": 0,\n  \"sides\": \"portFirst\",\n  \"sideOverflow\": false,\n");
    }

    [Fact]
    public void A_schema_10_file_reads_without_sides_and_writes_back_as_the_current_schema()
    {
        var saved = Read(Schema10Golden(), HullA);

        (saved.Plan.Sides, saved.Plan.SideOverflow).Should().Be((Sides.None, true));
        Written(saved).Should().Be(GoldenBow());
    }

    [Theory]
    [InlineData("sides", "\"portFirst\"")]
    [InlineData("sideOverflow", "false")]
    public void A_side_field_in_a_schema_10_file_is_an_error(string field, string value)
    {
        var node = JsonNode.Parse(Schema10Golden())!;
        node[field] = JsonNode.Parse(value);

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage($"*{field}*new in schema 11*the file says 10*");
    }

    [Theory]
    [InlineData("sides")]
    [InlineData("sideOverflow")]
    public void A_schema_11_file_without_a_side_field_is_an_error(string field)
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node.AsObject().Remove(field);

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage($"*no {field}*schema 11 requires*");
    }

    [Fact]
    public void An_unknown_side_word_is_an_error()
    {
        var node = JsonNode.Parse(GoldenBow())!;
        node["sides"] = "left";

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*sides 'left' is unknown*portFirst*");
    }

    // --- Mission file ---

    private static readonly MissionStop[] Stops = [new("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])];

    private const string StopsJson = "\"stops\": [ { \"destination\": \"Riker\", \"items\": [ { \"commodity\": \"Titanium\", \"scu\": 8, \"maxCrateSize\": 4 } ] } ]";

    private static MissionFile ReadMissions(string json) => MissionFileReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "test.json");

    [Theory]
    [InlineData(Sides.PortFirst, false, "portFirst", "false")]
    [InlineData(Sides.None, true, "none", "true")]
    public void Export_always_writes_the_sides_and_import_reads_them(Sides sides, bool overflow, string word, string flag)
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, Stops, FillOrder.SternFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 1, sides, overflow);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        json.Should().Contain("\"schemaVersion\": 5").And.Contain($"\"sides\": \"{word}\"").And.Contain($"\"sideOverflow\": {flag}");
        var file = ReadMissions(json);
        (file.Sides, file.SideOverflow).Should().Be((sides, overflow));
    }

    [Fact]
    public void A_schema_4_file_names_no_sides()
    {
        var file = ReadMissions($$"""{ "schemaVersion": 4, "fillRows": "crosswise", {{StopsJson}} }""");

        (file.Sides, file.SideOverflow).Should().Be(((Sides?)null, (bool?)null));
    }

    [Theory]
    [InlineData("sides", "\"portFirst\"")]
    [InlineData("sideOverflow", "false")]
    public void A_side_field_in_a_schema_4_file_is_an_error(string field, string value)
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 4, "{{field}}": {{value}}, {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage($"*{field}*new in schema 5*the file says 4*");
    }

    [Fact]
    public void An_unknown_side_word_in_a_mission_file_is_an_error()
    {
        var act = () => ReadMissions($$"""{ "schemaVersion": 5, "sides": "left", {{StopsJson}} }""");

        act.Should().Throw<MissionFormatException>().WithMessage("*sides 'left' is unknown*");
    }
}
