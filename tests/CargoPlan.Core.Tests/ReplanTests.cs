using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>A plan moved to another ship (D59): planned anew from its missions, ticks per stop kept.</summary>
public class ReplanTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly Ship C2 = ShipLoader.Load("c2_hercules");

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max) =>
        new(destination, 1, [new MissionItem(commodity, scu, max)]);

    /// <summary>Each stop a mission of its own, as captures come in.</summary>
    private static SavedPlan Placed(Ship ship, long watermark, params (MissionStop Stop, ContractSource? Contract)[] missions) =>
        missions.Aggregate(new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship), watermark), (saved, m) => LoadPlanner.PlaceInto(saved, ship, [m.Stop], m.Contract));

    /// <summary>The T7.0 load case: 8 stops, 58 of 64 SCU, one mission per stop.</summary>
    private static SavedPlan EightStops(Ship ship, FillOrder fillOrder = FillOrder.BowFirst)
    {
        var stops = MissionFixtures.HullAEightStopsMissions;
        return SavedPlan.FromStops(stops, LoadPlanner.Plan(ship, stops.ToStops(), fillOrder), CrateCap.Of(ship));
    }

    private static string[] Route(SavedPlan saved) => [.. saved.Stops.Select(s => s.Destination)];

    private static SavedPlan Ticked(SavedPlan saved, Ship ship, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, ship, destination, true), saved.CrateCap, saved.Watermark);

    private static IEnumerable<(string, string, CrateSize)> Kinds(SavedPlan saved) =>
        saved.Plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize)).Order();

    [Fact]
    public void Replan_puts_the_plan_on_the_new_ship_and_keeps_missions_contracts_watermark_and_fill_order()
    {
        var before = Placed(
            HullA, 41,
            (Stop("Riker", "Titanium", 12, CrateSize.Four), null),
            (Stop("Shubin", "Quartz", 8, CrateSize.Four), new ContractSource([36, 37], true, 3, 50_250)));
        before = new SavedPlan(before.Missions, before.Plan with { FillOrder = FillOrder.SternFirst }, before.CrateCap, before.Watermark);

        var after = LoadPlanner.Replan(before, C2);

        after.Plan.ShipId.Should().Be("c2_hercules");
        after.Missions.Should().BeEquivalentTo(before.Missions, o => o.WithStrictOrdering());
        after.Watermark.Should().Be(41);
        after.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        Kinds(after).Should().Equal(Kinds(before));
        LoadPlanner.Validate(after.Plan, C2).Should().BeEmpty();
    }

    [Fact]
    public void Replan_there_and_back_gives_the_plan_the_editor_would_make()
    {
        var hullA = EightStops(HullA);

        var back = LoadPlanner.Replan(LoadPlanner.Replan(hullA, C2), HullA);

        var editor = LoadPlanner.Plan(HullA, hullA.Missions, Route(hullA)).Plan;
        back.Plan.ShipId.Should().Be("hull_a");
        back.Plan.Crates.Should().Equal(editor.Crates);
        back.Plan.Warnings.Should().Equal(editor.Warnings);
    }

    [Fact]
    public void Replan_keeps_the_ticks_per_stop()
    {
        var saved = EightStops(HullA);
        var first = Route(saved)[0];

        var after = LoadPlanner.Replan(Ticked(saved, HullA, first), C2);

        after.Plan.Crates.Where(c => c.Destination == first).Should().OnlyContain(c => c.Delivered);
        after.Plan.Crates.Where(c => c.Destination != first).Should().OnlyContain(c => !c.Delivered);
    }

    [Fact]
    public void Replan_keeps_the_sizes_of_split_crates()
    {
        var saved = EightStops(HullA);
        var crate = saved.Plan.Crates.First(c => c.CrateSize == CrateSize.Sixteen);
        var split = LoadPlanner.SplitCrate(saved, HullA, crate);

        var after = LoadPlanner.Replan(split, C2);

        Kinds(after).Should().Equal(Kinds(split));
    }

    [Fact]
    public void Replan_that_doesnt_fit_places_mission_by_mission_and_leaves_the_rest_unplaced()
    {
        // 72 SCU: the C2 takes all, the Hull A's 64 SCU refuse the plan; missions 1–3 fill it, mission 4 waits (D41).
        var onC2 = Placed(
            C2, 0,
            (Stop("Riker", "Titanium", 32, CrateSize.Sixteen), null),
            (Stop("Shubin", "Quartz", 16, CrateSize.Sixteen), null),
            (Stop("Orison", "Gold", 16, CrateSize.Sixteen), null),
            (Stop("Lorville", "Agricium", 8, CrateSize.Eight), null));

        var after = LoadPlanner.Replan(onC2, HullA);

        after.Missions.Select(m => m.Unplaced).Should().Equal(false, false, false, true);
        after.Plan.Crates.Select(c => c.Destination).Distinct().Should().Equal("Riker", "Shubin", "Orison");
        LoadPlanner.PlaceProblem(after, HullA, 4).Should().Be("doesn't fit (8 SCU, 0 free)");
    }

    [Fact]
    public void Replan_tries_missions_that_didnt_fit_before()
    {
        var full = Placed(
            HullA, 0,
            (Stop("Riker", "Titanium", 64, CrateSize.Sixteen), null),
            (Stop("Shubin", "Quartz", 8, CrateSize.Eight), null));
        full.Missions[1].Unplaced.Should().BeTrue();

        var after = LoadPlanner.Replan(full, C2);

        after.Missions.Should().OnlyContain(m => !m.Unplaced);
        after.Plan.Crates.Should().Contain(c => c.Destination == "Shubin");
    }

    [Fact]
    public void Replan_of_a_plan_without_missions_is_the_empty_plan_of_the_new_ship()
    {
        var after = LoadPlanner.Replan(new SavedPlan([], new LoadPlan(HullA.Id, [], [], FillOrder.SternFirst), CrateCap.Of(HullA), 7), C2);

        after.Missions.Should().BeEmpty();
        after.Plan.ShipId.Should().Be("c2_hercules");
        after.Plan.Crates.Should().BeEmpty();
        after.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        after.Watermark.Should().Be(7);
    }

    public static TheoryData<string> ShippedShipIds => [.. ShipLoader.ListShips(ShipLoader.DefaultFolder)];

    // The overlay's switch has no catch: every ship in ships/ must take a plan, placed or waiting. The 3-stop example,
    // not the 8-stop load case: that one reaches the search limit on the Freelancer (~8 s Debug, see the Slow test).
    [Theory]
    [MemberData(nameof(ShippedShipIds))]
    public void Replan_onto_every_shipped_ship_never_throws(string shipId)
    {
        var ship = ShipLoader.Load(shipId);
        var example = MissionFixtures.LoadMissions("example_missions");
        var saved = SavedPlan.FromStops(example, LoadPlanner.Plan(HullA, example.ToStops()), CrateCap.Of(HullA));

        var after = LoadPlanner.Replan(Ticked(saved, HullA, "Riker"), ship);

        after.Plan.ShipId.Should().Be(shipId);
        after.Missions.Should().HaveCount(3);
    }

    // T7.2 finding: the 8-stop load case takes ~4 s in Release on the Freelancer (rack 9×2×3), on the UI thread like the
    // editor's Plan. Kept as the case for the backlog line on planning off the UI thread.
    [Fact]
    [Trait("Category", "Slow")]
    public void Replan_of_the_load_case_onto_the_Freelancer_places_every_mission()
    {
        var after = LoadPlanner.Replan(EightStops(HullA), ShipLoader.Load("freelancer"));

        after.Missions.Should().OnlyContain(m => !m.Unplaced);
    }
}
