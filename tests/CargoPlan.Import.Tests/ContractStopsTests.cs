using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Tests;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>
/// A contract's text as the plan's stops (D39, D40): <see cref="ContractStops.Of"/> over what the parser read. The texts are
/// real captures (Fixtures/RawText); these cases came from the database reader's tests and outlive it (T10.4a), since
/// a scan goes the same way (<see cref="ScannedContract"/>).
/// </summary>
public class ContractStopsTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>?> NoPickups = new Dictionary<string, IReadOnlyList<string>?>();

    private static (IReadOnlyList<MissionStop> Stops, CrateSize? Max) Read(string raw)
    {
        var parsed = ContractTextParser.Parse(raw);
        return (ContractStops.Of(parsed.Deliveries, parsed.MaxCrateSize, NoPickups), parsed.MaxCrateSize);
    }

    [Fact]
    public void A_text_with_three_places_gives_three_stops_with_assumed_sizes()
    {
        var (stops, max) = Read(ImportFixtures.RawText(ImportFixtures.StimsSplit));

        stops.Should().BeEquivalentTo(
            [
                new MissionStop("Thundering Express Station", 1, [new MissionItem("Stims", 4, CrateSize.Four)]),
                new MissionStop("Faithful Dream Station", 2, [new MissionItem("Stims", 5, CrateSize.Four)]),
                new MissionStop("High Course Station", 3, [new MissionItem("Stims", 3, CrateSize.Two)]),
            ],
            o => o.WithStrictOrdering());
        max.Should().BeNull("no size in the text: assumed");
    }

    [Fact]
    public void The_accepted_tab_gives_full_names()
    {
        Read(ImportFixtures.RawText(ImportFixtures.StimsAcceptedTab)).Stops.Select(s => s.Destination)
            .Should().Equal("Green Glade Station", "High Course Station", "Thundering Express Station");
    }

    [Fact]
    public void Two_commodities_to_one_place_are_one_stop_with_two_items()
    {
        Read(ImportFixtures.RawText(ImportFixtures.IceAndFood)).Stops.Should().BeEquivalentTo(
        [
            new MissionStop("Thundering Express", 1,
            [
                new MissionItem("Pressurized Ice", 7, CrateSize.Four),
                new MissionItem("Processed Food", 9, CrateSize.Eight),
            ]),
        ]);
    }

    [Fact]
    public void Misread_names_give_one_stop_per_place()
    {
        // B1: ids 9–12 gave five stops in the game, "Melodic Fieids" and "Thundering Express" among them.
        Read(ImportFixtures.RawText(ImportFixtures.HurstonAnyOrder)).Stops.Should().BeEquivalentTo(
            [
                new MissionStop("Melodic Fields Station", 1,
                [
                    new MissionItem("Pressurized Ice", 6, CrateSize.Four),
                    new MissionItem("Processed Food", 5, CrateSize.Four),
                ]),
                new MissionStop("Faithful Dream Station", 2, [new MissionItem("Processed Food", 5, CrateSize.Four)]),
                new MissionStop("Thundering Express Station", 3, [new MissionItem("Pressurized Ice", 3, CrateSize.Two)]),
            ],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Spellings_that_differ_only_by_ocr_letters_are_one_stop()
    {
        var (stops, _) = Read("""
            DETAILS
            Deliver 0/4 SCU of Stims to Thundering Express Statlon at Hurston's L3.
            Deliver 0/2 SCU of Waste to Thundering Express Station at Hurston's L3.
            ACCEPT OFFER
            """);

        stops.Should().ContainSingle().Which.Items.Should().HaveCount(2);
    }

    [Fact]
    public void The_size_phrase_sets_the_max_crate_size()
    {
        var (stops, max) = Read(ImportFixtures.RawText(ImportFixtures.SiliconMaxSize));

        stops.Should().BeEquivalentTo([new MissionStop("Melodic Fields Station", 1, [new MissionItem("Silicon", 10, CrateSize.One)])]);
        max.Should().Be(CrateSize.One);
    }

    [Theory]
    [InlineData(ImportFixtures.WasteOrSmaller, 124, 82)]
    [InlineData(ImportFixtures.PotassiumOrCut, 215)]
    [InlineData(ImportFixtures.CarbonOrCut, 200)]
    public void Or_smaller_contracts_of_issue_22_plan_with_8s_not_assumed_32s(string fixture, params int[] scus)
    {
        // B9 golden: before, each was "max 32 SCU?" (Endgame 3×32+24+4, Orbituary 2×32+16+2, Potassium 6×32+16+4+2+1,
        // Carbon 6×32+8); the kiosk delivers 8s.
        var (stops, max) = Read(ImportFixtures.RawText(fixture));

        max.Should().Be(CrateSize.Eight);
        var items = stops.SelectMany(s => s.Items).ToList();
        items.Select(i => i.Scu).Should().Equal(scus);
        items.Select(i => Groups(CrateMix.Decompose(i.Scu, i.MaxCrateSize))).Should().Equal(scus.Select(s => s switch
        {
            124 => "15×8+1×4",
            82 => "10×8+1×2",
            215 => "26×8+1×4+1×2+1×1",
            200 => "25×8",
            _ => throw new ArgumentOutOfRangeException(nameof(scus)),
        }));
    }

    /// <summary>Count×size per size, largest first: the golden's own spelling, independent of the app's format.</summary>
    private static string Groups(IEnumerable<CrateSize> crates) =>
        string.Join("+", crates.GroupBy(c => (int)c).OrderByDescending(g => g.Key).Select(g => $"{g.Count()}×{g.Key}"));

    // --- Duplicates in the legend (D38): the same text twice, not the same place twice ---

    /// <summary>The texts as captures 47, 48, 49 of one plan, each with the reward its text names.</summary>
    private static IReadOnlySet<int> DuplicatesOf(params (long Id, string Raw, string? Pickup)[] captures)
    {
        var ship = FrozenShips.Load("hull_a");
        Mission[] missions =
        [
            .. captures.Select((c, i) =>
            {
                var parsed = ContractTextParser.Parse(c.Raw);
                var pickups = c.Pickup is null
                    ? NoPickups
                    : parsed.Deliveries.ToDictionary(d => d.Commodity, d => (IReadOnlyList<string>?)[c.Pickup], StringComparer.OrdinalIgnoreCase);
                return new Mission(i + 1, ContractStops.Of(parsed.Deliveries, parsed.MaxCrateSize, pickups), new ContractSource([c.Id], Reward: parsed.Reward));
            }),
        ];
        var saved = LoadPlanner.Plan(ship, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()]);
        return new HashSet<int>(ContractSync.Duplicates(saved).Select(id => (int)saved.Missions.Single(m => m.Id == id).Contract!.ContractIds.Single()));
    }

    [Fact]
    public void Two_contracts_to_the_same_place_are_no_duplicate_but_a_retry_is()
    {
        // Ids 47/48: Waste 9 to Everus Harbor twice, rewards 50,250 and 72,500. Id 49: a retry of 47, same text.
        DuplicatesOf(
                (47, ImportFixtures.RawText(ImportFixtures.WasteTornAboy), null),
                (48, ImportFixtures.RawText(ImportFixtures.WasteTornAbov), null),
                (49, ImportFixtures.RawText(ImportFixtures.WasteTornAboy), null))
            .Should().Equal(49);
    }

    [Fact]
    public void A_retry_with_another_pickup_text_is_still_a_retry()
    {
        // D38: the same contract gave three pickup texts; the pickup is no part of the duplicate rule.
        DuplicatesOf(
                (47, ImportFixtures.RawText(ImportFixtures.WasteTornAboy), "Melodic Fields"),
                (49, ImportFixtures.RawText(ImportFixtures.WasteTornAboy), "Green Glade"))
            .Should().Equal(49);
    }
}
