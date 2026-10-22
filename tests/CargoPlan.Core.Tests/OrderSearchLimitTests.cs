using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The search for a layout in route order gives up after <see cref="CrateLayout.MaxOrderSearchSteps"/> steps, scaled by
/// the rack's cells (D58); the rack keeps its first layout and the plan says so (D54). The Hull A never gets there (golden
/// tests). A plan that reaches the limit costs about the same on any rack (about 1.3 s in Release, D58), so those tests
/// are "Slow" and stay out of the default run: <c>dotnet test tests/CargoPlan.Core.Tests -p:RunSettingsFilePath= --filter Category=Slow</c>.
/// Giving up itself is tested fast in <see cref="CrateLayoutTests"/> with a budget of one step.
/// </summary>
public class OrderSearchLimitTests
{
    /// <summary>One rack of 8×2×4 cells: a 32 SCU crate (8×2×2, D57) covers the whole plate, two cells high.</summary>
    private static readonly Ship Column = new("column", "Column test ship", 64,
        [new Rack("rack-0", 0, 64, new Dimensions(8, 2, 4), new RackPosition(RackSide.Port, RackLevel.Lower), new ShipPoint(0, 0, 0))]);

    private const string Note =
        "Rack 1: too many crates to rearrange – red crates lie on cargo that unloads earlier. Check the unload order by hand.";

    /// <summary>
    /// Riker's 32 fills the plate, Shubin's twenty 1s go on top of it, later on earlier. Riker can't stand on them either
    /// (it would take 32), so no layout in route order exists, and with 12 cells to leave empty the search can't prove it.
    /// </summary>
    private static readonly MissionStop[] ColumnRoute =
    [
        new("Riker", 1, [new MissionItem("Gold", 32, CrateSize.ThirtyTwo)]),
        new("Shubin", 2, [new MissionItem("Iron", 20, CrateSize.One)]),
    ];

    /// <summary>
    /// A rack of 8×4×4 (128 cells). Riker's 4 on the plate fixed first, Shubin's 113 1s would need 113 cells off Riker's
    /// 2×2 column, there are 112: no layout in route order, but the search only finds out after trying where to leave
    /// cells empty. With Riker's 32 fixed instead, the search proves it at once (T7.1d).
    /// </summary>
    private static Rack Box(int index) =>
        new($"rack-{index}", index, 128, new Dimensions(8, 4, 4), new RackPosition(index == 0 ? RackSide.Port : RackSide.Starboard, RackLevel.Lower), new ShipPoint(8 * index, 0, 0));

    private static readonly MissionStop RikerFour = new("Riker", 1, [new MissionItem("Gold", 4, CrateSize.Four)]);

    private static readonly MissionStop ShubinOnes = new("Shubin", 1, [new MissionItem("Iron", 113, CrateSize.One)]);

    private static LoadPlan Planned() => LoadPlanner.Plan(Column, ColumnRoute.ToStops());

    // D58: steps × cells stays the same, 150,000 steps on 1,008 cells (the rack of the T7.0 stress load).
    [Theory]
    [InlineData(1_008, 150_000)]
    [InlineData(216, 700_000)]
    [InlineData(81, 1_866_666)]
    [InlineData(16, 9_450_000)]
    public void The_budget_scales_with_the_cells_of_the_rack(int cells, int steps)
    {
        CrateLayout.MaxOrderSearchSteps(cells).Should().Be(steps);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void Plan_notes_a_rack_whose_search_for_the_route_order_gave_up()
    {
        var plan = Planned();

        plan.Crates.Sum(c => (int)c.CrateSize).Should().Be(52);
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Riker, Shubin – identify crates by count"),
            new PlanWarning("rack-0", Note));
        LoadPlanner.Validate(plan, Column).Should().NotBeEmpty().And.OnlyContain(w => w.Message.EndsWith("which is unloaded first"));
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void The_note_stays_through_ticks_moves_and_the_plan_file()
    {
        var plan = Planned();
        // A 1 on top moves to a free cell of the top layer, which rests on the full layer below.
        var shubin = plan.Crates.First(c => c.Destination == "Shubin" && c.Position.Stack == 3);
        HashSet<GridPosition> taken = [.. plan.Crates.SelectMany(c => c.Cells)];
        var free = Enumerable.Range(0, 8).SelectMany(l => Enumerable.Range(0, 2).Select(w => new GridPosition(l, w, 3))).First(p => !taken.Contains(p));

        var ticked = LoadPlanner.SetDelivered(plan, Column, "Riker", true);
        var moved = LoadPlanner.MoveCrate(plan, Column, shubin, "rack-0", free);

        ticked.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
        moved.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
        LoadPlanner.HintsOnBoard(ticked, Column).Should().Contain(new PlanWarning("rack-0", Note));
        ReadBack(SavedPlan.FromStops(ColumnRoute, plan)).Plan.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
    }

    [Fact]
    public void A_new_plan_that_needs_no_limit_has_no_note()
    {
        // The note is carried by moves and ticks, never into a plan of its own.
        var plan = LoadPlanner.Plan(Column, [ColumnRoute[0].ToStop()]);

        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void PlaceInto_notes_the_rack_too()
    {
        Ship box = new("box", "Box test ship", 128, [Box(0)]);
        var riker = LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(box.Id, [], [])), box, [RikerFour]);

        var saved = LoadPlanner.PlaceInto(riker, box, [ShubinOnes]);

        saved.Missions.Should().OnlyContain(m => !m.Unplaced);
        saved.Plan.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void MoveStop_notes_the_target_rack_too()
    {
        // Two boxes: Riker's 4 alone in rack 1, Shubin's 113 1s alone in rack 2, so the plan has no note. Moved onto Riker
        // they are the PlaceInto case above: 112 cells off Riker's column for 113 crates, too many ways to try.
        Ship boxes = new("boxes", "Two boxes test ship", 256, [Box(0), Box(1)]);
        var plan = LoadPlanner.Plan(boxes, [RikerFour.ToStop(), ShubinOnes.ToStop() with { Order = 2 }]);

        var moved = LoadPlanner.MoveStop(plan, boxes, "Shubin", "rack-0");

        plan.Warnings.Should().NotContain(w => w.Message == Note);
        moved.Warnings.Should().Contain(new PlanWarning("rack-0", Note));
    }

    private static SavedPlan ReadBack(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        using var input = new MemoryStream(stream.ToArray());
        return PlanFile.Read(input, "plan.json", Column);
    }
}
