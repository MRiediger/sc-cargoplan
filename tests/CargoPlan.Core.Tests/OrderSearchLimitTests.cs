using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The search for a layout in route order gives up after <see cref="CrateLayout.MaxOrderSearchSteps"/> steps; the rack
/// keeps its first layout and the plan says so (D54). The Hull A never gets there (golden tests).
/// </summary>
public class OrderSearchLimitTests
{
    /// <summary>One rack of 4×4×4 cells: a 32 SCU crate covers the whole plate, two cells high.</summary>
    private static readonly Ship Cube = new("cube", "Cube test ship", 64,
        [new Rack("rack-0", 0, 64, new Dimensions(4, 4, 4), new RackPosition(RackSide.Port, RackLevel.Lower), new ShipPoint(0, 0, 0))]);

    private const string Note =
        "Rack 1: stacking order not checked – too many crates for the check. Crates are placed, verify the unload order by hand.";

    /// <summary>
    /// Riker's 32 fills the plate, Shubin's twenty 1s go on top of it, later on earlier. Riker can't stand on them either
    /// (it would take 32), so no layout in route order exists, and with 12 cells to leave empty the search can't prove it.
    /// </summary>
    private static readonly MissionStop[] CubeRoute =
    [
        new("Riker", 1, [new MissionItem("Gold", 32, CrateSize.ThirtyTwo)]),
        new("Shubin", 2, [new MissionItem("Iron", 20, CrateSize.One)]),
    ];

    private static LoadPlan Planned() => LoadPlanner.Plan(Cube, CubeRoute.ToStops());

    [Fact]
    public void Plan_notes_a_rack_whose_search_for_the_route_order_gave_up()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var plan = Planned();
        watch.Stop();

        plan.Crates.Sum(c => (int)c.CrateSize).Should().Be(52);
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Riker, Shubin – identify crates by count"),
            new PlanWarning("rack-0", Note));
        LoadPlanner.Validate(plan, Cube).Should().NotBeEmpty().And.OnlyContain(w => w.Message.EndsWith("which is unloaded first"));
        watch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public void The_note_stays_through_ticks_moves_and_the_plan_file()
    {
        var plan = Planned();
        // A 1 on top moves to a free cell of the top layer, which rests on the full layer below.
        var shubin = plan.Crates.First(c => c.Destination == "Shubin" && c.Position.Stack == 3);
        HashSet<GridPosition> taken = [.. plan.Crates.SelectMany(c => c.Cells)];
        var free = Enumerable.Range(0, 4).SelectMany(l => Enumerable.Range(0, 4).Select(w => new GridPosition(l, w, 3))).First(p => !taken.Contains(p));

        var ticked = LoadPlanner.SetDelivered(plan, Cube, "Riker", true);
        var moved = LoadPlanner.MoveCrate(plan, Cube, shubin, "rack-0", free);

        ticked.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
        moved.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
        LoadPlanner.HintsOnBoard(ticked, Cube).Should().Contain(new PlanWarning("rack-0", Note));
        ReadBack(SavedPlan.FromStops(CubeRoute, plan)).Plan.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
    }

    [Fact]
    public void A_new_plan_that_needs_no_limit_has_no_note()
    {
        // The note is carried by moves and ticks, never into a plan of its own.
        var plan = LoadPlanner.Plan(Cube, [CubeRoute[0].ToStop()]);

        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void PlaceInto_notes_the_rack_too()
    {
        // Riker's 4 lies on the plate. Shubin's 49 1s would need 49 cells off Riker's columns, there are 48: no layout in
        // route order, but the search only finds out on the top layer, after trying where to leave 11 of 48 cells empty.
        SavedPlan empty = new([], new LoadPlan(Cube.Id, [], []));
        var riker = LoadPlanner.PlaceInto(empty, Cube, [new MissionStop("Riker", 1, [new MissionItem("Gold", 4, CrateSize.Four)])]);

        var saved = LoadPlanner.PlaceInto(riker, Cube, [new MissionStop("Shubin", 1, [new MissionItem("Iron", 49, CrateSize.One)])]);

        saved.Missions.Should().OnlyContain(m => !m.Unplaced);
        saved.Plan.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
    }

    private static SavedPlan ReadBack(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        using var input = new MemoryStream(stream.ToArray());
        return PlanFile.Read(input, "plan.json", Cube);
    }
}
