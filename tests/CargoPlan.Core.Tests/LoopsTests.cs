using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The loop ranking (T13.4, D124, design §4.7): rings per hub, direct runs there and back, the system-wide LEO ring from
/// observed edges only, Pyro rings per station with structure and observed rewards; order by the shortest round trip,
/// figures, ranking under the player's times. Positions and templates built here, ships from FrozenShips (A10).
/// </summary>
public class LoopsTests
{
    private const string Everus = "Everus Harbor";
    private const string Seraphim = "Seraphim Station";
    private const string Baijini = "Baijini Point";
    private const string Tressler = "Port Tressler";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L2 = "HUR-L2 Faithful Dream Station";
    private const string L3 = "HUR-L3 Thundering Express Station";
    private const string L4 = "HUR-L4 Melodic Fields Station";
    private const string L5 = "HUR-L5 High Course Station";
    private const string ArcL1 = "ARC-L1 Wide Forest Station";
    private const string Ruin = "Ruin Station";
    private const string Checkmate = "Checkmate";

    private static readonly DateTime Monday = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    // In Gm. The shortest round trip from Everus is L1 → L4 → L3 → L5 → L2 (70.38 Gm), and its reverse; the legs
    // L1 → L4 (13.48), L4 → L3 (20.81), L3 → L5 (20.00), L5 → L2 (14.02) are long, Everus ↔ L1/L2 (about 1) short.
    private static readonly Positions Places = new(
    [
        At(Everus, 0, 0), At(L1, 1, 0.2), At(L2, -1, 0.3), At(L3, 0, 25), At(L4, 12, 8), At(L5, -12, 9),
        At(Baijini, -23, -5), At(Tressler, -10, -40), At(Seraphim, 30, -10), At(ArcL1, -24, -5),
        At(Ruin, 0, 0, StarSystem.Pyro), At(Checkmate, 5, 0, StarSystem.Pyro),
    ]);

    private static readonly HaulLeg[] FourTin = [Leg("Tin", 41, 72), Leg("Tin", 41, 72), Leg("Tin", 41, 72), Leg("Tin", 41, 72)];

    private static HaulTemplate SeniorMedium() =>
        T([], [L1, L2, L3, L4, L5], 325_250, shape: HaulShape.SingleToMulti, stops: 4, legs: FourTin, title: "Senior Rank - Medium Cargo Haul");

    private static HaulTemplate Direct(string[] to, long reward = 60_000, string commodity = "Gold", int maxScu = 20, CrateSize crate = CrateSize.Four) =>
        T([], to, reward, size: HaulSize.Small, direct: true, legs: [Leg(commodity, 1, maxScu, crate)], title: "Rookie Rank - Direct Small Cargo Haul");

    private static HaulTemplate Back(string from, long reward = 45_000) =>
        T([from], [], reward, size: HaulSize.Small, legs: [Leg("Waste", 1, 20, CrateSize.Four)]);

    private static IReadOnlyList<LoopCandidate> For(IEnumerable<HaulTemplate> templates, ObservedEdges? edges = null, HaulFaction? faction = null, string hub = Everus) =>
        Loops.For([.. templates], Places, edges ?? ObservedEdges.Empty, HullB, HaulRank.Senior, faction, hub);

    [Fact]
    public void The_order_is_the_shortest_round_trip()
    {
        Loops.Order(StarSystem.Stanton, Everus, [L5, L3, L2, L4, L1], Places, []).Should().Equal(L1, L4, L3, L5, L2);
    }

    [Fact]
    public void A_tie_goes_to_the_station_with_a_one_destination_contract()
    {
        // Every round trip ties with its reverse: L1 first by place order, L2 first when a contract goes there alone.
        Loops.Order(StarSystem.Stanton, Everus, [L1, L2, L3, L4, L5], Places, []).Should().Equal(L1, L4, L3, L5, L2);
        Loops.Order(StarSystem.Stanton, Everus, [L1, L2, L3, L4, L5], Places, [L2]).Should().Equal(L2, L5, L3, L4, L1);
    }

    [Fact]
    public void A_ring_visits_every_station_of_its_pools_and_brings_the_way_back()
    {
        var bulk = SeniorMedium();
        var direct = Direct([L1]);
        var back = Back(L3);

        var ring = For([bulk, direct, back]).Single(c => c.Kind == LoopKind.LocalRing && c.Start == Everus);

        ring.Stations.Should().Equal(L1, L4, L3, L5, L2);
        (ring.Landings, ring.Legs, ring.LongJumps).Should().Be((6, 6, 4));
        ring.Contracts.Select(c => c.Template).Should().Equal(bulk, direct, back);
        ring.Reward.Should().Be(325_250 + 60_000 + 45_000, "the way back counts");
        ring.RewardPartial.Should().BeFalse();
        // 4 × 72 in 16s: 4 × (4 × 16 + 8) = 4 × 5; 20 in 4s: 5; twice.
        ring.Crates.Should().Be(20 + 5 + 5);
        (ring.Observed, ring.Total, ring.NeedsScansAt).Should().Be((0, 3, (string?)null));
    }

    [Fact]
    public void A_direct_run_goes_to_its_farthest_place_and_back_with_a_return_when_one_fits()
    {
        var direct = Direct([L1, L3]);
        var back = Back(L3);

        var run = For([direct, back]).Single(c => c.Kind == LoopKind.Direct);

        run.Stations.Should().Equal(L3);
        (run.Landings, run.Legs, run.LongJumps).Should().Be((2, 2, 2), "Everus ↔ L3 is 25 Gm both ways");
        run.Contracts.Select(c => c.Template).Should().Equal(direct, back);
        run.Reward.Should().Be(105_000);
    }

    [Fact]
    public void A_direct_run_without_a_return_still_lands_twice()
    {
        var run = For([Direct([L1])]).Single(c => c.Kind == LoopKind.Direct);

        (run.Landings, run.Legs, run.LongJumps).Should().Be((2, 2, 0));
        run.Contracts.Should().ContainSingle();
        run.Reward.Should().Be(60_000);
    }

    [Fact]
    public void A_ring_counts_a_way_back_with_several_pickups_once_and_a_direct_run_takes_none()
    {
        // Gate T13.4: one multi-to-single template was a way back at every station; it is one contract, paid once,
        // accepted at its first pickup on the ring and collecting at the next ones.
        var multi = T([L1, L2, L3, L4, L5], [], 72_500, size: HaulSize.Small, shape: HaulShape.MultiToSingle, stops: 4,
            legs: [Leg("Waste", 1, 4, CrateSize.Four), Leg("Waste", 1, 4, CrateSize.Four), Leg("Waste", 1, 4, CrateSize.Four), Leg("Waste", 1, 4, CrateSize.Four)]);
        var bulk = SeniorMedium();

        var loops = For([bulk, Direct([L3]), multi]);
        var ring = loops.Single(c => c.Kind == LoopKind.LocalRing);
        var run = loops.Single(c => c.Kind == LoopKind.Direct);

        ring.Stations.Should().Equal(L1, L4, L3, L5, L2);
        ring.Contracts.Where(c => c.Kind == RecipeKind.Return).Should().ContainSingle()
            .Which.Should().Match<RecipeRole>(r => r.AcceptAt == L1 && r.CollectAt!.SequenceEqual(new[] { L4, L3, L5 }));
        ring.Reward.Should().Be(325_250 + 60_000 + 72_500);
        run.Contracts.Should().ContainSingle("the way back would need three more pickups: a run lands only at its place");
        run.Landings.Should().Be(2);
    }

    [Fact]
    public void Directs_that_differ_only_in_their_commodity_are_one_row()
    {
        var runs = For([Direct([L3], commodity: "Iron (Ore)"), Direct([L3], commodity: "Quartz (Raw)"), Direct([L3], reward: 70_000)])
            .Where(c => c.Kind == LoopKind.Direct).ToList();

        runs.Select(r => (r.Reward, r.Alike)).Should().BeEquivalentTo(new (long?, int)[] { (60_000, 2), (70_000, 1) });
    }

    [Fact]
    public void An_observation_changes_crates_and_status_not_a_Covalex_reward()
    {
        var direct = Direct([L3], maxScu: 64, crate: CrateSize.Sixteen);
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Small, true, 60_000, ["Gold"]);
        var edges = Edges([direct], Scan(key, [Everus], (L3, "Gold", 20)));

        var before = For([direct]).Single(c => c.Kind == LoopKind.Direct);
        var after = For([direct], edges).Single(c => c.Kind == LoopKind.Direct);

        (before.Crates, before.Observed, before.Total).Should().Be((4, 0, 1), "64 in 16s");
        (after.Crates, after.Observed, after.Total).Should().Be((2, 1, 1), "the largest seen, 20: a 16 and a 4");
        after.Reward.Should().Be(before.Reward);
    }

    [Fact]
    public void A_Pyro_ring_shows_structure_and_a_reward_only_once_observed_its_faction_twin_once()
    {
        var hh = T([Ruin], [Checkmate], null, contractor: HaulContractor.RedWind, rank: HaulRank.Member, size: HaulSize.Small, system: StarSystem.Pyro,
            areaKey: Board.PyroSolar, faction: HaulFaction.Hh, legs: [Leg("Carbon", 1, 20, CrateSize.Four)]);
        var cfp = hh with { Uuid = "cfp-twin", Faction = HaulFaction.Cfp };
        var key = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Small, null, 532_500, ["Carbon"]);

        var before = For([hh, cfp]).Single(c => c.System == StarSystem.Pyro);
        var after = For([hh, cfp], Edges([hh, cfp], Scan(key, [Ruin], (Checkmate, "Carbon", 18)))).Single(c => c.System == StarSystem.Pyro);

        (before.Kind, before.Start, before.Landings).Should().Be((LoopKind.LocalRing, Ruin, 2));
        before.Contracts.Should().ContainSingle("both sides of one variant are one contract");
        before.Reward.Should().BeNull("Red Wind's data have no reward");
        before.TravelGm.Should().BeNull("another system");
        after.Reward.Should().Be(532_500);
        For([hh, cfp], faction: HaulFaction.Cfp).Single(c => c.System == StarSystem.Pyro).Contracts.Single().Template.Should().Be(cfp);
    }

    [Fact]
    public void The_LEO_ring_needs_scans_without_observed_edges()
    {
        var (tin, junior, ice) = SystemWide();
        var leo = For([tin, junior, ice]).Single(c => c.Kind == LoopKind.SystemWide);

        leo.Contracts.Should().BeEmpty();
        leo.HasNumbers.Should().BeFalse();
        leo.NeedsScansAt.Should().Be(Everus);
        (leo.Observed, leo.Total).Should().Be((0, 4));
    }

    [Fact]
    public void The_LEO_ring_takes_what_scans_saw_between_the_hubs_and_unloads_on_the_way()
    {
        // Design §8.6: at Everus a Senior Medium of tin to three hubs and a Junior Direct Medium to Baijini; at Port
        // Tressler an Experienced Direct Medium of ice to Everus, which fits only after Baijini's and Port Tressler's tin is off.
        var (tin, junior, ice) = SystemWide();
        var edges = Edges([tin, junior, ice],
            Scan(TinKey, [Everus], (Baijini, "Tin", 113), (Tressler, "Tin", 129), (Seraphim, "Tin", 91)),
            Scan(JuniorKey, [Everus], (Baijini, "Tin", 89)),
            Scan(IceKey, [Tressler], (Everus, "Pressurized Ice", 339)));

        var leo = For([tin, junior, ice], edges).Single(c => c.Kind == LoopKind.SystemWide);

        leo.Stations.Should().Equal([Baijini, Tressler, Seraphim], "Baijini first: a contract goes there alone");
        leo.Contracts.Select(c => c.Template).Should().Equal(tin, junior, ice);

        // T13.7b (D125 addendum): what the ring takes on the way says where; to the start it is a way back.
        leo.Contracts.Select(c => (c.Kind, c.AcceptAt)).Should().Equal(
            (RecipeKind.Bulk, null), (RecipeKind.Direct, null), (RecipeKind.Return, Tressler));
        leo.Contracts[2].CollectAt.Should().BeEmpty();
        leo.Reward.Should().Be(289_000 + 57_750 + 165_250);
        (leo.Landings, leo.LongJumps).Should().Be((4, 4));
        (leo.Observed, leo.Total, leo.NeedsScansAt).Should().Be((2, 4, Baijini), "no edge seen from Baijini yet");
    }

    [Fact]
    public void Travel_is_from_the_saved_hub_to_the_loops_start()
    {
        var everus = Direct([L1]);
        var baijini = T([], [ArcL1], 50_000, size: HaulSize.Small, areaKey: "Stanton3", legs: [Leg("Gold", 1, 20, CrateSize.Four)]);

        var rings = For([everus, baijini]).Where(c => c.Kind == LoopKind.LocalRing).ToDictionary(c => c.Start);

        rings[Everus].TravelGm.Should().Be(0);
        rings[Baijini].TravelGm.Should().BeApproximately(Math.Sqrt(23 * 23 + 5 * 5), 1e-9);
        For([everus, baijini], hub: Baijini).Single(c => c.Kind == LoopKind.LocalRing && c.Start == Everus).TravelGm
            .Should().BeApproximately(Math.Sqrt(23 * 23 + 5 * 5), 1e-9);
    }

    [Fact]
    public void A_hub_whose_board_lays_nothing_has_no_ring()
    {
        var large = T([], [L1], 2_000_000, size: HaulSize.Large, legs: [Leg("Gold", 500, 600, CrateSize.ThirtyTwo)]);

        For([large]).Should().NotContain(c => c.Kind == LoopKind.LocalRing || c.Kind == LoopKind.Direct);
    }

    [Fact]
    public void The_fit_cache_changes_nothing_and_a_second_ranking_asks_the_planner_nothing_new()
    {
        HaulTemplate[] templates = [SeniorMedium(), Direct([L1, L3]), Back(L3), Back(L5)];
        var fits = new FitCache();

        var without = Loops.For(templates, Places, ObservedEdges.Empty, HullB, HaulRank.Senior, null, Everus);
        var first = Loops.For(templates, Places, ObservedEdges.Empty, HullB, HaulRank.Senior, null, Everus, fits);
        var asked = fits.Count;
        var second = Loops.For(templates, Places, ObservedEdges.Empty, HullB, HaulRank.Senior, null, Everus, fits);

        first.Should().BeEquivalentTo(without, o => o.WithStrictOrdering());
        second.Should().BeEquivalentTo(without, o => o.WithStrictOrdering());
        asked.Should().BePositive();
        fits.Count.Should().Be(asked);
    }

    [Fact]
    public void The_ranking_tips_with_the_time_per_station()
    {
        // A ring of six landings and few crates against a direct run of two landings and many: at 10 min per station the
        // direct pays more per hour (375k against 360k), at 5 min the ring (684k against 500k).
        var ring = Made(LoopKind.LocalRing, 380_000, stations: 5, crates: 10);
        var direct = Made(LoopKind.Direct, 250_000, stations: 1, crates: 60);

        Loops.Rank([ring, direct], LoopSort.PerHour, LoopTimes.Default).Should().Equal(direct, ring);
        Loops.Rank([ring, direct], LoopSort.PerHour, LoopTimes.Default with { StationMinutes = 5 }).Should().Equal(ring, direct);
    }

    [Fact]
    public void Sorting_by_loop_and_landing_and_rows_without_figures_last()
    {
        var big = Made(LoopKind.LocalRing, 1_000_000, stations: 5, crates: 70);
        var small = Made(LoopKind.LocalRing, 600_000, stations: 2, crates: 40);
        var unknown = Made(LoopKind.LocalRing, null, stations: 3, crates: 20) with { Start = Ruin, System = StarSystem.Pyro };
        var needsScans = Made(LoopKind.SystemWide, null, stations: 3, crates: 0) with { Contracts = [], NeedsScansAt = Everus };

        Loops.Rank([needsScans, unknown, small, big], LoopSort.PerLoop, LoopTimes.Default).Should().Equal(big, small, unknown, needsScans);
        Loops.Rank([needsScans, unknown, big, small], LoopSort.PerLanding, LoopTimes.Default).Should().Equal(small, big, unknown, needsScans);
    }

    [Fact]
    public void A_measured_row_overtakes_a_model_row_by_the_hour_and_leaves_loop_and_landing_alone()
    {
        // By the model the direct pays more per hour (375k against 360k, above); the ring flown in a median 50 min pays 456k.
        var ring = Made(LoopKind.LocalRing, 380_000, stations: 5, crates: 10);
        var direct = Made(LoopKind.Direct, 250_000, stations: 1, crates: 60);
        var measured = MeasuredTimes.For([RunOf(ring, 45, 0), RunOf(ring, 50, 1), RunOf(ring, 70, 2)], HullB.Id);

        Loops.Rank([ring, direct], LoopSort.PerHour, LoopTimes.Default, measured).Should().Equal(ring, direct);
        Loops.Value(ring, LoopSort.PerHour, LoopTimes.Default, measured).Should().BeApproximately(380_000 / 50.0 * 60, 1e-6);
        Loops.Value(direct, LoopSort.PerHour, LoopTimes.Default, measured).Should().Be(direct.PerHour(LoopTimes.Default), "a row is measured or modelled, never both");
        Loops.Value(ring, LoopSort.PerLoop, LoopTimes.Default, measured).Should().Be(380_000);
        Loops.Value(ring, LoopSort.PerLanding, LoopTimes.Default, measured).Should().Be(ring.PerLanding);
        Loops.Rank([ring, direct], LoopSort.PerHour, LoopTimes.Default, null).Should().Equal(direct, ring);
        Loops.Rank([ring, direct], LoopSort.PerHour, LoopTimes.Default, MeasuredTimes.Empty).Should().Equal(direct, ring);
    }

    [Fact]
    public void A_loop_started_and_finished_is_measured_as_the_same_loop()
    {
        // The key both ways (T13.6c): the candidate's, and the run's that Start plan and Finish plan made of it.
        HaulTemplate[] templates = [SeniorMedium(), Back(L1), Direct([L3]), Direct([L3], commodity: "Iron"), Direct([L5], reward: 90_000)];
        var loops = For(templates).Where(l => l.HasNumbers && l.System == StarSystem.Stanton).ToList();
        loops.Select(l => l.Kind).Should().Contain([LoopKind.LocalRing, LoopKind.Direct]);
        var plan = new SavedPlan([], new LoadPlan(HullB.Id, [], []), CrateCap.Of(HullB));

        foreach (var loop in loops)
        {
            var route = Route.From(loop, "name", ObservedEdges.Empty, Monday) with { DepartedUtc = Monday };
            var run = Route.Finish(route, plan, RouteProgress.Of(route, plan, HullB, Places), Places, HullB, templates, Monday.AddMinutes(60));

            LoopKey.Of(run).Should().Be(LoopKey.Of(loop), $"{loop.Kind} from {loop.Start}");
            loops.Where(other => other != loop).Should().NotContain(other => LoopKey.Of(other) == LoopKey.Of(run));
        }
    }

    [Fact]
    public void Per_hour_uses_landings_crates_and_long_jumps_only()
    {
        var ring = Made(LoopKind.LocalRing, 1_010_000, stations: 5, crates: 75, longJumps: 4);

        // 6 × 10 min + 75 × 20 s + 4 × 4 min = 101 min.
        ring.PerHour(LoopTimes.Default).Should().BeApproximately(1_010_000 / (101 / 60.0), 1e-6);
        ring.PerLanding.Should().BeApproximately(1_010_000 / 6.0, 1e-9);
    }

    private static readonly ObservationKey TinKey = new(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 289_000, ["Tin"]);
    private static readonly ObservationKey JuniorKey = new(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Medium, true, 57_750, ["Tin"]);
    private static readonly ObservationKey IceKey = new(HaulContractor.Covalex, HaulRank.Experienced, HaulSize.Medium, true, 165_250, ["Pressurized Ice"]);

    // The system-wide templates of design §8.6; the data name no place for them.
    private static (HaulTemplate Tin, HaulTemplate Junior, HaulTemplate Ice) SystemWide() =>
    (
        T([], [], 289_000, areaKey: Board.StantonWide, shape: HaulShape.SingleToMulti, stops: 3, legs: [Leg("Tin", 41, 129), Leg("Tin", 41, 129), Leg("Tin", 41, 129)]),
        T([], [], 57_750, rank: HaulRank.Junior, direct: true, areaKey: Board.StantonWide, legs: [Leg("Tin", 60, 112)]),
        T([], [], 165_250, rank: HaulRank.Experienced, direct: true, areaKey: Board.StantonWide, legs: [Leg("Pressurized Ice", 280, 360)])
    );

    private static LoopCandidate Made(LoopKind kind, long? reward, int stations, int crates, int longJumps = 0) => new(
        kind, StarSystem.Stanton, Everus, [.. Enumerable.Range(0, stations).Select(i => $"S{i}")],
        [new RecipeRole(RecipeKind.Small, Direct([L1]), [L1], 20, crates)], reward, false, crates, longJumps, 0, 0, 1);

    // A run of loop that took minutes, finished day days after Monday.
    private static RouteRun RunOf(LoopCandidate loop, double minutes, int day)
    {
        var finished = Monday.AddDays(day);
        return new("run", finished.AddMinutes(-minutes), null, finished, loop.Start, loop.Kind, loop.System,
            loop.Landings, loop.Crates, loop.LongJumps, loop.Reward ?? 0, 1, 1, 0, HullB.Id, loop.Stations);
    }

    private static ObservedEdges Edges(IReadOnlyList<HaulTemplate> templates, params Observation[] seen) =>
        ObservedEdges.From(new Observations(seen), templates, Places);

    private static Observation Scan(ObservationKey key, string[] pickups, params (string Destination, string Commodity, int Scu)[] deliveries) =>
        Observation.Of(new ObservedScan(key, null, pickups, [.. deliveries.Select(d => new ObservedDelivery(d.Destination, d.Commodity, d.Scu))], Monday));

    private static StarPosition At(string name, double xGm, double yGm, StarSystem system = StarSystem.Stanton) =>
        new(name, name, system, "Manmade", null, xGm * 1e9, yGm * 1e9, 0, []);
}
