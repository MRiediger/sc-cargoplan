namespace CargoPlan.App.Tests;

public class SamplePlanTests
{
    [Fact]
    public void Load_plans_the_embedded_example_missions_for_the_Hull_A()
    {
        var (ship, plan) = SamplePlan.Load();

        ship.Id.Should().Be("hull_a");
        plan.ShipId.Should().Be(ship.Id);
        plan.Placements.Select(p => p.Destination).Distinct().Should().Equal("Riker", "Shubin", "Orison");
    }
}
