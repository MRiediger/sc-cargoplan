using CargoPlan.Core.Routing;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>Which scan is an observation (T13.1, U3): offers and accepted contracts read whole, with title and reward.</summary>
public class ScanObservationTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_offer_read_whole_is_observed_with_its_places_and_amounts()
    {
        var observed = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelOffer)).ToObservation(Monday);

        observed.Should().NotBeNull();
        observed!.Key.Should().Be(new ObservationKey(HaulContractor.Covalex, HaulRank.Experienced, HaulSize.Medium, Direct: false, Reward: 191_500, ["Hydrogen Fuel", "Quantum Fuel", "Ship Ammunition"]));
        observed.Pickups.Should().Equal("Everus Harbor");
        observed.Deliveries.Should().BeEquivalentTo(
        [
            new ObservedDelivery("Green Glade Station", "Hydrogen Fuel", 70),
            new ObservedDelivery("Green Glade Station", "Ship Ammunition", 133),
            new ObservedDelivery("Faithful Dream Station", "Quantum Fuel", 81),
            new ObservedDelivery("Faithful Dream Station", "Hydrogen Fuel", 62),
        ]);
        observed.Utc.Should().Be(Monday);
    }

    [Fact]
    public void An_accepted_contract_is_observed_under_the_key_of_its_offer()
    {
        var offer = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelOffer)).ToObservation(Monday);
        var accepted = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelAccepted)).ToObservation(Monday);

        accepted!.Key.Should().Be(offer!.Key);
    }

    [Theory]
    [InlineData(ImportFixtures.HurstonAnyOrder, "view unknown")]
    [InlineData(ImportFixtures.IceAndFood, "no title")]
    [InlineData(ImportFixtures.CarbonOrCut, "no reward read")]
    [InlineData(ImportFixtures.SiliconTypingIn, "in hard doubt")]
    public void A_scan_without_view_title_reward_or_whole_objectives_is_not_observed(string fixture, string why)
    {
        ScannedContract.Read(ImportFixtures.RawText(fixture)).ToObservation(Monday).Should().BeNull(why);
    }

    [Fact]
    public void A_text_without_a_delivery_is_not_observed()
    {
        ScannedContract.Read("Junior Rank - Small Cargo Haul\nReward 50,250\nACCEPT OFFER\n").ToObservation(Monday).Should().BeNull();
    }
}
