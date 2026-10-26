using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// An item added or changed by hand in a ContractTracker mission is <c>manual</c> (K6.5): stored in the plan, schema 7
/// (no second bump after K6.4), written only where true. Mission files never carry it: what the editor exports is all
/// handwork anyway, and a mission file says nothing about ContractTracker.
/// </summary>
public class ManualItemTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    /// <summary>Capture 37 to Riker, and Melodic Fields added by hand to the same mission.</summary>
    private static SavedPlan WithManualStop()
    {
        Mission mission = new(1,
        [
            new MissionStop("Riker", 1, [new MissionItem("Stims", 4, CrateSize.Four)]),
            new MissionStop("Melodic Fields", 2, [new MissionItem("Stims", 2, CrateSize.Two, Manual: true)]),
        ], new ContractSource([37]));
        return LoadPlanner.Plan(HullA, [mission], ["Riker", "Melodic Fields"]);
    }

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json) => PlanFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "plan.json", HullA);

    [Fact]
    public void A_plan_writes_manual_only_where_true()
    {
        var items = JsonNode.Parse(Written(WithManualStop()))!["missions"]![0]!["stops"]!.AsArray()
            .Select(s => s!["items"]![0]!.AsObject()).ToArray();

        items[0].ContainsKey("manual").Should().BeFalse();
        items[1]["manual"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Write_then_Read_keeps_manual()
    {
        var read = Read(Written(WithManualStop()));

        read.Missions.Single().Items.Select(at => at.Item.Manual).Should().Equal(false, true);
    }

    [Fact]
    public void Schema_6_with_manual_is_broken()
    {
        var node = JsonNode.Parse(Written(WithManualStop()))!;
        node["schemaVersion"] = 6;

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*manual*schema 7*");
    }

    [Fact]
    public void A_mission_file_refuses_manual()
    {
        const string json = """
            { "schemaVersion": 3, "stops": [ { "destination": "Riker", "items": [ { "commodity": "Stims", "scu": 4, "maxCrateSize": 4, "manual": true } ] } ] }
            """;

        var act = () => MissionFileReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "missions.json");

        act.Should().Throw<MissionFormatException>().WithMessage("*manual*plan file*");
    }

    [Fact]
    public void A_mission_file_never_writes_manual()
    {
        using var stream = new MemoryStream();
        MissionFileWriter.Write(stream, WithManualStop().Missions.Single().Stops, FillOrder.BowFirst);

        Encoding.UTF8.GetString(stream.ToArray()).Should().NotContain("manual");
    }

    [Fact]
    public void Manual_changes_nothing_the_planner_sees()
    {
        var saved = WithManualStop();
        Mission plain = new(1, [.. saved.Missions.Single().Stops.Select(s => s with { Items = [.. s.Items.Select(i => i with { Manual = false })] })], new ContractSource([37]));

        saved.Plan.Crates.Should().Equal(LoadPlanner.Plan(HullA, [plain], ["Riker", "Melodic Fields"]).Plan.Crates);
    }
}
