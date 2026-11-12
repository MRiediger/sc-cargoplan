using System.Text;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.Import.Tests;

/// <summary>Martin's plan of 23 Oct (B1 fixture) moved by hand: moves are accepted whenever the cargo fits (B3, D65).</summary>
public class HurstonPlanMoveTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    [Fact]
    public void Pulling_Everus_Harbor_crate_from_under_its_2_lets_the_2_settle()
    {
        // Everus Harbor ticked back: in Rack 1 its 2 lies on the bow 4. Before B3 the drag was refused: "… has nothing below it".
        var plan = LoadPlanner.SetDelivered(HurstonPlan().Plan, HullA, "Everus Harbor", false);
        var lower = plan.Crates.Single(c => c.Destination == "Everus Harbor" && c.RackId == "rack-0" && c.Position == new GridPosition(2, 0, 0));
        var upper = plan.Crates.Single(c => c.Destination == "Everus Harbor" && c.RackId == "rack-0" && c.CrateSize == CrateSize.Two);

        var moved = PlannerMoves.MoveCrate(plan, HullA, lower, "rack-2", new GridPosition(0, 0, 0));

        moved.Crates.Single(c => c.RackId == "rack-2").Position.Should().Be(new GridPosition(0, 0, 0));
        moved.Crates[plan.Crates.ToList().IndexOf(upper)].Position.Should().Be(new GridPosition(2, 0, 0));
        LoadPlanner.Validate(moved, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Its_ContractTracker_captures_read_as_scans_1_to_3()
    {
        // Schema 9, watermark 18 (D107): the ids become the key, the number follows the file's order.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ImportFixtures.Plan(ImportFixtures.HurstonPlan)));

        var (saved, lastScanNo, fromContractTracker) = PlanFile.ReadContent(stream, ImportFixtures.HurstonPlan, HullA);

        saved.Missions.Select(m => (m.Id, m.Contract!.Scan!.No, m.Contract.Scan.Key, m.Contract.Reward)).Should().Equal(
            (1, 1, "legacy:9-10-11-12", null),
            (2, 2, "legacy:17", null),
            (3, 3, "legacy:18", (long?)80_750));
        (lastScanNo, fromContractTracker.Count).Should().Be((3, 3));
    }

    private static SavedPlan HurstonPlan()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ImportFixtures.Plan(ImportFixtures.HurstonPlan)));
        return PlanFile.Read(stream, ImportFixtures.HurstonPlan, HullA);
    }
}
