using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A rack's minimum (K3.3, D70 addendum) on the shipped Hull C, whose spindle takes 32 SCU crates only: no mix is split
/// up to the minimum, a mission without one waits and says why.
/// </summary>
public class MinCrateSizeTests
{
    private static readonly Ship HullC = ShipLoader.Load("hull_c");
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private const string Only32 = "doesn't fit (ship takes 32 SCU crates only)";

    private static MissionStop Riker(int scu, CrateSize max, params CrateSize[] set) =>
        new("Riker", 1, [new MissionItem("Titanium", scu, max, set.Length == 0 ? null : set)]);

    private static SavedPlan Planned(Ship ship, MissionStop stop) => LoadPlanner.Plan(ship, [new Mission(1, [stop])], [stop.Destination]);

    private static SavedPlan Placed(Ship ship, MissionStop stop) =>
        LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship)), ship, [stop]);

    [Fact]
    public void A_32_contract_plans()
    {
        Planned(HullC, Riker(64, CrateSize.ThirtyTwo)).Plan.Crates.Should().HaveCount(2).And.OnlyContain(c => c.CrateSize == CrateSize.ThirtyTwo);
    }

    [Fact]
    public void A_16_contract_waits_and_says_why()
    {
        var act = () => Planned(HullC, Riker(16, CrateSize.Sixteen));
        act.Should().Throw<PlanException>().WithMessage("16 SCU crate of Titanium for Riker doesn't fit any rack of hull_c (ship takes 32 SCU crates only).");

        var saved = Placed(HullC, Riker(16, CrateSize.Sixteen));
        saved.Missions.Should().ContainSingle().Which.Unplaced.Should().BeTrue();
        LoadPlanner.PlaceProblem(saved, HullC, 1).Should().Be(Only32);
    }

    [Fact]
    public void A_set_mix_below_the_minimum_waits_the_same_way()
    {
        var saved = Placed(HullC, Riker(32, CrateSize.ThirtyTwo, CrateSize.Sixteen, CrateSize.Sixteen));

        LoadPlanner.PlaceProblem(saved, HullC, 1).Should().Be(Only32);
    }

    // The kiosk gives 32 + 8 for 40 at max 32; the 8 fits no rack, so there is no mix (K1: no rounding up).
    [Fact]
    public void A_rest_below_the_minimum_waits()
    {
        LoadPlanner.PlaceProblem(Placed(HullC, Riker(40, CrateSize.ThirtyTwo)), HullC, 1).Should().Be(Only32);
    }

    [Fact]
    public void A_ship_switch_makes_a_16_contract_wait_and_plan_again()
    {
        var onHullA = Planned(HullA, Riker(16, CrateSize.Sixteen));

        var onHullC = LoadPlanner.Replan(onHullA, HullC);
        onHullC.Missions.Should().ContainSingle().Which.Unplaced.Should().BeTrue();
        LoadPlanner.PlaceProblem(onHullC, HullC, 1).Should().Be(Only32);

        var back = LoadPlanner.Replan(onHullC, HullA);
        back.Plan.Crates.Should().ContainSingle().Which.CrateSize.Should().Be(CrateSize.Sixteen);
    }

    [Fact]
    public void Split_below_the_minimum_is_refused()
    {
        var saved = Planned(HullC, Riker(32, CrateSize.ThirtyTwo));
        var crate = saved.Plan.Crates.Single();
        var number = HullC.Racks.Single(r => r.Id == crate.RackId).Number;

        LoadPlanner.SplitProblem(saved, HullC, crate).Should().Be($"Rack {number}: takes 32 SCU crates only.");
    }

    // A move onto a rack whose minimum refuses the crate goes on to stage 4, as a door does (D65).
    [Fact]
    public void A_move_onto_a_rack_whose_minimum_refuses_the_crate_goes_elsewhere()
    {
        Ship ship = new("min", "Minimum test ship", 24,
        [
            new Rack("rack-0", 0, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 0, 0), StackAxis.PlusZ, null, CrateSize.Sixteen),
            new Rack("rack-1", 1, 8, new Dimensions(2, 2, 2), null, new ShipPoint(5, 0, 0)),
        ]);
        LoadPlan plan = new(ship.Id, [new Crate("Shubin", "Stims", CrateSize.One, "rack-1", new GridPosition(0, 0, 0))], []);

        var outcome = LoadPlanner.ResolveMove(plan, ship, [new CrateMove(plan.Crates[0], "rack-0", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Single().RackId.Should().Be("rack-1");
        outcome.Notices.Should().Equal("Rack 1: takes 16 SCU crates only – placed in rack 2");
    }

    // K2: a plan saved before K3.3 with a 16 on the Hull C reads under another cap and is planned anew; the 16 then waits.
    [Fact]
    public void A_plan_file_with_a_crate_below_the_minimum_is_planned_anew()
    {
        var withoutMin = new Ship(HullC.Id, HullC.Name, HullC.TotalScu,
            [.. HullC.Racks.Select(r => new Rack(r.Id, r.Index, r.CapacityScu, r.Grid, r.Position, r.Origin, r.StackAxis, r.MaxCrateSize))]);
        using var stream = new MemoryStream();
        PlanFile.Write(stream, Planned(withoutMin, Riker(16, CrateSize.Sixteen)));
        stream.Position = 0;

        var read = PlanFile.Read(stream, "plan.json", HullC);

        read.CrateCap.Should().NotBe(CrateCap.Of(HullC));
        LoadPlanner.Replan(read, HullC).Missions.Should().ContainSingle().Which.Unplaced.Should().BeTrue();
    }
}
