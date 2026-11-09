using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The cell order is stack &gt; progress axis &gt; row axis (D88): <see cref="FillRows"/> picks the row axis,
/// <see cref="FillOrder"/> the direction along the length. Lengthwise is what the planner did before; crosswise is new,
/// and a rack stacking along ±y ignores it.
/// </summary>
public class FillRowsTests
{
    private static readonly Ship Raft = FrozenShips.Load("raft");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");
    private static readonly Ship HullC = FrozenShips.Load("hull_c");

    private static Rack Flat(int length, int width, int stack, StackAxis axis = StackAxis.PlusZ, string id = "flat", int index = 0) =>
        new(id, index, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0), axis);

    /// <summary>Layer 0 of a rank table, written as the picture: width 2 on top, length 0 on the left.</summary>
    private static int[,] Layer0(int[,,] rank)
    {
        var picture = new int[rank.GetLength(1), rank.GetLength(0)];
        for (var w = 0; w < rank.GetLength(1); w++)
        {
            for (var l = 0; l < rank.GetLength(0); l++)
            {
                picture[rank.GetLength(1) - 1 - w, l] = rank[l, w, 0];
            }
        }

        return picture;
    }

    // --- The cell order ---

    [Fact]
    public void Lengthwise_bow_first_runs_rows_along_the_length_and_steps_across()
    {
        Layer0(CrateLayout.Ranks(Flat(4, 3, 2), FillOrder.BowFirst, FillRows.Lengthwise)).Should().BeEquivalentTo(new[,]
        {
            { 11, 10, 9, 8 },
            { 7, 6, 5, 4 },
            { 3, 2, 1, 0 },
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Crosswise_bow_first_runs_rows_across_and_steps_from_the_bow()
    {
        Layer0(CrateLayout.Ranks(Flat(4, 3, 2), FillOrder.BowFirst, FillRows.Crosswise)).Should().BeEquivalentTo(new[,]
        {
            { 11, 8, 5, 2 },
            { 10, 7, 4, 1 },
            { 9, 6, 3, 0 },
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Crosswise_stern_first_runs_rows_across_and_steps_from_the_stern()
    {
        Layer0(CrateLayout.Ranks(Flat(4, 3, 2), FillOrder.SternFirst, FillRows.Crosswise)).Should().BeEquivalentTo(new[,]
        {
            { 2, 5, 8, 11 },
            { 1, 4, 7, 10 },
            { 0, 3, 6, 9 },
        }, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Crosswise_fills_layer_0_before_layer_1(FillOrder order)
    {
        var rank = CrateLayout.Ranks(Flat(4, 3, 2), order, FillRows.Crosswise);

        rank[0, 0, 1].Should().BeGreaterThanOrEqualTo(12);
        Enumerable.Range(0, 4).SelectMany(l => Enumerable.Range(0, 3).Select(w => rank[l, w, 0])).Should().OnlyContain(r => r < 12);
    }

    [Theory]
    [InlineData(StackAxis.PlusZ)]
    [InlineData(StackAxis.MinusZ)]
    [InlineData(StackAxis.PlusX)]
    [InlineData(StackAxis.MinusX)]
    public void Crosswise_applies_to_every_rack_not_stacking_along_y(StackAxis axis)
    {
        CrateLayout.Ranks(Flat(4, 3, 2, axis), FillOrder.BowFirst, FillRows.Crosswise)
            .Should().BeEquivalentTo(CrateLayout.Ranks(Flat(4, 3, 2), FillOrder.BowFirst, FillRows.Crosswise), o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(StackAxis.PlusY, FillOrder.BowFirst)]
    [InlineData(StackAxis.MinusY, FillOrder.BowFirst)]
    [InlineData(StackAxis.PlusY, FillOrder.SternFirst)]
    [InlineData(StackAxis.MinusY, FillOrder.SternFirst)]
    public void A_rack_stacking_along_y_ignores_crosswise(StackAxis axis, FillOrder order)
    {
        var rack = Flat(4, 3, 2, axis);

        CrateLayout.Ranks(rack, order, FillRows.Crosswise).Should().BeEquivalentTo(CrateLayout.Ranks(rack, order, FillRows.Lengthwise), o => o.WithStrictOrdering());
    }

    [Fact]
    public void Crosswise_keeps_the_largest_crates_first_across_destinations()
    {
        // Grouping is K9.2: without it the 4s of both destinations come before the 2 listed first, all in the bow's cross row.
        (CrateSize Size, string Destination)[] crates = [(CrateSize.Two, "Green"), (CrateSize.Four, "Thunder"), (CrateSize.Four, "Green")];

        var poses = CrateLayout.Place(Flat(12, 8, 2), [], crates, FillOrder.BowFirst, FillRows.Crosswise)!;

        poses.Select(p => p.Position).Should().Equal(new GridPosition(10, 4, 0), new GridPosition(10, 0, 0), new GridPosition(10, 2, 0));
    }

    // --- The mirror: square footprints only, an oblong crate lies along the grid's length unturned ---

    // 4 × 6 × 3 takes about 5 s per mirror and runs as Slow below (K10, F26).
    public static TheoryData<int, int, int> Grids => new()
    {
        { 12, 8, 2 },
        { 5, 3, 2 },
        { 7, 7, 2 },
    };

    /// <summary>Seeded mixes of 1, 4 and 8 SCU crates for two destinations, up to the rack's volume.</summary>
    private static IEnumerable<(CrateSize Size, string Destination)[]> SquareMixes(int volume)
    {
        var random = new Random(88);
        CrateSize[] sizes = [CrateSize.One, CrateSize.Four, CrateSize.Eight];
        for (var i = 0; i < 40; i++)
        {
            List<(CrateSize, string)> mix = [];
            var left = random.Next(1, volume + 1);
            while (left > 0)
            {
                var size = sizes[random.Next(sizes.Length)];
                if ((int)size <= left)
                {
                    mix.Add((size, random.Next(2) == 0 ? "Green" : "Thunder"));
                    left -= (int)size;
                }
            }

            yield return [.. mix];
        }
    }

    /// <summary>
    /// Stern first: the transposed grid with the other row axis lays the same crates, length and width swapped. The rank
    /// tables are each other's transposition, so nothing else differs.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grids))]
    public void Stern_first_crosswise_is_the_transposition_of_lengthwise(int length, int width, int stack) =>
        SternFirstIsTransposed(length, width, stack);

    [Fact]
    [Trait("Category", "Slow")]
    public void Stern_first_crosswise_is_the_transposition_of_lengthwise_on_a_grid_three_high() =>
        SternFirstIsTransposed(4, 6, 3);

    private static void SternFirstIsTransposed(int length, int width, int stack)
    {
        foreach (var mix in SquareMixes(length * width * stack))
        {
            var lengthwise = CrateLayout.Place(Flat(length, width, stack), [], mix, FillOrder.SternFirst, FillRows.Lengthwise);
            var crosswise = CrateLayout.Place(Flat(width, length, stack), [], mix, FillOrder.SternFirst, FillRows.Crosswise);

            crosswise?.Select(p => p.Position).Should().Equal(lengthwise?.Select(p => new GridPosition(p.Position.Width, p.Position.Length, p.Position.Stack)));
            (crosswise is null).Should().Be(lengthwise is null);
        }
    }

    /// <summary>
    /// Bow first: transposed and reflected in both axes. Lengthwise steps across from starboard and runs from the bow,
    /// crosswise steps from the bow and runs from starboard, so each axis counts the other way.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grids))]
    public void Bow_first_crosswise_is_the_reflected_transposition_of_lengthwise(int length, int width, int stack) =>
        BowFirstIsReflectedTransposed(length, width, stack);

    [Fact]
    [Trait("Category", "Slow")]
    public void Bow_first_crosswise_is_the_reflected_transposition_of_lengthwise_on_a_grid_three_high() =>
        BowFirstIsReflectedTransposed(4, 6, 3);

    private static void BowFirstIsReflectedTransposed(int length, int width, int stack)
    {
        foreach (var mix in SquareMixes(length * width * stack))
        {
            var lengthwise = CrateLayout.Place(Flat(length, width, stack), [], mix, FillOrder.BowFirst, FillRows.Lengthwise);
            var crosswise = CrateLayout.Place(Flat(width, length, stack), [], mix, FillOrder.BowFirst, FillRows.Crosswise);

            (crosswise is null).Should().Be(lengthwise is null);
            if (lengthwise is not null)
            {
                crosswise!.Select(p => p.Position).Should().Equal(lengthwise.Zip(mix).Select(p =>
                {
                    var side = p.Second.Size.Dimensions.Length;
                    return new GridPosition(width - p.First.Position.Width - side, length - p.First.Position.Length - side, p.First.Position.Stack);
                }));
            }
        }
    }

    // --- Capacity: mixes with oblong crates, no mirror to lean on ---

    /// <summary>Seeded mixes of every size the ship's first rack takes, close to its volume, where a greedy order would show.</summary>
    [Theory]
    [InlineData("raft", FillOrder.BowFirst)]
    [InlineData("raft", FillOrder.SternFirst)]
    [InlineData("freelancer", FillOrder.BowFirst)]
    [InlineData("freelancer", FillOrder.SternFirst)]
    public void What_lays_out_lengthwise_also_lays_out_crosswise(string shipId, FillOrder order)
    {
        var rack = FrozenShips.Load(shipId).Racks[0];
        CrateSize[] sizes = [.. Enum.GetValues<CrateSize>().Where(rack.CanHold)];
        var random = new Random(89);
        List<string> failed = [];
        var laid = 0;
        for (var i = 0; i < 300; i++)
        {
            List<CrateSize> mix = [];
            var left = random.Next(rack.CapacityScu / 2, rack.CapacityScu + 1);
            while (left > 0)
            {
                var size = sizes[random.Next(sizes.Length)];
                if ((int)size <= left)
                {
                    mix.Add(size);
                    left -= (int)size;
                }
            }

            (CrateSize, string)[] crates = [.. mix.Select(s => (s, "Riker"))];
            if (CrateLayout.Place(rack, [], crates, order, FillRows.Lengthwise) is null)
            {
                continue;
            }

            laid++;
            if (CrateLayout.Place(rack, [], crates, order, FillRows.Crosswise) is null)
            {
                failed.Add(string.Join("+", mix.Select(s => (int)s)));
            }
        }

        laid.Should().BeGreaterThan(100, "the test has to see mixes that lay out");
        failed.Should().BeEmpty();
    }

    // --- Ships ---

    [Theory]
    [InlineData("example_missions", FillOrder.BowFirst)]
    [InlineData("example_missions", FillOrder.SternFirst)]
    [InlineData("split_and_shared", FillOrder.BowFirst)]
    [InlineData("split_required", FillOrder.BowFirst)]
    [InlineData("crate_mix", FillOrder.BowFirst)]
    [InlineData("hull_a_eight_stops", FillOrder.BowFirst)]
    [InlineData("hull_a_eight_stops", FillOrder.SternFirst)]
    public void The_Hull_A_plans_the_same_crosswise(string fixture, FillOrder order)
    {
        var stops = MissionFixtures.Load(fixture);
        var lengthwise = LoadPlanner.Plan(HullA, stops, order, FillRows.Lengthwise);
        var crosswise = LoadPlanner.Plan(HullA, stops, order, FillRows.Crosswise);

        crosswise.FillRows.Should().Be(FillRows.Crosswise);
        crosswise.Crates.Should().Equal(lengthwise.Crates);
        crosswise.Warnings.Should().Equal(lengthwise.Warnings);
    }

    [Fact]
    public void The_Hull_C_applies_crosswise_rack_by_rack()
    {
        string[] destinations = ["Riker", "Shubin"];
        Stop[] stops = [.. destinations.Select((d, i) => new Stop(d, i + 1, [new CargoItem("Titanium", 320, CrateSize.ThirtyTwo)]))];

        var plan = LoadPlanner.Plan(HullC, stops, FillOrder.BowFirst, FillRows.Crosswise);

        foreach (var rack in HullC.Racks)
        {
            Crate[] crates = [.. plan.CratesForRack(rack.Id)];
            if (crates.Length > 0)
            {
                var alone = CrateLayout.Lay(rack, [], [.. crates.Select(c => (c.CrateSize, c.Destination))], FillOrder.BowFirst, FillRows.Crosswise)!;
                crates.Select(c => c.Position).Order(Comparer<GridPosition>.Create((a, b) => string.CompareOrdinal(a.ToString(), b.ToString())))
                    .Should().Equal(alone.Poses.Select(p => p.Position).Order(Comparer<GridPosition>.Create((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()))));
            }
        }
    }

    /// <summary>
    /// Racks of one grid are alike to the search, which keeps one layout for both (D64). Crosswise they aren't alike if only
    /// one of them stacks along y: each must lie in its own order.
    /// </summary>
    [Fact]
    public void Racks_alike_but_for_the_stack_axis_lay_in_their_own_order_crosswise()
    {
        Rack up = Flat(4, 4, 1, StackAxis.PlusZ, "up", 0);
        Rack side = Flat(4, 4, 1, StackAxis.PlusY, "side", 1);
        Ship ship = new("twins", "Twins", 32, [up, side]);
        Stop[] stops = [new("Riker", 1, [new CargoItem("Titanium", 16, CrateSize.One)]), new("Shubin", 2, [new CargoItem("Quartz", 2, CrateSize.One)])];

        var plan = LoadPlanner.Plan(ship, stops, FillOrder.BowFirst, FillRows.Crosswise);

        plan.CratesForRack("up").Select(c => c.Position).First().Should().Be(new GridPosition(3, 0, 0));
        plan.CratesForRack("up").Select(c => c.Position).Skip(1).First().Should().Be(new GridPosition(3, 1, 0));
        plan.CratesForRack("side").Select(c => c.Position).Skip(1).First().Should().Be(new GridPosition(2, 0, 0));
    }

    // --- Every path keeps it ---

    private static readonly Stop[] RaftStops =
    [
        new("Green", 1, [new CargoItem("Food", 20, CrateSize.Four)]),
        new("Thunder", 2, [new CargoItem("Ice", 12, CrateSize.Four)]),
    ];

    private static IReadOnlyList<Mission> RaftMissions()
    {
        Mission[] missions =
        [
            new(1, [new MissionStop("Green", 1, [new MissionItem("Food", 20, CrateSize.Four)])]),
            new(2, [new MissionStop("Thunder", 1, [new MissionItem("Ice", 12, CrateSize.Four)])]),
        ];
        return missions;
    }

    private static string[] Layout(LoadPlan plan) =>
        [.. plan.Crates.Select(c => $"{c.Destination}|{(int)c.CrateSize}|{c.RackId}|{c.Position}").Order(StringComparer.Ordinal)];

    private static SavedPlan Crosswise() => LoadPlanner.Plan(Raft, RaftMissions(), ["Green", "Thunder"], FillOrder.BowFirst, fillRows: FillRows.Crosswise);

    [Fact]
    public void The_plan_lays_out_crosswise_and_says_so()
    {
        var saved = Crosswise();

        saved.Plan.FillRows.Should().Be(FillRows.Crosswise);
        Layout(saved.Plan).Should().NotEqual(Layout(LoadPlanner.Plan(Raft, RaftMissions(), ["Green", "Thunder"], FillOrder.BowFirst).Plan));
        Layout(saved.Plan).Should().Equal(Layout(LoadPlanner.Plan(Raft, RaftStops, FillOrder.BowFirst, FillRows.Crosswise)));
    }

    [Fact]
    public void Replan_on_the_same_ship_stays_crosswise()
    {
        var saved = Crosswise();

        var replanned = LoadPlanner.Replan(saved, Raft);

        replanned.Plan.FillRows.Should().Be(FillRows.Crosswise);
        Layout(replanned.Plan).Should().Equal(Layout(saved.Plan));
    }

    [Fact]
    public void Replan_with_loaded_cargo_stays_crosswise()
    {
        var saved = Crosswise();
        var loaded = new SavedPlan(saved.Missions, LoadPlanner.SetLoaded(saved.Plan, Raft, "Green", 1, true), saved.CrateCap, saved.Watermark);

        var replanned = LoadPlanner.Replan(loaded, Raft);

        replanned.Plan.FillRows.Should().Be(FillRows.Crosswise);
        Layout(replanned.Plan).Should().Equal(Layout(saved.Plan));
    }

    [Fact]
    public void A_new_ship_keeps_crosswise()
    {
        var freelancer = FrozenShips.Load("freelancer");

        var replanned = LoadPlanner.Replan(Crosswise(), freelancer);

        replanned.Plan.FillRows.Should().Be(FillRows.Crosswise);
        Layout(replanned).Should().Equal(Layout(LoadPlanner.Plan(freelancer, RaftMissions(), ["Green", "Thunder"], FillOrder.BowFirst, fillRows: FillRows.Crosswise).Plan));
    }

    private static string[] Layout(SavedPlan saved) => Layout(saved.Plan);

    [Fact]
    public void A_captured_mission_lies_in_the_next_cross_row()
    {
        var saved = Crosswise();

        var placed = LoadPlanner.PlaceInto(saved, Raft, [new MissionStop("Melodic", 1, [new MissionItem("Stims", 1, CrateSize.One)])]);

        placed.Plan.FillRows.Should().Be(FillRows.Crosswise);
        // 32 SCU in 4s: eight footprints from the bow, two cross rows of four; the next free cell in rank order is length 7.
        placed.Plan.Crates.Single(c => c.Destination == "Melodic").Position.Should().Be(new GridPosition(7, 0, 0));
    }

    [Fact]
    public void Discard_keeps_crosswise()
    {
        LoadPlanner.Discard(Crosswise(), Raft).Plan.FillRows.Should().Be(FillRows.Crosswise);
    }

    [Fact]
    public void Ticks_splits_and_moves_keep_crosswise()
    {
        var saved = Crosswise();
        var crate = saved.Plan.Crates[0];

        LoadPlanner.SetDelivered(saved.Plan, Raft, "Green", true).FillRows.Should().Be(FillRows.Crosswise);
        LoadPlanner.SplitCrate(saved, Raft, crate).Plan.FillRows.Should().Be(FillRows.Crosswise);
        PlannerMoves.MoveCrate(saved.Plan, Raft, crate, crate.RackId, new GridPosition(0, 7, 0)).FillRows.Should().Be(FillRows.Crosswise);
    }

    [Fact]
    public void A_crate_split_where_it_lies_fills_its_footprint_crosswise()
    {
        var saved = Crosswise();
        var crate = saved.Plan.Crates.First(c => c.CrateSize == CrateSize.Four);

        var split = LoadPlanner.SplitCrate(saved, Raft, crate, [CrateSize.Two, CrateSize.One, CrateSize.One]);

        var (l, w) = (crate.Position.Length, crate.Position.Width);
        var parts = split.Plan.Crates.Where(c => c.Destination == crate.Destination && c.CrateSize != CrateSize.Four).ToArray();
        parts.SelectMany(c => c.Cells).Should().BeEquivalentTo(crate.Cells);
        parts.Single(c => c.CrateSize == CrateSize.Two).Position.Should().Be(new GridPosition(l, w, crate.Position.Stack));
    }
}
