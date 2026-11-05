using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>The legend's order lives in Core (B6, D69), so the sections of a plan can follow what the player sees (D89).</summary>
public class LegendOrderTests
{
    private static Crate Of(string destination, int mission) =>
        new(destination, "Titanium", CrateSize.One, "rack-0", new GridPosition(0, 0, 0), Mission: mission);

    [Fact]
    public void Martins_RAFT_plan_reads_in_the_order_of_his_legend()
    {
        var raft = ShipLoader.Load("raft");
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");
        var saved = PlanFile.Read(stream, "raft", raft);

        saved.Plan.LegendDestinations([.. saved.Missions.Select(m => m.Id)]).Should().Equal(
            "Green Glade Station", "High Course Station", "Thundering Express Station", "Faithful Dream Station", "Melodic Fields Station");
    }

    [Fact]
    public void A_mission_brings_all_its_destinations_along()
    {
        // Route A, B, C, D; mission 1 goes to A and D, so D reads before B, as the legend shows it.
        LoadPlan plan = new("raft", [Of("A", 1), Of("B", 2), Of("C", 2), Of("D", 1)], []);

        plan.Route.Should().Equal("A", "B", "C", "D");
        plan.LegendDestinations([1, 2]).Should().Equal("A", "D", "B", "C");
    }

    [Fact]
    public void Missions_starting_at_one_destination_stand_in_mission_order()
    {
        LoadPlan plan = new("raft", [Of("A", 2), Of("A", 1), Of("B", 1), Of("C", 2)], []);

        plan.LegendMissions([1, 2]).Select(m => m.Mission).Should().Equal(1, 2);
        plan.LegendDestinations([1, 2]).Should().Equal("A", "B", "C");
        plan.LegendDestinations([2, 1]).Should().Equal("A", "C", "B");
    }

    [Fact]
    public void A_line_holds_its_crates_for_one_destination()
    {
        LoadPlan plan = new("raft", [Of("A", 1), Of("B", 1), Of("A", 1)], []);

        plan.LegendMissions([1]).Single().Lines.Select(l => (l.Destination, l.Crates.Count)).Should().Equal(("A", 2), ("B", 1));
    }
}
