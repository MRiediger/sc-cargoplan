using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>Observations per template (T13.1, U3): one entry per key, sets joined, bands widened, counted.</summary>
public class ObservationsTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
    private static readonly ObservationKey SeniorMedium = new(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, Direct: false, Reward: 325_250, ["Waste"]);

    [Fact]
    public void A_first_scan_is_one_entry_counted_once()
    {
        var observations = Observations.Empty.Record(Scan(SeniorMedium, Monday, ("HUR-L3 Thundering Express Station", "Waste", 113)));

        observations.Entries.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Key = SeniorMedium,
            Pickups = new[] { "Everus Harbor" },
            Destinations = new[] { "HUR-L3 Thundering Express Station" },
            Scu = new[] { new ScuBand("Waste", 113, 113) },
            Count = 1,
            FirstUtc = Monday,
            LastUtc = Monday,
        });
    }

    [Fact]
    public void A_second_scan_of_the_same_variant_joins_places_widens_bands_and_counts_two()
    {
        // Design §8.3: the same template drew L3·L4·L5·L2 on one day and L1·L2·L3·L4 on another, SCU rolled anew.
        var key = SeniorMedium with { Commodities = ["Waste", "Scrap"] };
        var first = Scan(key, Monday, ("HUR-L3", "Waste", 113), ("HUR-L4", "Waste", 129), ("HUR-L4", "Scrap", 20));
        var second = Scan(key, Monday.AddHours(2), ("HUR-L1", "Waste", 91), ("HUR-L3", "Waste", 140), ("HUR-L1", "Scrap", 20));

        var observations = Observations.Empty.Record(first).Record(second);

        var entry = observations.Entries.Should().ContainSingle().Subject;
        entry.Destinations.Should().Equal("HUR-L1", "HUR-L3", "HUR-L4");
        entry.Scu.Should().Equal(new ScuBand("Scrap", 20, 20), new ScuBand("Waste", 91, 140));
        entry.Count.Should().Be(2);
        entry.FirstUtc.Should().Be(Monday);
        entry.LastUtc.Should().Be(Monday.AddHours(2));
    }

    [Fact]
    public void Another_reward_rank_size_direct_or_contractor_is_another_entry()
    {
        ObservationKey[] keys =
        [
            SeniorMedium,
            SeniorMedium with { Reward = 289_000 },
            SeniorMedium with { Rank = HaulRank.Experienced },
            SeniorMedium with { Size = HaulSize.Large },
            SeniorMedium with { Direct = true },
            SeniorMedium with { Direct = null },
            SeniorMedium with { Contractor = HaulContractor.RedWind },
            SeniorMedium with { Commodities = ["Tin"] },
        ];

        var observations = keys.Aggregate(Observations.Empty, (all, key) => all.Record(Scan(key, Monday, ("Baijini Point", "Tin", 41))));

        observations.Entries.Select(e => e.Key).Should().Equal(keys, "the entries keep the order they were first seen in");
    }

    [Fact]
    public void The_area_is_the_last_one_read()
    {
        var key = SeniorMedium with { Contractor = HaulContractor.RedWind, Direct = null };
        var observations = Observations.Empty
            .Record(Scan(key, Monday, ("Ruin Station", "Carbon", 200)) with { Area = AreaLevel.Stellar })
            .Record(Scan(key, Monday.AddMinutes(5), ("Ruin Station", "Carbon", 180)));

        observations.Entries.Single().Area.Should().Be(AreaLevel.Stellar, "a scan without the word doesn't unlearn it");
    }

    [Fact]
    public void Recording_leaves_the_observations_it_started_from_as_they_were()
    {
        var once = Observations.Empty.Record(Scan(SeniorMedium, Monday, ("HUR-L3", "Waste", 113)));

        _ = once.Record(Scan(SeniorMedium, Monday.AddHours(1), ("HUR-L1", "Waste", 50)));

        once.Entries.Single().Count.Should().Be(1);
        once.Entries.Single().Destinations.Should().Equal("HUR-L3");
    }

    [Fact]
    public void The_commodities_tell_templates_apart_that_share_rank_size_direct_and_reward()
    {
        // K1 of the T13.3 approval: at Everus four Rookie Direct Medium pay 50,500, one per ore.
        var rookie = new ObservationKey(HaulContractor.Covalex, HaulRank.Rookie, HaulSize.Medium, Direct: true, Reward: 50_500, ["Iron (Ore)"]);

        var observations = Observations.Empty
            .Record(Scan(rookie, Monday, ("HUR-L1 Green Glade Station", "Iron (Ore)", 94)))
            .Record(Scan(rookie with { Commodities = ["Quartz (Raw)"] }, Monday, ("HUR-L2 Faithful Dream Station", "Quartz (Raw)", 88)));

        observations.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void A_key_compares_its_commodities_as_OCR_may_read_them_in_any_order()
    {
        var key = SeniorMedium with { Commodities = ["Hydrogen Fuel", "Quantum Fuel"] };

        (key with { Commodities = ["QUANTUM FUEL", "Hydrogen Fue1", "Hydrogen Fuel"] }).Should().Be(key);
        (key with { Commodities = ["QUANTUM FUEL", "Hydrogen Fue1"] }).GetHashCode().Should().Be(key.GetHashCode());
        key.Should().NotBe(key with { Commodities = ["Hydrogen Fuel"] });
    }

    [Fact]
    public void Names_of_one_place_and_one_commodity_are_joined_to_one()
    {
        // E1 of T13.1: B1 joins "Green Glade" and "Green Glade Station", the longer name stays; bands join as OCR reads.
        var observations = Observations.Empty
            .Record(Scan(SeniorMedium, Monday, ("Green Glade", "Waste", 40)))
            .Record(Scan(SeniorMedium, Monday.AddMinutes(1), ("Green Glade Station", "WASTE", 60), ("Melodic Fieids", "Waste", 50)));

        var entry = observations.Entries.Single();
        entry.Destinations.Should().Equal("Green Glade Station", "Melodic Fieids");
        entry.Scu.Should().Equal(new ScuBand("Waste", 40, 60));
    }

    [Fact]
    public void Folding_joins_names_a_file_kept_apart_and_changes_nothing_the_second_time()
    {
        var unfolded = new Observations(
        [
            new Observation(SeniorMedium, null, ["Everus Harbor", "Everus Harbor Station"], ["Green Glade", "Green Glade Station", "HUR-L1 Green Glade Station"],
                [new ScuBand("Waste", 40, 40), new ScuBand("WASTE", 60, 60)], 2, Monday, Monday),
        ]);

        var folded = unfolded.Folded();

        var entry = folded.Entries.Single();
        entry.Pickups.Should().Equal("Everus Harbor Station");
        entry.Destinations.Should().Equal(["Green Glade Station", "HUR-L1 Green Glade Station"], "the L code is no B1 form; T13.3 resolves it against the positions");
        entry.Scu.Should().Equal(new ScuBand("Waste", 40, 60));
        entry.Count.Should().Be(2);
        folded.Folded().Entries.Should().BeEquivalentTo(folded.Entries, options => options.WithStrictOrdering());
    }

    // --- One name per place in routes.json (E3 of the T13.3a approval) ---

    private static StarPosition At(string name, StarSystem system) => new(name, name, system, "Manmade", null, 0, 0, 0, []);

    private static readonly Positions Places = new(
    [
        At("Everus Harbor", StarSystem.Stanton),
        At("HUR-L1 Green Glade Station", StarSystem.Stanton),
        At("HUR-L3 Thundering Express Station", StarSystem.Stanton),
        At("Ruin Station", StarSystem.Pyro),
    ]);

    [Fact]
    public void Named_gives_each_place_its_positions_name_on_both_sides()
    {
        // Martin's routes.json (24 Nov): the card writes pickups with the L code, destinations without.
        var observations = Observations.Empty
            .Record(Scan(SeniorMedium, Monday, ("Thundering Express Station", "Waste", 40), ("Green Glade", "Waste", 30), ("Nowhere Station", "Waste", 20)))
            .Record(new ObservedScan(SeniorMedium, null, ["HUR-L3 Thundering Express Station"], [new ObservedDelivery("HUR-L1 Green Glade Station", "Waste", 50)], Monday));

        var entry = observations.Named(Places).Entries.Single();

        entry.Pickups.Should().Equal("Everus Harbor", "HUR-L3 Thundering Express Station");
        entry.Destinations.Should().Equal(["HUR-L1 Green Glade Station", "HUR-L3 Thundering Express Station", "Nowhere Station"], "a place no position answers to stays as read");
        entry.Scu.Should().Equal(new ScuBand("Waste", 20, 50));
        entry.Count.Should().Be(2);
    }

    [Fact]
    public void Named_resolves_in_the_contractors_system_and_changes_nothing_the_second_time()
    {
        var redWind = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, null, 532_250, ["Carbon"]);
        var observations = Observations.Empty.Record(new ObservedScan(
            redWind, null, ["Ruin"], [new ObservedDelivery("Everus Harbor", "Carbon", 200)], Monday));

        var named = observations.Named(Places);

        named.Entries.Single().Pickups.Should().Equal("Ruin Station");
        named.Entries.Single().Destinations.Should().Equal(["Everus Harbor"], "a Stanton name in a Pyro contract is none of its places");
        named.Named(Places).Entries.Should().BeEquivalentTo(named.Entries, options => options.WithStrictOrdering());
        observations.Named(Positions.Empty).Should().BeEquivalentTo(observations.Folded(), "without positions it only folds");
    }

    internal static ObservedScan Scan(ObservationKey key, DateTime utc, params (string Destination, string Commodity, int Scu)[] deliveries) =>
        new(key, Area: null, ["Everus Harbor"], [.. deliveries.Select(d => new ObservedDelivery(d.Destination, d.Commodity, d.Scu))], utc);
}
