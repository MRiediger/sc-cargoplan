using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Crates turn 90° about the rack's stack axis (K3.2, D71): length and width swap in the grid, the stack stays. Only
/// crates with an oblong footprint turn (2, 16, 24, 32); 1, 4 and 8 are square. The planner lays a crate unturned where it
/// can and turns it only where that fails, so every plan that worked before stays as it was.
/// </summary>
public class CrateRotationTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly Ship Freelancer = ShipLoader.Load("freelancer");
    private static readonly Ship Perseus = ShipLoader.Load("perseus");

    /// <summary>One rack of these cells, stacking up.</summary>
    private static Ship OneRack(int length, int width, int stack, CrateSize? max = null, CrateSize? min = null) =>
        new("one_rack", "One rack test ship", length * width * stack,
        [
            new Rack("rack-0", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0), StackAxis.PlusZ, max, min),
        ]);

    private static MissionItem Item(int scu, CrateSize max, params CrateSize[] crates) =>
        new("Titanium", scu, max, crates.Length == 0 ? null : crates);

    private static SavedPlan Planned(Ship ship, params MissionItem[] items)
    {
        MissionStop riker = new("Riker", 1, items);
        return LoadPlanner.Plan(ship, [new Mission(1, [riker])], ["Riker"]);
    }

    private static Crate Riker(CrateSize size, int length, int width, int stack, bool rotated = false, string rackId = "rack-0") =>
        new("Riker", "Titanium", size, rackId, new GridPosition(length, width, stack), Mission: 1, Rotated: rotated);

    /// <summary>The plan with these crates instead of the planner's; the missions stay.</summary>
    private static SavedPlan WithCrates(SavedPlan saved, params Crate[] crates) =>
        new(saved.Missions, saved.Plan with { Crates = crates }, saved.CrateCap);

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json, Ship ship) => PlanFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "plan.json", ship);

    // --- Shapes ---

    [Theory]
    [InlineData(CrateSize.Two, 1, 2, 1)]
    [InlineData(CrateSize.Sixteen, 2, 4, 2)]
    [InlineData(CrateSize.TwentyFour, 2, 6, 2)]
    [InlineData(CrateSize.ThirtyTwo, 2, 8, 2)]
    public void A_turned_crate_swaps_length_and_width(CrateSize size, int length, int width, int stack)
    {
        size.Turns.Should().BeTrue();
        size.Shape(rotated: true).Should().Be(new Dimensions(length, width, stack));
        size.Shape(rotated: false).Should().Be(size.Dimensions);
    }

    [Theory]
    [InlineData(CrateSize.One)]
    [InlineData(CrateSize.Four)]
    [InlineData(CrateSize.Eight)]
    public void A_square_crate_doesnt_turn(CrateSize size)
    {
        size.Turns.Should().BeFalse();

        var act = () => Riker(size, 0, 0, 0, rotated: true);

        act.Should().Throw<ArgumentException>().WithMessage($"*{(int)size} SCU*turn*");
    }

    [Fact]
    public void A_turned_crate_takes_the_cells_across()
    {
        Riker(CrateSize.Two, 0, 0, 0, rotated: true).Cells.Should().Equal(new GridPosition(0, 0, 0), new GridPosition(0, 1, 0));
    }

    // The Hull A's plates stack along ±y (D26): length runs along x, width along z. Turned, a 2 stands upright in the ship.
    [Fact]
    public void A_turned_crate_on_the_Hull_A_runs_along_z()
    {
        var rack = HullA.Racks[0];

        Extent(rack.CellBox(Riker(CrateSize.Two, 0, 0, 0, rotated: true))).Should().Be((1, 1, 2));
        Extent(rack.CellBox(Riker(CrateSize.Two, 0, 0, 0))).Should().Be((2, 1, 1));
    }

    private static (int X, int Y, int Z) Extent(ShipBox box) => (box.Max.X - box.Min.X, box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z);

    // --- Racks ---

    [Fact]
    public void A_grid_takes_a_crate_that_fits_only_turned()
    {
        OneRack(1, 2, 3).Racks[0].CanHold(CrateSize.Two).Should().BeTrue();
        OneRack(6, 8, 2).Racks[0].CanHold(CrateSize.ThirtyTwo).Should().BeTrue();
        OneRack(1, 1, 3).Racks[0].CanHold(CrateSize.Two).Should().BeFalse();
    }

    [Fact]
    public void A_door_still_refuses_a_crate_that_fits_only_turned()
    {
        var rack = OneRack(2, 4, 2, max: CrateSize.Eight).Racks[0];

        rack.FitsGrid(CrateSize.Sixteen).Should().BeTrue();
        rack.CanHold(CrateSize.Sixteen).Should().BeFalse();
        rack.LimitRefuses(CrateSize.Sixteen).Should().BeTrue();
    }

    [Fact]
    public void A_minimum_the_grid_takes_only_turned_loads_and_plans()
    {
        var ship = OneRack(4, 8, 6, min: CrateSize.ThirtyTwo);

        var crates = Planned(ship, Item(192, CrateSize.ThirtyTwo)).Plan.Crates;

        crates.Should().HaveCount(6).And.OnlyContain(c => c.Rotated);
    }

    // --- Planner ---

    [Fact]
    public void Three_2s_fill_a_3x2x1_rack_one_of_them_turned()
    {
        var ship = OneRack(3, 2, 1);

        var plan = Planned(ship, Item(6, CrateSize.Two)).Plan;

        plan.Crates.Should().HaveCount(3);
        plan.Crates.Count(c => c.Rotated).Should().Be(1);
        LoadPlanner.Validate(plan, ship).Should().BeEmpty();
    }

    // Perseus rack 1 is 6×8×2 in Fleetyards' orientation: a 32 (8×2×2) fits only turned, three of them fill it.
    [Fact]
    public void Three_32s_fill_the_Perseus_turned()
    {
        var plan = Planned(Perseus, Item(96, CrateSize.ThirtyTwo)).Plan;

        plan.Crates.Should().HaveCount(3).And.OnlyContain(c => c.CrateSize == CrateSize.ThirtyTwo && c.Rotated);
        LoadPlanner.Validate(plan, Perseus).Should().BeEmpty();
    }

    // Freelancer racks 2 and 3 are 1×2×3: a 2 fits only turned. Rack 1 (9×2×3) keeps a 1×2 column per layer for one
    // more turned 2, so 66 SCU of 2s fill the ship; unturned, rack 1 held 24 and the side racks none.
    [Fact]
    public void The_Freelancers_racks_take_66_SCU_of_2s_turned_where_needed()
    {
        var plan = Planned(Freelancer, Item(66, CrateSize.Two)).Plan;

        plan.Crates.Should().HaveCount(33);
        plan.Crates.Where(c => c.RackId is "rack-0" or "rack-1").Should().HaveCount(6).And.OnlyContain(c => c.Rotated);
        plan.Crates.Count(c => c.RackId == "rack-2" && c.Rotated).Should().Be(3);
        LoadPlanner.Validate(plan, Freelancer).Should().BeEmpty();
    }

    [Fact]
    public void The_planner_turns_nothing_where_crates_fit_unturned()
    {
        LoadPlanner.Plan(HullA, MissionFixtures.LoadMissions("hull_a_eight_stops").ToStops(CrateCap.Of(HullA)))
            .Crates.Should().OnlyContain(c => !c.Rotated);
    }

    // --- Moves ---

    [Fact]
    public void A_crate_turned_in_place_is_a_move()
    {
        var ship = OneRack(2, 2, 1);
        var saved = WithCrates(Planned(ship, Item(2, CrateSize.Two)), Riker(CrateSize.Two, 0, 0, 0));
        var crate = saved.Plan.Crates.Single();

        var moved = LoadPlanner.MoveCrates(saved.Plan, ship, [new CrateMove(crate, "rack-0", new GridPosition(0, 0, 0), Turn: true)]);

        moved.Crates.Single().Should().Be(crate with { Rotated = true });
        LoadPlanner.MovedCrates(saved.Plan, moved).Should().Be(1);
    }

    // 3×2×1: two 2s side by side in the first two lengths, the last length free. Turned, one of them fits there exactly.
    [Fact]
    public void A_turned_crate_drops_into_a_gap_it_fits_only_turned()
    {
        var ship = OneRack(3, 2, 1);
        var saved = WithCrates(Planned(ship, Item(4, CrateSize.Two)), Riker(CrateSize.Two, 0, 0, 0), Riker(CrateSize.Two, 0, 1, 0));
        var second = saved.Plan.Crates[1];

        var outcome = LoadPlanner.ResolveMove(saved.Plan, ship, [new CrateMove(second, "rack-0", new GridPosition(2, 0, 0), Turn: true)]);

        outcome.Stage.Should().Be(MoveStage.Gravity);
        outcome.Plan!.Crates.Should().Equal(saved.Plan.Crates[0], second with { Position = new GridPosition(2, 0, 0), Rotated = true });
    }

    [Fact]
    public void A_selection_keeps_every_crates_orientation()
    {
        var ship = OneRack(4, 2, 1);
        var saved = WithCrates(Planned(ship, Item(4, CrateSize.Two)), Riker(CrateSize.Two, 0, 0, 0, rotated: true), Riker(CrateSize.Two, 1, 0, 0));
        var (turned, flat) = (saved.Plan.Crates[0], saved.Plan.Crates[1]);

        var moves = CrateMove.Shift(ship, [turned, flat], turned, "rack-0", new GridPosition(1, 0, 0))!;
        var moved = LoadPlanner.MoveCrates(saved.Plan, ship, moves);

        moves.Should().OnlyContain(m => !m.Turn);
        moved.Crates.Should().Equal(turned with { Position = new GridPosition(1, 0, 0) }, flat with { Position = new GridPosition(2, 0, 0) });
    }

    // --- Split and merge ---

    [Fact]
    public void A_turned_32_splits_into_turned_16s_and_merges_back()
    {
        var ship = OneRack(2, 8, 2);
        var saved = Planned(ship, Item(32, CrateSize.ThirtyTwo));
        saved.Plan.Crates.Single().Rotated.Should().BeTrue();

        var split = LoadPlanner.SplitCrate(saved, ship, saved.Plan.Crates.Single());

        split.Plan.Crates.Should().HaveCount(2).And.OnlyContain(c => c.CrateSize == CrateSize.Sixteen && c.Rotated);
        LoadPlanner.MergeCrates(split, ship, split.Plan.Crates).Plan.Crates.Should().ContainSingle()
            .Which.Should().Match<Crate>(c => c.CrateSize == CrateSize.ThirtyTwo && c.Rotated && c.Position == new GridPosition(0, 0, 0));
    }

    // Two turned 2s side by side are a 2×2×1 block: a square 4, which doesn't turn.
    [Fact]
    public void Two_turned_2s_merge_into_a_4()
    {
        var ship = OneRack(2, 2, 1);
        var saved = WithCrates(Planned(ship, Item(4, CrateSize.Four, CrateSize.Two, CrateSize.Two)),
            Riker(CrateSize.Two, 0, 0, 0, rotated: true), Riker(CrateSize.Two, 1, 0, 0, rotated: true));

        var merged = LoadPlanner.MergeCrates(saved, ship, saved.Plan.Crates).Plan.Crates.Should().ContainSingle().Which;

        merged.CrateSize.Should().Be(CrateSize.Four);
        merged.Rotated.Should().BeFalse();
    }

    // --- Plan file schema 5 ---

    [Fact]
    public void Write_writes_rotated_only_where_true()
    {
        var ship = OneRack(3, 2, 1);
        var node = JsonNode.Parse(Written(Planned(ship, Item(6, CrateSize.Two))))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(PlanFile.SupportedSchemaVersion);
        var crates = node["crates"]!.AsArray().Select(c => c!.AsObject()).ToList();
        crates.Count(c => c.ContainsKey("rotated")).Should().Be(1);
        crates.Single(c => c.ContainsKey("rotated"))["rotated"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Write_then_Read_keeps_a_turned_crate()
    {
        var saved = Planned(Perseus, Item(96, CrateSize.ThirtyTwo));

        Read(Written(saved), Perseus).Plan.Crates.Should().Equal(saved.Plan.Crates);
    }

    [Fact]
    public void Schema_4_reads_with_every_crate_unturned()
    {
        var node = JsonNode.Parse(Written(Planned(HullA, Item(8, CrateSize.Four))))!;
        node["schemaVersion"] = 4;

        var saved = Read(node.ToJsonString(), HullA);

        saved.Plan.Crates.Should().OnlyContain(c => !c.Rotated);
        saved.CrateCap.Should().Be(CrateCap.Of(HullA));
    }

    [Fact]
    public void Schema_4_with_rotated_is_broken()
    {
        var node = JsonNode.Parse(Written(Planned(Perseus, Item(96, CrateSize.ThirtyTwo))))!;
        node["schemaVersion"] = 4;

        var act = () => Read(node.ToJsonString(), Perseus);

        act.Should().Throw<PlanFormatException>().WithMessage("*rotated*schema 5*");
    }

    [Fact]
    public void A_turned_square_crate_in_the_file_is_broken()
    {
        var node = JsonNode.Parse(Written(Planned(HullA, Item(8, CrateSize.Four))))!;
        node["crates"]![0]!["rotated"] = true;

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*4 SCU*turn*");
    }

    [Fact]
    public void Schema_6_is_refused()
    {
        var node = JsonNode.Parse(Written(Planned(HullA, Item(8, CrateSize.Four))))!;
        node["schemaVersion"] = 6;

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*schemaVersion 6 is not supported*");
    }

    // --- Racks that changed since the file was written (K1): planned anew, whatever the schema ---

    /// <summary>
    /// A Freelancer plan as written before its side racks went back to Fleetyards' orientation (K3.2): a 2 at [0, 0, 0]
    /// of rack-0 lay along the length, which is 1 cell now.
    /// </summary>
    private static JsonNode OldFreelancerFile(int schemaVersion)
    {
        var saved = Planned(Freelancer, Item(6, CrateSize.Two));
        var node = JsonNode.Parse(Written(WithCrates(saved,
            Riker(CrateSize.Two, 0, 0, 0, rackId: "rack-2"), Riker(CrateSize.Two, 2, 0, 0, rackId: "rack-2"), Riker(CrateSize.Two, 0, 0, 0, rackId: "rack-0"))))!;
        node["schemaVersion"] = schemaVersion;
        foreach (var crate in node["crates"]!.AsArray())
        {
            crate!.AsObject().Remove("rotated");
        }

        return node;
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void A_crate_outside_todays_rack_reads_as_racks_changed_and_is_planned_anew(int schemaVersion)
    {
        var saved = Read(OldFreelancerFile(schemaVersion).ToJsonString(), Freelancer);

        saved.CrateCap.Reason.Should().Be(CapReason.RacksChanged);
        saved.CrateCap.Should().NotBe(CrateCap.Of(Freelancer));
        saved.CratesCapped.Should().BeFalse();

        var replanned = LoadPlanner.Replan(saved, Freelancer);
        replanned.CrateCap.Should().Be(CrateCap.Of(Freelancer));
        replanned.Missions.Should().OnlyContain(m => !m.Unplaced);
        LoadPlanner.Validate(replanned.Plan, Freelancer).Should().BeEmpty();
    }

    [Fact]
    public void A_stop_ticked_as_a_whole_keeps_its_tick_when_the_racks_changed()
    {
        var node = OldFreelancerFile(5);
        foreach (var crate in node["crates"]!.AsArray())
        {
            crate!["delivered"] = true;
        }

        var replanned = LoadPlanner.Replan(Read(node.ToJsonString(), Freelancer), Freelancer);

        replanned.Plan.Crates.Should().NotBeEmpty().And.OnlyContain(c => c.Delivered);
    }

    [Fact]
    public void Schema_5_with_every_crate_inside_loads_as_written()
    {
        var saved = Planned(Freelancer, Item(66, CrateSize.Two));

        var read = Read(Written(saved), Freelancer);

        read.CrateCap.Should().Be(CrateCap.Of(Freelancer));
        read.Plan.Crates.Should().Equal(saved.Plan.Crates);
    }
}
