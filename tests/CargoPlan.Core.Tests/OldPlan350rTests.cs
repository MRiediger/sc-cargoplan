using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// V1 (v0.5.1): a 350r plan saved by v0.5.0, when the ship had one 2 × 2 rack, read against today's 350r (Motherlode
/// 4.10.3: rack-0 is a new 4 × 2 rack in front, the old 2 × 2 is rack-1). A data test over the baseline (A10, D113): it reads what the
/// app ships. The frozen file was written by v0.5.0's own code (worktree on tag v0.5.0, 16 Nov 2026): Shubin 2 Quartz in
/// one crate of 2, delivered; Riker 2 Titanium in two crates of 1.
/// </summary>
public class OldPlan350rTests
{
    private static readonly Ship Today = ShipLoader.Load(ShipLoader.DefaultFolder, "350r");

    private static SavedPlan Read()
    {
        using var stream = MissionFixtures.Open("plans/350r_v0.5.0.json");
        return PlanFile.Read(stream, "350r_v0.5.0.json", Today);
    }

    [Fact]
    public void Todays_350r_keeps_the_old_rack_as_rack_1_behind_a_bigger_rack_0()
    {
        // The premise of the other tests: the rack id the old file names now is another rack, its old cells fit inside.
        Today.Racks.Select(r => (r.Id, r.Grid, r.Origin)).Should().Equal(
            ("rack-0", new Dimensions(4, 2, 1), new ShipPoint(-2, -1, 0)),
            ("rack-1", new Dimensions(2, 2, 1), new ShipPoint(-4, -1, 0)));
    }

    [Fact]
    public void A_plan_from_v0_5_0_loads_as_written_without_a_re_plan()
    {
        // Every old position lies inside today's rack-0, so the file reads under the ship's own cap: no "racks changed",
        // no re-plan on loading, no notice; the tick stays.
        var saved = Read();

        saved.CrateCap.Should().Be(CrateCap.Of(Today));
        saved.Plan.Crates.Select(c => (c.Destination, c.RackId, c.Position, c.Delivered)).Should().Equal(
            ("Shubin", "rack-0", new GridPosition(0, 0, 0), true),
            ("Riker", "rack-0", new GridPosition(1, 1, 0), false),
            ("Riker", "rack-0", new GridPosition(0, 1, 0), false));
        LoadPlanner.Validate(saved.Plan, Today).Should().BeEmpty();
    }

    [Fact]
    public void Its_crates_now_stand_on_the_front_rack_not_where_the_player_loaded_them()
    {
        // The finding behind "re-plan recommended": rack-0 lies 2 cells further to the bow than the old rack did, so the
        // overlay shows the cargo on a rack the player didn't load. Nothing marks it.
        var crates = Read().Plan.Crates;

        crates.Should().OnlyContain(c => c.RackId == "rack-0");
        Today.Racks.Single(r => r.Id == "rack-0").Origin.Should().NotBe(new ShipPoint(-4, -1, 0), "v0.5.0's only rack lay at -4");
    }

    [Fact]
    public void A_re_plan_lays_it_out_on_todays_racks_and_keeps_the_tick()
    {
        var replanned = LoadPlanner.Replan(Read(), Today);

        replanned.Missions.Should().OnlyContain(m => !m.Unplaced);
        replanned.Plan.Crates.Where(c => c.Destination == "Shubin").Should().OnlyContain(c => c.Delivered);
        replanned.Plan.Crates.Where(c => c.Destination == "Riker").Should().HaveCount(2).And.OnlyContain(c => !c.Delivered && !c.OffShip);
        LoadPlanner.Validate(replanned.Plan, Today).Should().BeEmpty();
    }
}
