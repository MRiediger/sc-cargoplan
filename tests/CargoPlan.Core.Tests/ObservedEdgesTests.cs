using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>Observed edges and rewards (T13.3, design §8.3), and how the port matrix shows them. Built templates (A10).</summary>
public sealed class ObservedEdgesTests
{
    private const string Everus = "Everus Harbor";
    private const string Baijini = "Baijini Point";
    private const string Tressler = "Port Tressler";
    private const string Seraphim = "Seraphim Station";
    private const string L1 = "HUR-L1 Green Glade Station";

    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Positions Places = new(
    [
        .. new[] { Everus, Baijini, Tressler, Seraphim, L1 }.Select(n => Position(n, StarSystem.Stanton)),
        .. new[] { "Ruin Station", "Checkmate" }.Select(n => Position(n, StarSystem.Pyro)),
    ]);

    // Design §8.6: Senior Medium 289,000 Tin from Everus to Baijini, Port Tressler and Seraphim; the data name no place.
    private static readonly HaulTemplate SystemWide = T([], [], reward: 289_000, areaKey: "Stanton", shape: HaulShape.SingleToMulti, stops: 3,
        legs: [Leg("Tin", 41, 144), Leg("Tin", 41, 144), Leg("Tin", 41, 144)]);

    private static readonly ObservationKey Tin = new(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 289_000, ["Tin"]);

    private static readonly RouteFilter Stellar = RouteFilter.Default;

    [Fact]
    public void A_matched_observation_gives_its_template_an_edge_per_pickup_and_drop_off()
    {
        var edges = ObservedEdges.From(Seen(Scan(Tin, [Everus], (Baijini, "Tin", 113), (Tressler, "Tin", 129), (Seraphim, "Tin", 91))), [SystemWide], Places);

        edges.Of(SystemWide).Should().BeEquivalentTo(
        [
            new ObservedEdge(Everus, Baijini, 1, Monday),
            new ObservedEdge(Everus, Tressler, 1, Monday),
            new ObservedEdge(Everus, Seraphim, 1, Monday),
        ]);
        (edges.Matched, edges.Ambiguous, edges.None).Should().Be((1, 0, 0));
    }

    [Fact]
    public void A_system_wide_template_leaves_the_line_below_the_matrix_once_observed()
    {
        var before = PortMatrix.Build([SystemWide], Stellar);
        var after = PortMatrix.Build([SystemWide], Stellar,
            ObservedEdges.From(Seen(Scan(Tin, [Everus], (Baijini, "Tin", 113), (Tressler, "Tin", 129), (Seraphim, "Tin", 91))), [SystemWide], Places));

        before.WithoutPlace.Should().Be(1);
        after.WithoutPlace.Should().Be(0);
        after.Cell(Everus, Baijini)!.Should().Match<PortCell>(c => c.Count == 1 && c.RewardSum == 289_000 && c.Observed);
        after.Pickups.Should().Equal(Everus);
        after.Dropoffs.Should().Equal(Seraphim, Baijini, Tressler);
    }

    [Fact]
    public void An_observed_pair_of_the_pools_marks_the_template_there_and_nowhere_else()
    {
        var local = T([], [L1, Baijini], reward: 50_500, rank: HaulRank.Rookie, direct: true, legs: [Leg("Iron (Ore)", 80, 112, CrateSize.Eight)]);
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Medium, true, 50_500, ["Iron (Ore)"]);

        var matrix = PortMatrix.Build([local], Stellar, ObservedEdges.From(Seen(Scan(key, [Everus], (L1, "Iron (Ore)", 94))), [local], Places));

        matrix.Cell(Everus, L1)!.Observed.Should().BeTrue();
        matrix.Cell(Everus, L1)!.Count.Should().Be(1, "the observed pair is the pool's, not a second entry");
        matrix.Cell(Everus, Baijini)!.Observed.Should().BeFalse();
    }

    [Fact]
    public void Faction_twins_share_the_observation_and_its_reward()
    {
        var hh = T(["Checkmate"], ["Ruin Station"], reward: null, contractor: HaulContractor.RedWind, rank: HaulRank.Member, system: StarSystem.Pyro,
            areaKey: "Solar", faction: HaulFaction.Hh, legs: [Leg("Carbon", 25, 175, CrateSize.Eight)]);
        var cfp = hh with { Uuid = "cfp", Faction = HaulFaction.Cfp };
        var key = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, null, 532_500, ["Carbon"]);
        var earlier = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, null, 500_000, ["Carbon"]);

        var edges = ObservedEdges.From(
            Seen(Scan(earlier, ["Checkmate"], ("Ruin Station", "Carbon", 90)) with { LastUtc = Monday.AddDays(-1) }, Scan(key, ["Checkmate"], ("Ruin Station", "Carbon", 80))),
            [hh, cfp], Places);

        edges.RewardOf(hh).Should().Be(532_500, "the last reward seen");
        edges.RewardOf(cfp).Should().Be(532_500);
        var cell = PortMatrix.Build([hh, cfp], RouteFilter.Default with { System = StarSystem.Pyro }, edges).Cell("Checkmate", "Ruin Station")!;
        cell.RewardSum.Should().Be(1_065_000);
        cell.UnknownRewards.Should().Be(0);
        PortMatrix.Build([hh, cfp], RouteFilter.Default with { System = StarSystem.Pyro, Faction = HaulFaction.Hh }, edges)
            .Cell("Checkmate", "Ruin Station")!.RewardSum.Should().Be(532_500, "the faction filter shows the player's side");
    }

    [Fact]
    public void A_Covalex_reward_stays_the_datas()
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 289_000, ["Tin"]);

        ObservedEdges.From(Seen(Scan(key, [Everus], (Baijini, "Tin", 113), (Tressler, "Tin", 129), (Seraphim, "Tin", 91))), [SystemWide], Places)
            .RewardOf(SystemWide).Should().BeNull("the data know it");
    }

    [Fact]
    public void Templates_ambiguous_beyond_the_faction_or_none_add_nothing()
    {
        var twin = SystemWide with { Uuid = "twin", Legs = [.. SystemWide.Legs] };
        var unknown = Tin with { Reward = 1 };

        var edges = ObservedEdges.From(
            Seen(Scan(Tin, [Everus], (Baijini, "Tin", 113), (Tressler, "Tin", 129), (Seraphim, "Tin", 91)), Scan(unknown, [Everus], (Baijini, "Tin", 50))),
            [SystemWide, twin], Places);

        edges.Of(SystemWide).Should().BeEmpty();
        (edges.Matched, edges.Ambiguous, edges.None).Should().Be((0, 1, 1));
    }

    [Fact]
    public void A_place_no_position_answers_to_makes_no_edge()
    {
        var edges = ObservedEdges.From(Seen(Scan(Tin, [Everus], (Baijini, "Tin", 113), ("Bajini Pt", "Tin", 129), (Seraphim, "Tin", 91))), [SystemWide], Places);

        edges.Of(SystemWide).Select(e => e.To).Should().Equal(Baijini, Seraphim);
    }

    private static Observations Seen(params Observation[] entries) => new(entries);

    private static Observation Scan(ObservationKey key, string[] pickups, params (string Destination, string Commodity, int Scu)[] deliveries) =>
        Observation.Of(new ObservedScan(key, null, pickups, [.. deliveries.Select(d => new ObservedDelivery(d.Destination, d.Commodity, d.Scu))], Monday));

    private static StarPosition Position(string name, StarSystem system) => new(name, name, system, "Manmade", null, 0, 0, 0, []);
}
