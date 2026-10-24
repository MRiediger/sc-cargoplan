using System.Text;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Hull A plans stay byte-identical: every Hull A mission fixture in both fill orders, written as a plan file and compared
/// with the golden file. A difference changes the layout of every route a player plans, so the golden files are never
/// regenerated to make this pass, only with a decision that changes the planner's objective on purpose: D64 (no route
/// order) regenerated crate_mix and hull_a_eight_stops and left the other three byte-identical. D68 (plan schema 4)
/// regenerated all ten for the format alone: apart from <c>schemaVersion</c> and each crate's <c>item</c> they are the
/// files before, racks and positions unchanged.
/// </summary>
public class HullAGoldenTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    public static TheoryData<string, FillOrder> Cases => new()
    {
        { "example_missions", FillOrder.BowFirst },
        { "example_missions", FillOrder.SternFirst },
        { "split_and_shared", FillOrder.BowFirst },
        { "split_and_shared", FillOrder.SternFirst },
        { "split_required", FillOrder.BowFirst },
        { "split_required", FillOrder.SternFirst },
        { "crate_mix", FillOrder.BowFirst },
        { "crate_mix", FillOrder.SternFirst },
        { "hull_a_eight_stops", FillOrder.BowFirst },
        { "hull_a_eight_stops", FillOrder.SternFirst },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Plan_writes_the_golden_plan_file(string fixture, FillOrder fillOrder)
    {
        var written = Written(fixture, fillOrder);

        // Mission ids and the watermark come from the route, nothing from time or chance: two runs, one file.
        written.Should().Be(Written(fixture, fillOrder));
        written.Should().Be(Golden(fixture, fillOrder));
    }

    [Fact]
    public void Plan_of_too_much_cargo_keeps_its_message()
    {
        var act = () => LoadPlanner.Plan(HullA, MissionFixtures.OverflowMissions);

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "Ship capacity exceeded: 8 SCU missing (stops need 72 SCU, ship 'hull_a' holds 64 SCU).");
    }

    private static string Written(string fixture, FillOrder fillOrder)
    {
        var missions = MissionFixtures.LoadMissions(fixture);
        var saved = SavedPlan.FromStops(missions, LoadPlanner.Plan(HullA, missions.ToStops(), fillOrder));
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

    /// <summary>Line endings follow the checkout (.gitattributes text=auto), the bytes in between are compared.</summary>
    private static string Golden(string fixture, FillOrder fillOrder)
    {
        using var reader = new StreamReader(MissionFixtures.Open(GoldenResourceName(fixture, fillOrder)));
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    private static string GoldenResourceName(string fixture, FillOrder fillOrder) =>
        $"plans/golden_{fixture}_{(fillOrder == FillOrder.BowFirst ? "bow" : "stern")}.json";
}
