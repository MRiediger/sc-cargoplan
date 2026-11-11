using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

public class ScannedContractTests
{
    [Fact]
    public void Accepted_contract_becomes_stops_per_place_with_the_pickup_of_each_item()
    {
        var scanned = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelAccepted));

        scanned.View.Should().Be(ContractView.Accepted);
        scanned.Stops.Should().BeEquivalentTo(
            [
                new MissionStop("Green Glade Station", 1,
                [
                    new MissionItem("Hydrogen Fuel", 55, CrateSize.Sixteen, Pickup: ["Everus Harbor"]),
                    new MissionItem("Ship Ammunition", 115, CrateSize.Sixteen, Pickup: ["Everus Harbor"]),
                ]),
                new MissionStop("Faithful Dream Station", 2,
                [
                    new MissionItem("Quantum Fuel", 136, CrateSize.Sixteen, Pickup: ["Everus Harbor"]),
                    new MissionItem("Hydrogen Fuel", 64, CrateSize.Sixteen, Pickup: ["Everus Harbor"]),
                ]),
            ],
            options => options.WithStrictOrdering());
        scanned.Pickups.Should().Equal("Everus Harbor");
        scanned.Reward.Should().Be(191_500);
        scanned.CrateSizeAssumed.Should().BeFalse();
        scanned.ListedDestinationCount.Should().Be(2);
        scanned.DestinationsIncomplete.Should().BeFalse();
    }

    [Fact]
    public void Several_pickups_of_one_item_stay_in_the_order_of_the_text()
    {
        var scanned = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.WasteCollectWrapped));

        string[] stations = ["HUR-L1 Green Glade Station", "HUR-L4 Melodic Fields Station", "HUR-L3 Thundering Express Station", "HUR-L2 Faithful Dream Station"];
        scanned.Stops.Should().ContainSingle().Which.Items.Should().ContainSingle().Which.Pickup.Should().Equal(stations);
        scanned.Pickups.Should().Equal(stations);
    }

    [Fact]
    public void Contract_without_a_size_assumes_the_largest_crate_that_fits()
    {
        var scanned = ScannedContract.Read(Contract("50,250", "", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor."));

        scanned.CrateSizeAssumed.Should().BeTrue();
        // Records compare their pickup lists by reference, so structurally.
        scanned.Stops.Should().ContainSingle().Which.Items.Should().BeEquivalentTo([new MissionItem("Silicon", 6, CrateSize.Four, Pickup: ["Everus Harbor"])]);
    }

    [Fact]
    public void Text_without_a_delivery_gives_no_stops()
    {
        var scanned = ScannedContract.Read("Covalex Independent Contractor\nDETAILS\nPRIMARY OBJECTIVES\nACCEPT OFFER");

        scanned.Stops.Should().BeEmpty();
        scanned.View.Should().Be(ContractView.Offer);
    }

    [Fact]
    public void Fewer_deliveries_than_the_drop_off_list_names_are_incomplete()
    {
        var text = Contract("60,750", "", "Deliver 0/3 SCU of Stims to Green Glade Station.")
            .Replace("DETAILS\n", "DETAILS\nDROP OFF LOCATIONS (ANY ORDER)\n- Freight elevator at Green Glade Station at Hurston's L1\n- Freight elevator at High Course Station at Hurston's L5\n", StringComparison.Ordinal);

        var scanned = ScannedContract.Read(text);

        scanned.ListedDestinationCount.Should().Be(2);
        scanned.DestinationsIncomplete.Should().BeTrue();
    }

    [Fact]
    public void Key_names_pickups_destination_commodity_pairs_max_and_reward_in_a_fixed_order()
    {
        // The format the plan file will keep (T10.2b): parts in anchor form (B1), each sorted, no SCU (D101).
        ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelOffer)).Key.Should().Be(
            "EVERUSHARBOR | FAITHFUIDREAM:HYDROGENFUEI, FAITHFUIDREAM:QUANTUMFUEI, GREENGIADE:HYDROGENFUEI, GREENGIADE:SHIPAMMUNITION | 16 | 191500");
    }

    [Fact]
    public void The_same_contract_before_and_after_accepting_has_one_key()
    {
        // scan-probe.log, reward 191,500: the game rolled every amount again (70/81/133/62 → 55/136/115/64).
        var offer = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelOffer));
        var accepted = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelAccepted));

        accepted.Key.Should().Be(offer.Key);
        accepted.Stops.SelectMany(s => s.Items).Select(i => i.Scu).Should().NotEqual(offer.Stops.SelectMany(s => s.Items).Select(i => i.Scu));
    }

    [Fact]
    public void Key_does_not_depend_on_the_order_of_the_deliveries_or_their_amounts()
    {
        // scan-probe.log, reward 72,500, four Stims deliveries each: an offer to High Course and an accepted contract to
        // Thundering Express instead, two routes. The same route in another order and with other SCU keeps its key.
        var offer = ScannedContract.Read(Contract("72,500", "They will be packaged up in containers 4 SCU or smaller.",
            Stims(3, "Green Glade Station"), Stims(3, "High Course Station"), "L", Stims(3, "Melodic Fields Station"), "L", Stims(2, "Faithful Dream Station")));
        var accepted = ScannedContract.Read(Contract("72,500", "They will be packaged up in containers 4 SCU or smaller.",
            Stims(2, "Faithful Dream Station"), Stims(2, "Melodic Fields Station"), "0", Stims(2, "Thundering Express Station"), Stims(2, "Green Glade Station")));
        var reordered = ScannedContract.Read(Contract("72,500", "They will be packaged up in containers 4 SCU or smaller.",
            Stims(2, "Melodic Fields Station"), Stims(2, "High Course Station"), Stims(4, "Faithful Dream Station"), Stims(1, "Green Glade Station")));

        reordered.Key.Should().Be(offer.Key);
        accepted.Key.Should().NotBe(offer.Key, "Thundering Express instead of High Course is another route");
    }

    [Fact]
    public void Names_of_one_place_give_one_key()
    {
        // B1: "Station" lost at a line break, "Fieids" read for "Fields".
        var first = ScannedContract.Read(Contract("50,250", "", "Deliver 0/6 SCU of Silicon to Melodic Fieids", "Collect Silicon from HUR-L2 Faithful Dream"));
        var second = ScannedContract.Read(Contract("50,250", "", "Deliver 0/7 SCU of Silicon to Melodic Fields Station.", "Collect Silicon from HUR-L2 Faithful Dream Station."));

        second.Key.Should().Be(first.Key);
    }

    [Fact]
    public void Commodities_swapped_between_two_destinations_are_another_contract()
    {
        // H1: separate lists of destinations and commodities would make these one contract.
        var first = ScannedContract.Read(Contract("81,250", "", "Deliver 0/5 SCU of Stims to Riker.", "Deliver 0/3 SCU of Processed Food to Orison."));
        var second = ScannedContract.Read(Contract("81,250", "", "Deliver 0/5 SCU of Processed Food to Riker.", "Deliver 0/3 SCU of Stims to Orison."));

        second.Key.Should().NotBe(first.Key);
    }

    [Theory]
    [InlineData("50,250", "Max size will be 4 SCU.", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor.")]
    [InlineData("50,500", "Max size will be 1 SCU.", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor.")]
    [InlineData("50,250", "Max size will be 1 SCU.", "Deliver 0/6 SCU of Stims to Riker.", "Collect Stims from Everus Harbor.")]
    [InlineData("50,250", "Max size will be 1 SCU.", "Deliver 0/6 SCU of Silicon to Orison.", "Collect Silicon from Everus Harbor.")]
    [InlineData("50,250", "Max size will be 1 SCU.", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Green Glade Station.")]
    [InlineData("50,250", "", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor.")]
    [InlineData("5O,250", "Max size will be 1 SCU.", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor.")]
    public void Any_other_part_gives_another_key_and_unreadable_is_no_joker(string reward, string max, string delivery, string collect)
    {
        // D101: an unreadable reward or size ("?") is not equal to a number. Rewards repeat between contracts
        // (scan-probe.log: 72,500 twice, 50,250 three times), so a joker would merge two contracts of one route.
        var reference = ScannedContract.Read(Contract("50,250", "Max size will be 1 SCU.", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor."));

        ScannedContract.Read(Contract(reward, max, delivery, collect)).Key.Should().NotBe(reference.Key);
    }

    [Fact]
    public void Unreadable_reward_and_size_stand_in_the_key_as_unknown()
    {
        var scanned = ScannedContract.Read(Contract("5O,250", "", "Deliver 0/6 SCU of Silicon to Riker.", "Collect Silicon from Everus Harbor."));

        scanned.Reward.Should().BeNull();
        scanned.Key.Should().Be("EVERUSHARBOR | RIKER:SIIICON | ? | ?");
    }

    // --- Doubts: a second read when the text may still be typing in (T10.2b2, D103) ---

    [Fact]
    public void A_contract_typed_in_while_captured_is_in_hard_doubt()
    {
        var scanned = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.SiliconTypingIn));

        scanned.Doubts.Should().BeEquivalentTo([ContractDoubt.NoDeliveryRead, ContractDoubt.ObjectiveLineUnread]);
        scanned.InHardDoubt.Should().BeTrue();
    }

    [Theory]
    [InlineData("Deliver 0/9 SCU of Silicon", ContractDoubt.ObjectiveLineUnread)]
    [InlineData("Deliver O/9 SCU of Silicon to", ContractDoubt.ObjectiveLineUnread)]
    [InlineData("Collect Silicon from", ContractDoubt.ObjectiveLineUnread)]
    [InlineData("CoIlect Silicon from", ContractDoubt.ObjectiveLineUnread)]
    public void An_objective_line_that_reads_as_nothing_is_a_hard_doubt(string line, ContractDoubt expected)
    {
        var scanned = ScannedContract.Read(Contract("50,250", "", "Deliver 0/6 SCU of Stims to Riker.", "Collect Stims from Everus Harbor.", line));

        scanned.Doubts.Should().Equal(expected);
        scanned.InHardDoubt.Should().BeTrue();
    }

    [Fact]
    public void Objectives_without_any_delivery_are_a_hard_doubt()
    {
        ScannedContract.Read(Contract("50,250", "")).Doubts.Should().Equal(ContractDoubt.NoDeliveryRead);
    }

    [Fact]
    public void A_text_without_objectives_is_no_doubt_but_no_contract()
    {
        // "No contract in frame" is the scanner's (no DETAILS / PRIMARY OBJECTIVES); this text has neither a delivery nor
        // an objectives heading: nothing typing in.
        var scanned = ScannedContract.Read("Covalex Independent Contractor\nDETAILS\nSome prose.");

        scanned.Stops.Should().BeEmpty();
        scanned.Doubts.Should().BeEmpty();
    }

    [Fact]
    public void A_missing_reward_is_a_soft_doubt()
    {
        var scanned = ScannedContract.Read(Contract("5O,250", "", "Deliver 0/6 SCU of Stims to Riker.", "Collect Stims from Everus Harbor."));

        scanned.Doubts.Should().Equal(ContractDoubt.NoReward);
        scanned.InHardDoubt.Should().BeFalse();
    }

    [Fact]
    public void A_commodity_without_its_collect_line_is_a_soft_doubt()
    {
        var scanned = ScannedContract.Read(Contract("50,250", "",
            "Deliver 0/6 SCU of Stims to Riker.", "Collect Stims from Everus Harbor.", "Deliver 0/4 SCU of Gold to Orison."));

        scanned.Doubts.Should().Equal(ContractDoubt.CommodityWithoutPickup);
        scanned.InHardDoubt.Should().BeFalse();
    }

    [Fact]
    public void A_contract_without_any_collect_line_doubts_no_pickup()
    {
        // ContractTracker's own texts and some contracts have none; only a missing one among others is a doubt.
        ScannedContract.Read(Contract("50,250", "", "Deliver 0/6 SCU of Stims to Riker.")).Doubts.Should().BeEmpty();
    }

    [Fact]
    public void Fewer_deliveries_than_listed_is_a_soft_doubt()
    {
        // E1 (Martin): taken in with its listed count, the legend warns "1 of 2 destinations" as for a capture.
        var text = Contract("60,750", "", "Deliver 0/3 SCU of Stims to Green Glade Station.", "Collect Stims from Everus Harbor.")
            .Replace("DETAILS\n", "DETAILS\nDROP OFF LOCATIONS (ANY ORDER)\n- Freight elevator at Green Glade Station at Hurston's L1\n- Freight elevator at High Course Station at Hurston's L5\n", StringComparison.Ordinal);

        var scanned = ScannedContract.Read(text);

        scanned.Doubts.Should().Equal(ContractDoubt.FewerDestinationsThanListed);
        scanned.InHardDoubt.Should().BeFalse();
    }

    public static TheoryData<string> AllRawTextFixtures() => ContractTextParserTests.AllRawTextFixtures();

    [Theory]
    [MemberData(nameof(AllRawTextFixtures))]
    public void No_complete_capture_is_in_hard_doubt(string fixture)
    {
        // Every fixture but the one typed in: no second read for a text that is all there (Gate 1, T10.1b).
        var scanned = ScannedContract.Read(ImportFixtures.RawText(fixture));

        scanned.InHardDoubt.Should().Be(fixture == ImportFixtures.SiliconTypingIn, "{0}: {1}", fixture, string.Join(", ", scanned.Doubts));
    }

    [Fact]
    public void To_capture_names_the_scan_and_marks_an_offer_estimated()
    {
        var offer = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelOffer)).ToCapture(4);
        var accepted = ScannedContract.Read(ImportFixtures.RawText(ImportFixtures.FuelAccepted)).ToCapture(4);

        offer.Source.Scan.Should().Be(new ScanIdentity(4, accepted.Source.Scan!.Key, true));
        accepted.Source.Scan!.QuantitiesEstimated.Should().BeFalse();
        accepted.Source.Reward.Should().Be(191_500);
        accepted.Source.ListedDestinations.Should().Be(2);
        accepted.Stops.Should().HaveCount(2);
    }

    /// <summary>A contract in the reading order of CargoPlan's own OCR (D99), accepted view.</summary>
    private static string Contract(string reward, string maxPhrase, params string[] objectives) =>
        $"ABANDON\nDETAILS\n{maxPhrase}\nReward {reward}\nContract Deadline N/A\nPRIMARY OBJECTIVES\n{string.Join('\n', objectives)}\nSHARE UNTRACK";

    private static string Stims(int scu, string destination) => $"Deliver 0/{scu} SCU of Stims to {destination} at\nHurston's L1 Lagrange point.\nCollect Stims from Everus Harbor.";
}
