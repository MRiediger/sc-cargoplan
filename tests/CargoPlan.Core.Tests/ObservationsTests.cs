using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>Observations per template (T13.1, U3): one entry per key, sets joined, bands widened, counted.</summary>
public class ObservationsTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
    private static readonly ObservationKey SeniorMedium = new(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, Direct: false, Reward: 325_250);

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
        var first = Scan(SeniorMedium, Monday, ("HUR-L3", "Waste", 113), ("HUR-L4", "Waste", 129), ("HUR-L4", "Scrap", 20));
        var second = Scan(SeniorMedium, Monday.AddHours(2), ("HUR-L1", "Waste", 91), ("HUR-L3", "Waste", 140));

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

    internal static ObservedScan Scan(ObservationKey key, DateTime utc, params (string Destination, string Commodity, int Scu)[] deliveries) =>
        new(key, Area: null, ["Everus Harbor"], [.. deliveries.Select(d => new ObservedDelivery(d.Destination, d.Commodity, d.Scu))], utc);
}
