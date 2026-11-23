using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>An observation to its template (T13.3, D122). Templates and positions built here (A10).</summary>
public sealed class TemplateMatchTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L2 = "HUR-L2 Faithful Dream Station";
    private const string L3 = "HUR-L3 Thundering Express Station";
    private const string L4 = "HUR-L4 Melodic Fields Station";
    private const string L5 = "HUR-L5 High Course Station";
    private const string Seraphim = "Seraphim Station";
    private const string Baijini = "Baijini Point";

    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Positions Places = new(
    [
        .. new[] { Everus, L1, L2, L3, L4, L5, Seraphim, Baijini }.Select(n => Position(n, StarSystem.Stanton)),
        .. new[] { "Ruin Station", "Checkmate", "Endgame", "Orbituary" }.Select(n => Position(n, StarSystem.Pyro)),
    ]);

    private static readonly string[] Ring = [L1, L2, L3, L4, L5];

    // Design §7.3: four Rookie Direct Medium for 50,500 at Everus, one per ore.
    private static readonly HaulTemplate Iron = RookieDirect("Iron (Ore)");
    private static readonly HaulTemplate Quartz = RookieDirect("Quartz (Raw)");

    [Fact]
    public void A_Covalex_scan_is_the_template_of_its_rank_size_direct_reward_and_commodity()
    {
        var result = TemplateMatch.Find(Scan(Key(HaulRank.Rookie, HaulSize.Medium, true, 50_500), [Everus], (L1, "Iron (Ore)", 94)), [Iron, Quartz], Places);

        result.Should().Be(new MatchResult.Matched(Iron));
    }

    [Fact]
    public void Commodities_compare_as_OCR_reads_them() =>
        TemplateMatch.Find(Scan(Key(HaulRank.Rookie, HaulSize.Medium, true, 50_500), [Everus], ("Green Glade", "lron (0re)", 94)), [Iron, Quartz], Places)
            .Should().Be(new MatchResult.Matched(Iron));

    [Fact]
    public void Two_templates_the_scan_cannot_tell_apart_are_ambiguous()
    {
        // ships-2026.11.2: two system-wide Junior Direct Medium for 55,250, both Quartz 80–112, both without places.
        var first = T([], [], reward: 55_250, rank: HaulRank.Junior, direct: true, areaKey: "Stanton", legs: [Leg("Quartz", 80, 112, CrateSize.Eight)]);
        var second = first with { Uuid = "twin" };

        var result = TemplateMatch.Find(Scan(Key(HaulRank.Junior, HaulSize.Medium, true, 55_250), [Baijini], (Seraphim, "Quartz", 100)), [first, second], Places);

        result.Should().BeOfType<MatchResult.Ambiguous>().Which.Candidates.Should().Equal([first, second]);
    }

    [Theory]
    [InlineData(HaulRank.Junior, 50_500L, "no covalex template of rank junior, size medium, direct")]
    [InlineData(HaulRank.Rookie, 50_250L, "no such template pays 50,250 aUEC")]
    public void A_scan_no_template_answers_names_the_first_test_none_passed(HaulRank rank, long reward, string reason) =>
        TemplateMatch.Find(Scan(Key(rank, HaulSize.Medium, true, reward), [Everus], (L1, "Iron (Ore)", 94)), [Iron, Quartz], Places)
            .Should().Be(new MatchResult.None(reason));

    [Fact]
    public void A_commodity_no_leg_carries_is_no_match() =>
        TemplateMatch.Find(Scan(Key(HaulRank.Rookie, HaulSize.Medium, true, 50_500), [Everus], (L1, "Gold", 94)), [Iron, Quartz], Places)
            .Should().BeOfType<MatchResult.None>().Which.Reason.Should().Be("no such template carries Gold");

    [Theory]
    [InlineData(79)]
    [InlineData(113)]
    public void Scu_outside_the_band_is_no_match(int scu) =>
        TemplateMatch.Find(Scan(Key(HaulRank.Rookie, HaulSize.Medium, true, 50_500), [Everus], (L1, "Iron (Ore)", scu)), [Iron], Places)
            .Should().BeOfType<MatchResult.None>().Which.Reason.Should().StartWith("SCU outside the template's bands: Iron (Ore)");

    [Fact]
    public void A_delivery_line_may_carry_several_legs_of_its_commodity()
    {
        // carbon_or_cut: 200 SCU Carbon to Ruin Station from two pickups, legs of 25–175 each.
        var twoPickups = RedWind(HaulShape.MultiToSingle, 2, ["Checkmate", "Endgame", "Orbituary"], ["Ruin Station"], Leg("Carbon", 25, 175, CrateSize.Eight), Leg("Carbon", 25, 175, CrateSize.Eight));

        TemplateMatch.Find(Scan(RedWindKey(), ["Checkmate", "Endgame"], ("Ruin Station", "Carbon", 200)), [twoPickups], Places)
            .Should().Be(new MatchResult.Matched(twoPickups));
    }

    [Fact]
    public void The_many_side_names_as_many_places_as_the_template_draws()
    {
        var two = T([], Ring, reward: 72_500, rank: HaulRank.Junior, size: HaulSize.Small, shape: HaulShape.SingleToMulti, stops: 2, legs: [Leg("Stims", 2, 4, CrateSize.Four), Leg("Stims", 2, 4, CrateSize.Four)]);
        var three = two with { Uuid = "three", Stops = 3, Legs = [.. two.Legs, Leg("Stims", 2, 4, CrateSize.Four)] };
        var key = Key(HaulRank.Junior, HaulSize.Small, false, 72_500);

        TemplateMatch.Find(Scan(key, [Everus], (L1, "Stims", 3), (L2, "Stims", 4)), [two, three], Places).Should().Be(new MatchResult.Matched(two));
        TemplateMatch.Find(Scan(key, [Everus], (L1, "Stims", 3), (L2, "Stims", 4), (L5, "Stims", 2)), [two, three], Places).Should().Be(new MatchResult.Matched(three));
    }

    [Fact]
    public void A_pool_smaller_than_the_stops_repeats_a_station()
    {
        // Design §2: at Crusader's three stations a four-stop contract goes L5 · L4 · L1 · L5.
        var four = T([], [L1, L2, L3], reward: 90_000, rank: HaulRank.Member, size: HaulSize.Small, shape: HaulShape.SingleToMulti, stops: 4,
            legs: [.. Enumerable.Repeat(Leg("Stims", 2, 4, CrateSize.Four), 4)]);

        TemplateMatch.Find(Scan(Key(HaulRank.Member, HaulSize.Small, false, 90_000), [Everus], (L1, "Stims", 3), (L2, "Stims", 4), (L3, "Stims", 6)), [four], Places)
            .Should().Be(new MatchResult.Matched(four));
    }

    [Fact]
    public void Several_scans_together_name_at_least_the_places_of_one()
    {
        var two = T([], Ring, reward: 72_500, rank: HaulRank.Junior, size: HaulSize.Small, shape: HaulShape.SingleToMulti, stops: 2, legs: [Leg("Stims", 2, 4, CrateSize.Four), Leg("Stims", 2, 4, CrateSize.Four)]);
        var key = Key(HaulRank.Junior, HaulSize.Small, false, 72_500);
        var observation = Observation.Of(Delivered(key, [Everus], (L1, "Stims", 3), (L2, "Stims", 4)))
            .With(Delivered(key, [Everus], (L3, "Stims", 3), (L4, "Stims", 4)));

        TemplateMatch.Find(observation, [two], Places).Should().Be(new MatchResult.Matched(two));
    }

    [Theory]
    [InlineData(Everus, L1, true)]
    [InlineData(Seraphim, L1, false)]
    [InlineData(Everus, Baijini, false)]
    [InlineData(Everus, "Green Gle", false)]
    public void Places_lie_in_the_pools_or_at_the_hub_of_an_empty_side(string pickup, string destination, bool matches)
    {
        // Iron: pickup pool empty, so Everus Harbor stands in (D121); drop-offs L1, L2.
        var result = TemplateMatch.Find(Scan(Key(HaulRank.Rookie, HaulSize.Medium, true, 50_500), [pickup], (destination, "Iron (Ore)", 94)), [Iron], Places);

        if (matches)
        {
            result.Should().Be(new MatchResult.Matched(Iron));
        }
        else
        {
            result.Should().BeOfType<MatchResult.None>().Which.Reason.Should().StartWith("places outside the template's pools: ").And.Contain(destination);
        }
    }

    [Fact]
    public void A_system_wide_template_without_places_takes_any_place()
    {
        var wide = T([], [], reward: 289_000, rank: HaulRank.Senior, areaKey: "Stanton", shape: HaulShape.SingleToMulti, stops: 3,
            legs: [Leg("Tin", 41, 144), Leg("Tin", 41, 144), Leg("Tin", 41, 144)]);

        TemplateMatch.Find(Scan(Key(HaulRank.Senior, HaulSize.Medium, false, 289_000), [Everus], (Baijini, "Tin", 113), ("Port Tressler", "Tin", 129), (Seraphim, "Tin", 91)), [wide], Places)
            .Should().Be(new MatchResult.Matched(wide), "Port Tressler isn't among these positions, and needn't be");
    }

    [Fact]
    public void Red_Wind_never_compares_the_reward_and_takes_an_unknown_direct()
    {
        var direct = RedWind(HaulShape.AToB, 1, ["Checkmate"], ["Ruin Station"], Leg("Carbon", 25, 175, CrateSize.Eight)) with { Direct = true };
        var notDirect = direct with { Uuid = "not", Direct = false };

        TemplateMatch.Find(Scan(RedWindKey(reward: 1), ["Checkmate"], ("Ruin Station", "Carbon", 80)), [direct, notDirect], Places)
            .Should().BeOfType<MatchResult.Ambiguous>().Which.Candidates.Should().Equal([direct, notDirect], "the card doesn't say whether it is direct");
        TemplateMatch.Find(Scan(RedWindKey(direct: true), ["Checkmate"], ("Ruin Station", "Carbon", 80)), [direct, notDirect], Places)
            .Should().Be(new MatchResult.Matched(direct));
    }

    [Fact]
    public void Red_Wind_of_both_factions_is_ambiguous_on_the_same_bands()
    {
        var hh = RedWind(HaulShape.AToB, 1, ["Checkmate"], ["Ruin Station"], Leg("Carbon", 25, 175, CrateSize.Eight));
        var cfp = hh with { Uuid = "cfp", Faction = HaulFaction.Cfp };

        TemplateMatch.Find(Scan(RedWindKey(), ["Checkmate"], ("Ruin Station", "Carbon", 80)), [hh, cfp], Places)
            .Should().BeOfType<MatchResult.Ambiguous>().Which.Candidates.Should().Equal([hh, cfp]);
    }

    private static ObservationKey Key(HaulRank rank, HaulSize size, bool? direct, long reward) =>
        new(HaulContractor.Covalex, rank, size, direct, reward, []);

    private static ObservationKey RedWindKey(long reward = 532_500, bool? direct = null) =>
        new(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, direct, reward, []);

    private static Observation Scan(ObservationKey key, string[] pickups, params (string Destination, string Commodity, int Scu)[] deliveries) =>
        Observation.Of(Delivered(key, pickups, deliveries));

    private static ObservedScan Delivered(ObservationKey key, string[] pickups, params (string Destination, string Commodity, int Scu)[] deliveries) =>
        new(key with { Commodities = [.. deliveries.Select(d => d.Commodity)] }, null, pickups,
            [.. deliveries.Select(d => new ObservedDelivery(d.Destination, d.Commodity, d.Scu))], Monday);

    private static HaulTemplate RookieDirect(string ore) =>
        T([], [L1, L2], reward: 50_500, rank: HaulRank.Rookie, direct: true, legs: [Leg(ore, 80, 112, CrateSize.Eight)]);

    private static HaulTemplate RedWind(HaulShape shape, int stops, string[] pickups, string[] dropoffs, params HaulLeg[] legs) =>
        T(pickups, dropoffs, reward: null, contractor: HaulContractor.RedWind, rank: HaulRank.Member, system: StarSystem.Pyro, areaKey: "Solar",
            faction: HaulFaction.Hh, shape: shape, stops: stops, legs: legs);

    private static StarPosition Position(string name, StarSystem system) => new(name, name, system, "Manmade", null, 0, 0, 0, []);
}
