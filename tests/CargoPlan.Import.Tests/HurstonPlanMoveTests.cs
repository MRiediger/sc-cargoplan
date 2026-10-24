using System.Text;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Import.Tests;

/// <summary>Martin's plan of 23 Oct (B1 fixture) moved by hand: moves are accepted whenever the cargo fits (B3, D65).</summary>
public class HurstonPlanMoveTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    [Fact]
    public void Pulling_Everus_Harbor_crate_from_under_its_2_lets_the_2_settle()
    {
        // Everus Harbor ticked back: in Rack 1 its 2 lies on the bow 4. Before B3 the drag was refused: "… has nothing below it".
        var plan = LoadPlanner.SetDelivered(HurstonPlan().Plan, HullA, "Everus Harbor", false);
        var lower = plan.Crates.Single(c => c.Destination == "Everus Harbor" && c.RackId == "rack-0" && c.Position == new GridPosition(2, 0, 0));
        var upper = plan.Crates.Single(c => c.Destination == "Everus Harbor" && c.RackId == "rack-0" && c.CrateSize == CrateSize.Two);

        var moved = LoadPlanner.MoveCrate(plan, HullA, lower, "rack-2", new GridPosition(0, 0, 0));

        moved.Crates.Single(c => c.RackId == "rack-2").Position.Should().Be(new GridPosition(0, 0, 0));
        moved.Crates[plan.Crates.ToList().IndexOf(upper)].Position.Should().Be(new GridPosition(2, 0, 0));
        LoadPlanner.Validate(moved, HullA).Should().BeEmpty();
    }

    private static SavedPlan HurstonPlan()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ImportFixtures.Plan(ImportFixtures.HurstonPlan)));
        return PlanFile.Read(stream, ImportFixtures.HurstonPlan, HullA);
    }
}
