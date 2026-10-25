using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Split and merge (D27): the plan follows the crates the kiosk really delivered. Bow first in Rack 1 of the Hull A, a
/// lone crate of 4 SCU or more lies at [2, 0, 0]; 2 SCU crates of one item lie at [2, 0, 0] and [0, 0, 0], in a row.
/// </summary>
public class SplitMergeTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    /// <summary>One 8×3×2 rack (48 SCU): room for the 24 and 32 SCU crates (6×2×2, 8×2×2, D57) no Hull A rack holds.</summary>
    private static readonly Ship BigRackShip = new("big_rack", "Big rack test ship", 48,
    [
        new Rack("rack-0", 0, 48, new Dimensions(8, 3, 2), new RackPosition(RackSide.Port, RackLevel.Upper), new ShipPoint(0, 0, 0)),
    ]);

    private static MissionItem Item(string commodity, int scu, CrateSize max, params CrateSize[] crates) =>
        new(commodity, scu, max, crates.Length == 0 ? null : crates);

    /// <summary>One stop, Riker, with these items, planned.</summary>
    private static SavedPlan Riker(Ship ship, params MissionItem[] items) => Saved(ship, new MissionStop("Riker", 1, items));

    private static SavedPlan Saved(Ship ship, params MissionStop[] missions) => SavedPlan.FromStops(missions, LoadPlanner.Plan(ship, missions.ToStops()), CrateCap.Of(ship));

    private static Crate CrateAt(SavedPlan saved, string rackId, int length, int width, int stack) =>
        saved.Plan.Crates.Single(c => c.RackId == rackId && c.Position == new GridPosition(length, width, stack));

    private static Crate Crate(CrateSize size, int length, int width, int stack, string commodity = "Titanium") =>
        new("Riker", commodity, size, "rack-0", new GridPosition(length, width, stack), Mission: 1);

    /// <summary>The crates where the player moved them in the overlay (D25); the missions stay.</summary>
    private static SavedPlan Moved(SavedPlan saved, Ship ship, params (Crate Crate, string RackId, GridPosition Position)[] moves) =>
        new(saved.Missions, LoadPlanner.MoveCrates(saved.Plan, ship, [.. moves.Select(m => new CrateMove(m.Crate, m.RackId, m.Position))]), saved.CrateCap);

    /// <summary>Every plan split and merge make is laid out correctly (D24).</summary>
    private static void ShouldBeValid(SavedPlan saved, Ship ship) => LoadPlanner.Validate(saved.Plan, ship).Should().BeEmpty();

    // --- Split ---

    [Fact]
    public void Split_of_a_4_gives_two_2s_side_by_side_in_its_footprint()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));

        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[0]);

        split.Plan.Crates.Should().Equal(Crate(CrateSize.Two, 2, 0, 0), Crate(CrateSize.Two, 2, 1, 0));
        ShouldBeValid(split, HullA);
    }

    [Fact]
    public void Split_of_an_8_gives_two_4s_on_top_of_each_other()
    {
        var saved = Riker(HullA, Item("Agricium", 8, CrateSize.Eight));

        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[0]);

        split.Plan.Crates.Should().Equal(Crate(CrateSize.Four, 2, 0, 0, "Agricium"), Crate(CrateSize.Four, 2, 0, 1, "Agricium"));
        ShouldBeValid(split, HullA);
    }

    [Theory]
    [InlineData(false, 16, 8, 8)]
    [InlineData(true, 24, 16, 8)]
    [InlineData(true, 32, 16, 16)]
    public void Split_lays_the_default_parts_into_the_cells_of_the_crate(bool bigRack, int size, params int[] parts)
    {
        var ship = bigRack ? BigRackShip : HullA;
        var saved = Riker(ship, Item("Titanium", size, (CrateSize)size));
        var crate = saved.Plan.Crates.Single();

        var split = LoadPlanner.SplitCrate(saved, ship, crate);

        split.Plan.Crates.Select(c => (int)c.CrateSize).Should().Equal(parts);
        split.Plan.Crates.SelectMany(c => c.Cells).Should().BeEquivalentTo(crate.Cells);
        ShouldBeValid(split, ship);
    }

    // Every crate from 8 SCU up is n × 2 × 2 cells (D57), so every split of a crate fits its footprint (a 32 has 211).
    // SplitProblem keeps the footprint check for crate shapes to come.
    [Theory]
    [InlineData(CrateSize.Two)]
    [InlineData(CrateSize.Four)]
    [InlineData(CrateSize.Eight)]
    [InlineData(CrateSize.Sixteen)]
    [InlineData(CrateSize.TwentyFour)]
    [InlineData(CrateSize.ThirtyTwo)]
    public void Every_split_of_a_crate_fits_its_footprint(CrateSize size)
    {
        var saved = Riker(BigRackShip, Item("Titanium", (int)size, size));
        var crate = saved.Plan.Crates.Single();
        CrateSize[] smaller = [.. Enum.GetValues<CrateSize>().Where(s => s < size).OrderDescending()];

        Splits((int)size, smaller).Should().NotBeEmpty()
            .And.OnlyContain(parts => LoadPlanner.SplitProblem(saved, BigRackShip, crate, parts) == null);
    }

    /// <summary>Every multiset of <paramref name="sizes"/> (largest first) that adds up to <paramref name="scu"/>.</summary>
    private static IEnumerable<CrateSize[]> Splits(int scu, CrateSize[] sizes, int from = 0)
    {
        if (scu == 0)
        {
            yield return [];
            yield break;
        }

        for (var i = from; i < sizes.Length; i++)
        {
            if ((int)sizes[i] > scu)
            {
                continue;
            }

            foreach (var rest in Splits(scu - (int)sizes[i], sizes, i))
            {
                yield return [sizes[i], .. rest];
            }
        }
    }

    [Fact]
    public void Split_refuses_a_1_SCU_crate()
    {
        var saved = Riker(HullA, Item("Gold", 1, CrateSize.One));

        var act = () => LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[0]);

        act.Should().Throw<PlanException>().WithMessage("1 SCU crates can't be split.");
    }

    [Theory]
    [InlineData(new[] { 2, 1 }, "2 + 1 SCU don't add up to 4 SCU.")]
    [InlineData(new[] { 4 }, "A 4 SCU crate splits into two or more crates.")]
    [InlineData(new[] { 3, 1 }, "3 SCU is not a crate size.")]
    public void Split_refuses_parts_that_are_no_split_of_the_crate(int[] parts, string reason)
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));

        var act = () => LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[0], [.. parts.Select(p => (CrateSize)p)]);

        act.Should().Throw<PlanException>().WithMessage(reason);
    }

    [Fact]
    public void Split_keeps_the_crates_on_top_supported()
    {
        // Four 4s fill Rack 1; the lower one at [2, 0, 0] carries the one at [2, 0, 1].
        var saved = Riker(HullA, Item("Titanium", 16, CrateSize.Four));
        ShouldBeValid(saved, HullA);

        var split = LoadPlanner.SplitCrate(saved, HullA, CrateAt(saved, "rack-0", 2, 0, 0));

        ShouldBeValid(split, HullA);
        split.Plan.Crates.Where(c => c.CrateSize == CrateSize.Four).Should().Equal(saved.Plan.Crates.Skip(1));
    }

    [Fact]
    public void Split_puts_the_parts_where_the_crate_was_in_the_plan()
    {
        var saved = Riker(HullA, Item("Titanium", 16, CrateSize.Four));

        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[1]);

        split.Plan.Crates.Select(c => c.CrateSize).Should().Equal(
            CrateSize.Four, CrateSize.Two, CrateSize.Two, CrateSize.Four, CrateSize.Four);
        split.Plan.Placements.Select(p => p.Item).Should().Equal(
            new CargoItem("Titanium", 12, CrateSize.Four), new CargoItem("Titanium", 4, CrateSize.Two));
    }

    /// <summary>Riker 4 Titanium twice, as two captures (D42): two missions to one stop.</summary>
    private static SavedPlan TwoMissions(CrateSize max)
    {
        MissionStop stop = new("Riker", 1, [new MissionItem("Titanium", 4, max)]);
        SavedPlan empty = new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));
        return LoadPlanner.PlaceInto(LoadPlanner.PlaceInto(empty, HullA, [stop]), HullA, [stop]);
    }

    [Fact]
    public void Split_keeps_the_mission_of_the_crate()
    {
        var saved = TwoMissions(CrateSize.Four);
        var second = saved.Plan.Crates.Single(c => c.Mission == 2);

        var split = LoadPlanner.SplitCrate(saved, HullA, second);

        split.Plan.Crates.Count(c => c.Mission == 2).Should().Be(2);
        split.MissionOf(2).Stops[0].Items[0].Crates.Should().Equal(CrateSize.Two, CrateSize.Two);
        split.MissionOf(1).Should().BeSameAs(saved.MissionOf(1));
    }

    [Fact]
    public void Merge_refuses_crates_of_two_missions()
    {
        var saved = TwoMissions(CrateSize.Eight);

        LoadPlanner.MergeProblem(saved, HullA, saved.Plan.Crates).Should().Be("The crates belong to different missions.");
    }

    [Fact]
    public void Split_rebooks_the_contract_item()
    {
        var saved = Riker(HullA, Item("Titanium", 12, CrateSize.Four), Item("Quartz", 4, CrateSize.Two));

        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[0]);

        split.Missions[0].Stops[0].Items[0].Crates.Should().Equal(CrateSize.Four, CrateSize.Four, CrateSize.Two, CrateSize.Two);
        split.Missions[0].Stops[0].Items[1].Should().BeSameAs(saved.Missions[0].Stops[0].Items[1]);
    }

    [Fact]
    public void Split_leaves_the_original_unchanged()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));
        Crate[] before = [.. saved.Plan.Crates];

        LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[0]);

        saved.Plan.Crates.Should().Equal(before);
        saved.Missions[0].Stops[0].Items[0].Crates.Should().BeNull();
    }

    [Fact]
    public void SplitProblem_is_null_for_a_split_that_works()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));

        LoadPlanner.SplitProblem(saved, HullA, saved.Plan.Crates[0]).Should().BeNull();
    }

    [Fact]
    public void Split_rejects_a_delivered_crate()
    {
        // Delivered crates can't be selected (D25), so only a bug gets here.
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));
        SavedPlan delivered = new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "Riker", true), saved.CrateCap);

        var act = () => LoadPlanner.SplitCrate(delivered, HullA, delivered.Plan.Crates[0]);

        act.Should().Throw<ArgumentException>().WithParameterName("crate").WithMessage("*delivered*");
    }

    [Fact]
    public void Split_rejects_a_crate_not_in_the_plan()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));

        var act = () => LoadPlanner.SplitCrate(saved, HullA, Crate(CrateSize.Four, 0, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("crate");
    }

    [Fact]
    public void Split_rejects_a_plan_for_another_ship()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));

        var act = () => LoadPlanner.SplitCrate(saved, BigRackShip, saved.Plan.Crates[0]);

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    // --- Merge ---

    [Fact]
    public void Merge_of_two_2s_side_by_side_gives_a_4_and_drops_the_override()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));
        var moved = Moved(saved, HullA, (CrateAt(saved, "rack-0", 0, 0, 0), "rack-0", new GridPosition(2, 1, 0)));

        var merged = LoadPlanner.MergeCrates(moved, HullA, moved.Plan.Crates);

        merged.Plan.Crates.Should().Equal(Crate(CrateSize.Four, 2, 0, 0));
        merged.Missions[0].Stops[0].Items[0].Crates.Should().BeNull();
        ShouldBeValid(merged, HullA);
    }

    [Fact]
    public void Merge_of_two_4s_on_top_of_each_other_gives_an_8()
    {
        var saved = Riker(HullA, Item("Agricium", 8, CrateSize.Eight, CrateSize.Four, CrateSize.Four));
        var moved = Moved(saved, HullA, (CrateAt(saved, "rack-0", 0, 0, 0), "rack-0", new GridPosition(2, 0, 1)));

        var merged = LoadPlanner.MergeCrates(moved, HullA, moved.Plan.Crates);

        merged.Plan.Crates.Should().Equal(Crate(CrateSize.Eight, 2, 0, 0, "Agricium"));
        ShouldBeValid(merged, HullA);
    }

    [Fact]
    public void Merge_of_a_4_a_2_and_two_1s_stacked_as_a_cube_gives_an_8()
    {
        // Laid out: the 4 at [2, 0, 0], the 2 at [0, 0, 0], the 1s at [1, 1, 0] and [0, 1, 0]; all onto the 4.
        var saved = Riker(HullA, Item("Agricium", 8, CrateSize.Eight, CrateSize.Four, CrateSize.Two, CrateSize.One, CrateSize.One));
        var moved = Moved(saved, HullA,
            (CrateAt(saved, "rack-0", 0, 0, 0), "rack-0", new GridPosition(2, 0, 1)),
            (CrateAt(saved, "rack-0", 1, 1, 0), "rack-0", new GridPosition(2, 1, 1)),
            (CrateAt(saved, "rack-0", 0, 1, 0), "rack-0", new GridPosition(3, 1, 1)));

        var merged = LoadPlanner.MergeCrates(moved, HullA, moved.Plan.Crates);

        merged.Plan.Crates.Should().Equal(Crate(CrateSize.Eight, 2, 0, 0, "Agricium"));
        merged.Missions[0].Stops[0].Items[0].Crates.Should().BeNull();
        ShouldBeValid(merged, HullA);
    }

    [Fact]
    public void Merge_puts_the_crate_where_the_first_merged_one_was_in_the_plan()
    {
        // Titanium 4 + 2 + 2: the 4 at [2, 0, 0], the 2s at [0, 0, 0] and [0, 1, 0], side by side.
        var saved = Riker(HullA, Item("Titanium", 8, CrateSize.Four, CrateSize.Four, CrateSize.Two, CrateSize.Two), Item("Quartz", 2, CrateSize.Two));
        Crate[] twos = [.. saved.Plan.Crates.Where(c => c.Commodity == "Titanium" && c.CrateSize == CrateSize.Two)];

        var merged = LoadPlanner.MergeCrates(saved, HullA, twos);

        merged.Plan.Crates.Select(c => (c.Commodity, c.CrateSize)).Should().Equal(
            ("Titanium", CrateSize.Four), ("Titanium", CrateSize.Four), ("Quartz", CrateSize.Two));
    }

    [Fact]
    public void Merge_refuses_crates_that_do_not_touch()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));
        var moved = Moved(saved, HullA, (CrateAt(saved, "rack-0", 0, 0, 0), "rack-0", new GridPosition(0, 1, 0)));

        var act = () => LoadPlanner.MergeCrates(moved, HullA, moved.Plan.Crates);

        act.Should().Throw<PlanException>().WithMessage("The crates are not adjacent.");
    }

    [Fact]
    public void Merge_refuses_a_row_that_is_no_crate_shape()
    {
        // As laid out, the two 2s lie in one row: 4×1×1.
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));

        LoadPlanner.MergeProblem(saved, HullA, saved.Plan.Crates)
            .Should().Be("Together they would be a 4×1×1 crate, 4 SCU crates are 2×2×1.");
    }

    [Fact]
    public void Merge_refuses_a_standing_block()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));
        var moved = Moved(saved, HullA, (CrateAt(saved, "rack-0", 0, 0, 0), "rack-0", new GridPosition(2, 0, 1)));

        LoadPlanner.MergeProblem(moved, HullA, moved.Plan.Crates)
            .Should().Be("Together they would be a 2×1×2 crate, 4 SCU crates are 2×2×1.");
    }

    [Fact]
    public void Merge_refuses_different_commodities()
    {
        var saved = Riker(HullA, Item("Titanium", 2, CrateSize.Two), Item("Quartz", 2, CrateSize.Two));

        LoadPlanner.MergeProblem(saved, HullA, saved.Plan.Crates).Should().Be("The crates hold different commodities.");
    }

    [Fact]
    public void Merge_refuses_different_destinations()
    {
        var saved = Saved(HullA,
            new MissionStop("Riker", 1, [Item("Titanium", 2, CrateSize.Two)]),
            new MissionStop("Shubin", 2, [Item("Titanium", 2, CrateSize.Two)]));

        LoadPlanner.MergeProblem(saved, HullA, saved.Plan.Crates).Should().Be("The crates go to different destinations.");
    }

    [Fact]
    public void Merge_refuses_crates_in_different_racks()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));
        var moved = Moved(saved, HullA, (CrateAt(saved, "rack-0", 0, 0, 0), "rack-1", new GridPosition(0, 0, 0)));

        LoadPlanner.MergeProblem(moved, HullA, moved.Plan.Crates).Should().Be("The crates lie in different racks.");
    }

    [Fact]
    public void Merge_refuses_a_sum_that_is_no_crate_size()
    {
        var saved = Riker(HullA, Item("Agricium", 10, CrateSize.Eight));

        LoadPlanner.MergeProblem(saved, HullA, saved.Plan.Crates).Should().Be("8 + 2 is not a crate size.");
    }

    [Fact]
    public void Merge_refuses_a_crate_larger_than_the_contract_allows()
    {
        // The kiosk never delivers a crate above the max crate size (D23).
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Two));

        LoadPlanner.MergeProblem(saved, HullA, saved.Plan.Crates)
            .Should().Be("4 SCU crates are larger than the max crate size of 2 SCU for Titanium.");
    }

    [Fact]
    public void Merge_refuses_crates_of_two_contract_items()
    {
        // Each item's SCU is contract data: one 2 from each would move SCU from one item to the other.
        var saved = Riker(HullA, Item("Titanium", 6, CrateSize.Four), Item("Titanium", 6, CrateSize.Four));
        Crate[] twos = [.. saved.Plan.Crates.Where(c => c.CrateSize == CrateSize.Two)];

        LoadPlanner.MergeProblem(saved, HullA, twos).Should().Be("The crates belong to different contract items.");
    }

    [Fact]
    public void MergeProblem_is_null_for_a_merge_that_works()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));
        var moved = Moved(saved, HullA, (CrateAt(saved, "rack-0", 0, 0, 0), "rack-0", new GridPosition(2, 1, 0)));

        LoadPlanner.MergeProblem(moved, HullA, moved.Plan.Crates).Should().BeNull();
    }

    [Fact]
    public void Merge_rejects_fewer_than_two_crates()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four));

        var act = () => LoadPlanner.MergeCrates(saved, HullA, saved.Plan.Crates);

        act.Should().Throw<ArgumentException>().WithParameterName("crates");
    }

    [Fact]
    public void Merge_rejects_the_same_crate_twice()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));

        var act = () => LoadPlanner.MergeCrates(saved, HullA, [saved.Plan.Crates[0], saved.Plan.Crates[0]]);

        act.Should().Throw<ArgumentException>().WithParameterName("crates").WithMessage("*more than once*");
    }

    [Fact]
    public void Merge_rejects_delivered_crates()
    {
        var saved = Riker(HullA, Item("Titanium", 4, CrateSize.Four, CrateSize.Two, CrateSize.Two));
        SavedPlan delivered = new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "Riker", true), saved.CrateCap);

        var act = () => LoadPlanner.MergeCrates(delivered, HullA, delivered.Plan.Crates);

        act.Should().Throw<ArgumentException>().WithParameterName("crates").WithMessage("*delivered*");
    }

    // --- Split, then merge ---

    [Fact]
    public void Split_then_merge_gives_the_original()
    {
        var saved = Riker(HullA, Item("Titanium", 12, CrateSize.Four), Item("Quartz", 4, CrateSize.Two));
        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates[1]);
        Crate[] parts = [.. split.Plan.Crates.Except(saved.Plan.Crates)];

        var merged = LoadPlanner.MergeCrates(split, HullA, parts);

        // Records compare lists by reference, so the lists are compared item by item.
        merged.Plan.Crates.Should().Equal(saved.Plan.Crates);
        merged.Plan.Warnings.Should().Equal(saved.Plan.Warnings);
        merged.Missions.Should().BeEquivalentTo(saved.Missions, o => o.WithStrictOrdering());
    }
}
