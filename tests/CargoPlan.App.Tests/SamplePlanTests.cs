namespace CargoPlan.App.Tests;

public class SamplePlanTests
{
    [Fact]
    public void Load_plans_the_embedded_split_and_shared_missions_for_the_Hull_A()
    {
        var (ship, plan) = SamplePlan.Load();

        ship.Id.Should().Be("hull_a");
        plan.ShipId.Should().Be(ship.Id);
        plan.Placements.Select(p => p.Destination).Distinct().Should().Equal("Riker", "Shubin", "Orison", "Lorville");
    }

    [Fact]
    public void Sample_plan_shows_a_split_and_a_shared_rack()
    {
        // The ship view's demo: a stop in two racks and a rack with two stops, both as planner hints.
        var (_, plan) = SamplePlan.Load();

        plan.Warnings.Select(w => w.Message).Distinct().Should().Equal(
            "Riker spans racks 1+2",
            "Rack 2 shared by Riker, Shubin – identify crates by count");
    }
}
