using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>
/// Data test (T13.3, D122): the RawText fixtures through parser, observation, folding and matching against the pinned
/// release (D113). Every Covalex fixture that is an observation (offer or accepted, read whole, title and reward) is one
/// template or has a named reason; the Red Wind fixtures, whose rewards the capture lost, match on their bands alone.
/// A pin bump or a parser change can fail it, which is its job. Quota of ships-2026.11.2: Covalex 5 of 7 matched, 2 with
/// places torn by the capture; Red Wind 3 of 3 ambiguous between the factions' twins.
/// </summary>
public sealed class FixtureMatchTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Lazy<HaulingData> Pinned = new(() =>
        HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder)).Should().BeOfType<HaulingLoad.Loaded>().Which.Data);

    // Fixture → the template's debug name, or "none: " and the start of the reason.
    private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        [ImportFixtures.FuelOffer] = "matched HaulCargo_SingleToMulti2_Processed_Mixed_QTFuelHydroFuelShipAmmo_Stanton1_SupplyGrade1",
        [ImportFixtures.FuelAccepted] = "matched HaulCargo_SingleToMulti2_Processed_Mixed_QTFuelHydroFuelShipAmmo_Stanton1_SupplyGrade1",
        [ImportFixtures.IronCouldntHandle] = "matched HaulCargo_AToB_RawOre_Iron_Stanton1_SupplyGrade1",
        [ImportFixtures.SiliconAtMost] = "matched HaulCargo_AToB_NonMetal_Silicon_Stanton1_SupplyGrade",
        [ImportFixtures.WasteCollectWrapped] = "matched HaulCargo_Multi4ToSingle_Waste_Waste_Stanton1_SmallGrade",
        [ImportFixtures.WasteTornAbov] = "none: places outside the template's pools: HUR-L1 Green Gle",
        [ImportFixtures.WasteTornAboy] = "none: places outside the template's pools: HUR-L1 Green Glad",
    };

    public static TheoryData<string> CovalexObservations() =>
    [
        .. ContractTextParserTests.AllRawTextFixtures().Cast<object[]>().Select(row => (string)row[0])
            .Where(f => ScannedContract.Read(ImportFixtures.RawText(f)) is { Title.Contractor: HaulContractor.Covalex } scanned && scanned.ToObservation(Monday) is not null),
    ];

    [Fact]
    public void Every_Covalex_observation_among_the_fixtures_is_expected() =>
        CovalexObservations().Cast<object[]>().Select(row => (string)row[0]).Should().BeEquivalentTo(Expected.Keys);

    [Theory]
    [MemberData(nameof(CovalexObservations))]
    public void A_Covalex_fixture_is_one_template_or_names_why_not(string fixture)
    {
        var observation = Observation.Of(ScannedContract.Read(ImportFixtures.RawText(fixture)).ToObservation(Monday)!);

        Describe(Find(observation)).Should().StartWith(Expected[fixture]);
    }

    [Theory]
    [InlineData(ImportFixtures.CarbonOrCut, "StationToRuin_Carbon_CargoHauling_Multi2ToSingle")]
    [InlineData(ImportFixtures.PotassiumOrCut, "StationToRuin_Potassium_CargoHauling_Multi3ToSingle")]
    [InlineData(ImportFixtures.WasteOrSmaller, "RuinToStation_Waste_CargoHauling_SingleToMulti2")]
    public void A_Red_Wind_fixture_matches_on_its_bands_between_the_factions_twins(string fixture, string variant)
    {
        var result = Find(RedWind(ImportFixtures.RawText(fixture)));

        result.Should().BeOfType<MatchResult.Ambiguous>().Which.Candidates.Select(c => c.DebugName).Should().BeEquivalentTo(
            $"RedWind_Pyro_SupplyGrade_Solar_HH_{variant}", $"RedWind_Pyro_SupplyGrade_Solar_CFP_{variant}");
    }

    [Fact]
    public void Red_Wind_matches_the_same_with_the_title_of_the_data_as_with_the_card()
    {
        // Issue #22: the card says "Member | Stellar Medium Haul | to Ruin Station", the data "Member Hauler Needed for
        // Medium Shipment". Matching never reads the title's text.
        var card = ImportFixtures.RawText(ImportFixtures.CarbonOrCut);
        var data = "Member Hauler Needed for Medium Shipment" + card[card.IndexOf('\n')..];

        var byCard = Find(RedWind(card)).Should().BeOfType<MatchResult.Ambiguous>().Subject;
        var byData = Find(RedWind(data)).Should().BeOfType<MatchResult.Ambiguous>().Subject;

        ScannedContract.Read(data).Title!.Area.Should().BeNull("the data's form names no level: it is the data's title that was read");
        byData.Candidates.Should().Equal(byCard.Candidates);
    }

    [Fact]
    public void Two_ores_for_one_reward_stay_two_observations_each_its_template()
    {
        // K1 of the T13.3 approval: without the commodities in the key the two scans would be one observation of two
        // ores, which matches no template.
        var iron = ImportFixtures.RawText(ImportFixtures.IronCouldntHandle);
        var quartz = iron.Replace("Iron (Ore)", "Quartz (Raw)", StringComparison.Ordinal);

        var observations = Observations.Empty
            .Record(ScannedContract.Read(iron).ToObservation(Monday)!)
            .Record(ScannedContract.Read(quartz).ToObservation(Monday)!);

        observations.Entries.Select(e => Describe(Find(e))).Should().Equal(
            "matched HaulCargo_AToB_RawOre_Iron_Stanton1_SupplyGrade1", "matched HaulCargo_AToB_RawOre_Quartz_Stanton1_SupplyGrade");
    }

    private static MatchResult Find(Observation observation) => TemplateMatch.Find(observation, Pinned.Value.Templates, Pinned.Value.Positions);

    // The captures lost the Red Wind reward; Red Wind never matches on it (D122), so any reward does.
    private static Observation RedWind(string rawText) => Observation.Of((ScannedContract.Read(rawText) with { Reward = 1 }).ToObservation(Monday)!);

    private static string Describe(MatchResult result) => result switch
    {
        MatchResult.Matched m => $"matched {m.Template.DebugName}",
        MatchResult.Ambiguous a => $"ambiguous {string.Join(", ", a.Candidates.Select(c => c.DebugName))}",
        MatchResult.None n => $"none: {n.Reason}",
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };
}
