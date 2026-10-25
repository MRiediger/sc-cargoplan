using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The planner knows no route (D64): the same missions in any input order give the same racks and positions; a
/// destination stays together, racks hold one destination where they can, and no crate sits on another destination's
/// cargo while a layout avoids it. Where none does, a white hint says so instead of a red finding.
/// </summary>
public class OrderFreePlanTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    /// <summary>Every fixture with the ship it is planned on.</summary>
    public static TheoryData<string, string> Cases => new()
    {
        { "example_missions", "hull_a" },
        { "split_and_shared", "hull_a" },
        { "split_required", "hull_a" },
        { "crate_mix", "hull_a" },
        { "hull_a_eight_stops", "hull_a" },
        { "hull_a_hurston_2026-10-24", "hull_a" },
        { "hull_a_eight_stops", "freelancer" },
        { "spike_uneven_run4", "uneven_long" },
    };

    private static Ship ShipOf(string id) => id == "uneven_long" ? TestShips.Load(id) : ShipLoader.Load(id);

    private static Stop StopOf(string destination, int order, string commodity, int scu, CrateSize size) =>
        new(destination, order, [new CargoItem(commodity, scu, size)]);

    /// <summary>Every permutation up to six stops; above, a fixed set of 50 shuffles (seeded, so every run tests the same).</summary>
    private static IEnumerable<int[]> Permutations(int count)
    {
        if (count <= 6)
        {
            return All([.. Enumerable.Range(0, count)]);
        }

        var random = new Random(64);
        return Enumerable.Range(0, 50).Select(_ =>
        {
            int[] order = [.. Enumerable.Range(0, count)];
            random.Shuffle(order);
            return order;
        });

        static IEnumerable<int[]> All(int[] rest) =>
            rest.Length <= 1
                ? [rest]
                : rest.SelectMany(first => All([.. rest.Where(r => r != first)]).Select(tail => (int[])[first, .. tail]));
    }

    /// <summary>The stops in <paramref name="order"/>, renumbered, every stop's items reversed.</summary>
    private static Stop[] Shuffled(IReadOnlyList<Stop> stops, int[] order) =>
        [.. order.Select((index, position) => stops[index] with { Order = position + 1, Items = [.. stops[index].Items.Reverse()] })];

    /// <summary>Where every crate lies, independent of the crate list's order.</summary>
    private static string[] Layout(LoadPlan plan) =>
        [.. plan.Crates.Select(c => $"{c.Destination}|{c.Commodity}|{(int)c.CrateSize}|{c.RackId}|{c.Position}").Order(StringComparer.Ordinal)];

    /// <summary>Who sits on whom, per rack.</summary>
    private static string[] Stacking(LoadPlan plan, Ship ship) =>
    [
        .. ship.Racks.SelectMany(r => CrateLayout.StackedOn(r, plan.CratesForRack(r.Id)).Select(p => $"{r.Id}|{p.Upper}|{p.Lower}"))
            .Order(StringComparer.Ordinal),
    ];

    private static string[][] StopsPerRack(LoadPlan plan, Ship ship) =>
        [.. ship.Racks.OrderBy(r => r.Index).Select(r => plan.CratesForRack(r.Id).Select(c => c.Destination).Distinct().Order(StringComparer.Ordinal).ToArray())];

    private static IEnumerable<string> SitsOn(LoadPlan plan) => plan.Warnings.Select(w => w.Message).Where(m => m.Contains(" sits on cargo for "));

    // --- Order invariance ---

    [Theory]
    [MemberData(nameof(Cases))]
    public void Plan_gives_the_same_racks_positions_and_stacking_in_any_input_order(string fixture, string shipId)
    {
        var ship = ShipOf(shipId);
        var stops = MissionFixtures.Load(fixture);
        var first = LoadPlanner.Plan(ship, stops);

        foreach (var order in Permutations(stops.Count))
        {
            var plan = LoadPlanner.Plan(ship, Shuffled(stops, order));

            Layout(plan).Should().Equal(Layout(first), "order {0} lays out like the input order", string.Join(",", order));
            Stacking(plan, ship).Should().Equal(Stacking(first, ship), "order {0} stacks like the input order", string.Join(",", order));
        }
    }

    [Fact]
    public void Plan_keeps_the_input_order_in_the_crate_list_for_legend_and_colours()
    {
        var plan = LoadPlanner.Plan(HullA, [StopOf("Shubin", 1, "Quartz", 4, CrateSize.Two), StopOf("Riker", 2, "Titanium", 8, CrateSize.Four)]);

        plan.Route.Should().Equal("Shubin", "Riker");
    }

    // --- Priority 2: one destination per rack ---

    [Fact]
    public void Plan_of_eight_stops_keeps_Lorville_and_Riker_alone_and_shares_two_racks_by_three()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.Load("hull_a_eight_stops"));

        StopsPerRack(plan, HullA).Should().BeEquivalentTo<string[]>(
            [["Lorville"], ["Riker"], ["Orison", "Seraphim", "Shubin"], ["Area18", "Everus Harbor", "Port Tressler"]],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Plan_puts_the_largest_destination_into_rack_1_on_a_tie()
    {
        var plan = LoadPlanner.Plan(HullA, [StopOf("Riker", 1, "Titanium", 8, CrateSize.Four), StopOf("Shubin", 2, "Quartz", 8, CrateSize.Four), StopOf("Orison", 3, "Gold", 16, CrateSize.Sixteen)]);

        StopsPerRack(plan, HullA).Should().BeEquivalentTo<string[]>([["Orison"], ["Riker"], ["Shubin"], []], o => o.WithStrictOrdering());
    }

    // --- Priority 3: no crate on another destination's cargo ---

    /// <summary>
    /// Five stops for four racks: one rack is shared. Area18 (12 in 4s) with Tressler (4) needs every column, so one
    /// sits on the other; Orison (6) with Tressler doesn't. Both pairings tie on racks and sharing.
    /// </summary>
    private static readonly Stop[] FiveStops =
    [
        StopOf("Lorville", 1, "Gold", 16, CrateSize.Sixteen),
        StopOf("Area18", 2, "Titanium", 12, CrateSize.Four),
        new("Orison", 3, [new CargoItem("Quartz", 4, CrateSize.Four), new CargoItem("Iron", 2, CrateSize.Two)]),
        new("Shubin", 4, [new CargoItem("Quartz", 4, CrateSize.Four), new CargoItem("Iron", 2, CrateSize.Two)]),
        StopOf("Port Tressler", 5, "Copper", 4, CrateSize.Four),
    ];

    [Fact]
    public void Plan_chooses_the_pairing_without_foreign_stacking_among_equal_ones()
    {
        var plan = LoadPlanner.Plan(HullA, FiveStops);

        StopsPerRack(plan, HullA).Should().ContainEquivalentOf(new[] { "Orison", "Port Tressler" });
        SitsOn(plan).Should().BeEmpty();
        Stacking(plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Plan_hints_at_foreign_stacking_it_can_not_avoid_and_marks_nothing_red()
    {
        Stop[] stops =
        [
            StopOf("Lorville", 1, "Gold", 16, CrateSize.Sixteen),
            StopOf("Shubin", 2, "Gold", 16, CrateSize.Sixteen),
            StopOf("Orison", 3, "Gold", 16, CrateSize.Sixteen),
            StopOf("Area18", 4, "Titanium", 12, CrateSize.Four),
            StopOf("Port Tressler", 5, "Copper", 4, CrateSize.Four),
        ];

        var plan = LoadPlanner.Plan(HullA, stops);

        var rack = HullA.Racks.Single(r => plan.CratesForRack(r.Id).Any(c => c.Destination == "Area18"));
        plan.Warnings.Should().Contain(new PlanWarning(rack.Id, $"Rack {rack.Number}: crate for Port Tressler sits on cargo for Area18"));
        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void A_crate_dragged_onto_another_destination_gets_the_hint_not_a_problem()
    {
        var plan = LoadPlanner.Plan(HullA, [StopOf("Riker", 1, "Titanium", 4, CrateSize.Four), StopOf("Shubin", 2, "Quartz", 4, CrateSize.Four)]);
        var shubin = plan.Crates.Single(c => c.Destination == "Shubin");
        var riker = plan.Crates.Single(c => c.Destination == "Riker");

        var moved = LoadPlanner.MoveCrate(plan, HullA, shubin, riker.RackId, new GridPosition(riker.Position.Length, riker.Position.Width, 1));

        moved.Warnings.Should().Contain(new PlanWarning(riker.RackId, "Rack 1: crate for Shubin sits on cargo for Riker"));
        LoadPlanner.Validate(moved, HullA).Should().BeEmpty();
    }

    [Fact]
    public void StackedOn_follows_the_support_chain_down_to_the_plate()
    {
        var rack = HullA.Racks.Single(r => r.Index == 0);
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Two, rack.Id, new GridPosition(0, 0, 0)),
            new("Shubin", "Quartz", CrateSize.Two, rack.Id, new GridPosition(0, 1, 0)),
            new("Orison", "Gold", CrateSize.Four, rack.Id, new GridPosition(0, 0, 1)),
        ];

        CrateLayout.StackedOn(rack, crates).Should().BeEquivalentTo([("Orison", "Riker"), ("Orison", "Shubin")]);
    }

    [Fact]
    public void StackedOn_names_every_destination_of_the_chain_below()
    {
        Rack tall = new("tall", 0, 6, new Dimensions(2, 1, 3), null, new ShipPoint(0, 0, 0));
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Two, tall.Id, new GridPosition(0, 0, 0)),
            new("Shubin", "Quartz", CrateSize.Two, tall.Id, new GridPosition(0, 0, 1)),
            new("Orison", "Gold", CrateSize.Two, tall.Id, new GridPosition(0, 0, 2)),
        ];

        CrateLayout.StackedOn(tall, crates).Should().BeEquivalentTo([("Shubin", "Riker"), ("Orison", "Shubin"), ("Orison", "Riker")]);
    }

    [Fact]
    public void StackedOn_ignores_cargo_on_its_own_destination()
    {
        Rack tall = new("tall", 0, 6, new Dimensions(2, 1, 3), null, new ShipPoint(0, 0, 0));
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Two, tall.Id, new GridPosition(0, 0, 0)),
            new("Riker", "Quartz", CrateSize.Two, tall.Id, new GridPosition(0, 0, 1)),
        ];

        CrateLayout.StackedOn(tall, crates).Should().BeEmpty();
    }

    [Fact]
    public void Validate_finds_nothing_in_a_rack_that_only_breaks_the_old_unload_order()
    {
        var rack = HullA.Racks.Single(r => r.Index == 0);
        var plan = new LoadPlan(HullA.Id,
        [
            new Crate("Riker", "Titanium", CrateSize.Eight, rack.Id, new GridPosition(2, 0, 0)),
            new Crate("Riker", "Iron", CrateSize.Four, rack.Id, new GridPosition(0, 0, 0)),
            new Crate("Shubin", "Quartz", CrateSize.Four, rack.Id, new GridPosition(0, 0, 1)),
        ], []);

        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
        CrateLayout.Faulty(rack, plan.Crates).Should().BeEmpty();
    }

    // --- Incremental placement (D41) ---

    [Fact]
    public void PlaceInto_takes_the_rack_of_the_destination_where_nothing_lands_on_other_cargo()
    {
        var plan = LoadPlanner.Plan(HullA, [StopOf("Riker", 1, "Titanium", 8, CrateSize.Four), StopOf("Shubin", 2, "Quartz", 4, CrateSize.Four)]);
        var (rack1, rack2) = (HullA.Racks.Single(r => r.Index == 0), HullA.Racks.Single(r => r.Index == 1));
        var rikerOut = plan.Crates.First(c => c.Destination == "Riker" && c.RackId == rack1.Id);
        var shubin = plan.Crates.Single(c => c.Destination == "Shubin");
        var freeInRack2 = new GridPosition(shubin.Position.Length == 0 ? 2 : 0, 0, 0);

        // Rack 1: Riker and Shubin side by side on the plate; rack 2: Riker alone.
        var swapped = LoadPlanner.MoveCrates(plan, HullA,
            [new CrateMove(rikerOut, rack2.Id, freeInRack2), new CrateMove(shubin, rack1.Id, rikerOut.Position)]);
        SavedPlan saved = SavedPlan.FromStops(
            [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)]), new MissionStop("Shubin", 2, [new MissionItem("Quartz", 4, CrateSize.Four)])],
            swapped, CrateCap.Of(HullA));

        var placed = LoadPlanner.PlaceInto(saved, HullA, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])]);

        placed.Plan.Crates.Where(c => c.Mission == placed.Missions[^1].Id).Should().OnlyContain(c => c.RackId == rack2.Id);
        SitsOn(placed.Plan).Should().BeEmpty();
    }

    [Fact]
    public void PlaceInto_puts_a_new_destination_into_an_empty_rack_before_onto_other_cargo()
    {
        SavedPlan saved = SavedPlan.FromStops(
            [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])],
            LoadPlanner.Plan(HullA, [StopOf("Riker", 1, "Titanium", 8, CrateSize.Four)]), CrateCap.Of(HullA));

        var placed = LoadPlanner.PlaceInto(saved, HullA, [new MissionStop("Shubin", 1, [new MissionItem("Quartz", 4, CrateSize.Four)])]);

        placed.Plan.Crates.Single(c => c.Destination == "Shubin").RackId.Should().Be("rack-1");
        SitsOn(placed.Plan).Should().BeEmpty();
    }
}
