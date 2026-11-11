using System.Text.RegularExpressions;
using CargoPlan.Core.Models;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

public class ContractTextParserTests
{
    private static readonly ParsedDelivery[] StimsDeliveries =
    [
        new("Thundering Express Station", "Stims", 4),
        new("Faithful Dream Station", "Stims", 5),
        new("High Course Station", "Stims", 3),
    ];

    [Fact]
    public void Capture_with_ocr_errors_gives_all_three_destinations()
    {
        // ContractTracker found one destination in this capture; "Dellver", "O0/5" and "O/3" are in the raw text.
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.StimsOcrErrors));

        parsed.Deliveries.Should().Equal(StimsDeliveries);
        parsed.ListedDestinationCount.Should().Be(3);
        parsed.MaxCrateSize.Should().BeNull();
    }

    [Fact]
    public void Split_capture_gives_all_three_destinations()
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.StimsSplit));

        parsed.Deliveries.Should().Equal(StimsDeliveries);
        parsed.ListedDestinationCount.Should().Be(3);
    }

    [Fact]
    public void Two_captures_of_the_same_contract_give_the_same_names()
    {
        // What T5.4's duplicate marking compares: commodity and destinations, already normalised here (D40).
        var split = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.StimsSplit));
        var retry = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.StimsOcrErrors));

        retry.Deliveries.Should().Equal(split.Deliveries);
    }

    [Theory]
    [InlineData(ImportFixtures.WasteListFragments)]
    [InlineData(ImportFixtures.WasteLs)]
    public void List_fragments_before_details_give_no_commodity(string fixture)
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(fixture));

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Everus Harbor", "Waste", 9));
    }

    [Fact]
    public void Max_size_phrase_is_read()
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.SiliconMaxSize));

        parsed.MaxCrateSize.Should().Be(CrateSize.One);
        parsed.Deliveries.Should().Equal(new ParsedDelivery("Melodic Fields Station", "Silicon", 10));
    }

    [Fact]
    public void Bigger_than_phrase_is_read()
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.WasteBiggerThan));

        parsed.MaxCrateSize.Should().Be(CrateSize.Four);
    }

    [Theory]
    [InlineData("Don't\nworry, none of the\ncontainers are bigger than\n8 SCU.", CrateSize.Eight)]
    [InlineData("1. Pick up cargo. Max size\nwill be 2\nSCU.", CrateSize.Two)]
    public void Size_phrase_may_contain_line_breaks(string phrase, CrateSize expected)
    {
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\n{phrase}\nACCEPT OFFER");

        parsed.MaxCrateSize.Should().Be(expected);
    }

    [Fact]
    public void Size_that_is_no_crate_size_counts_as_missing()
    {
        var parsed = ContractTextParser.Parse("DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\nMax size will be 3 SCU.");

        parsed.MaxCrateSize.Should().BeNull();
    }

    [Fact]
    public void Missing_size_phrase_gives_none()
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.StimsSplit));

        parsed.MaxCrateSize.Should().BeNull();
    }

    [Fact]
    public void Truncated_fragments_of_one_capture_become_one_destination()
    {
        // "to Thundering Express" and "to Thundering": the line with "Station" was not captured for either.
        // Known limit: no full name anywhere in the text, so the longer fragment wins.
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.IceAndFood));

        parsed.Deliveries.Should().Equal(
            new ParsedDelivery("Thundering Express", "Pressurized Ice", 7),
            new ParsedDelivery("Thundering Express", "Processed Food", 9));
        parsed.ListedDestinationCount.Should().BeNull();
    }

    [Fact]
    public void Truncated_destination_is_completed_from_the_drop_off_list()
    {
        const string text = """
            DETAILS
            Deliver 0/4 SCU of Stims to Thundering
            DROP OFF LOCATIONS (ANY ORDER)
            - Freight elevator at Thundering Express Station at Hurston's L3
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thundering Express Station", "Stims", 4));
        parsed.ListedDestinationCount.Should().Be(1);
    }

    [Fact]
    public void Capture_from_the_accepted_tab_gives_full_names()
    {
        // No ACCEPT OFFER; "to Thunde" breaks mid-word, "to High Course" is cut off: both completed from the drop-off list.
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.StimsAcceptedTab));

        parsed.Deliveries.Should().Equal(
            new ParsedDelivery("Green Glade Station", "Stims", 3),
            new ParsedDelivery("High Course Station", "Stims", 4),
            new ParsedDelivery("Thundering Express Station", "Stims", 4));
        parsed.ListedDestinationCount.Should().Be(3);
        parsed.MaxCrateSize.Should().BeNull();
    }

    [Fact]
    public void Ocr_errors_in_the_list_and_the_fragment_still_complete()
    {
        // B1: "Fieids" for "Fields" in the delivery line, "alevator" for "elevator" in the list. Both gave a stop of their
        // own in the game ("Melodic Fieids", "Thundering Express").
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.HurstonAnyOrder));

        parsed.Deliveries.Should().Equal(
            new ParsedDelivery("Melodic Fields Station", "Pressurized Ice", 6),
            new ParsedDelivery("Faithful Dream Station", "Processed Food", 5),
            new ParsedDelivery("Thundering Express Station", "Pressurized Ice", 3),
            new ParsedDelivery("Melodic Fields Station", "Processed Food", 5));
        parsed.ListedDestinationCount.Should().Be(3);
    }

    [Theory]
    [InlineData("Freight alevator at")]
    [InlineData("Freight levator at")]
    public void List_entry_with_a_misread_elevator_counts(string entry)
    {
        var text = $"""
            DETAILS
            Deliver 0/4 SCU of Stims to Thundering
            DROP OFF LOCATIONS (ANY ORDER)
            - {entry} Thundering Express Station at Hurston's L3
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thundering Express Station", "Stims", 4));
        parsed.ListedDestinationCount.Should().Be(1);
    }

    [Fact]
    public void Fragment_with_a_misread_letter_takes_the_list_spelling()
    {
        const string text = """
            DETAILS
            Deliver 0/6 SCU of Pressurized Ice to Melodic Fieids
            DROP OFF LOCATIONS (ANY ORDER)
            - Freight elevator at Melodic Fields Station at Hurston's L4
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Melodic Fields Station", "Pressurized Ice", 6));
    }

    [Fact]
    public void Fragment_matching_several_list_entries_stays()
    {
        const string text = """
            DETAILS
            Deliver 0/4 SCU of Stims to Thunde
            DROP OFF LOCATIONS (ANY ORDER)
            - Freight elevator at Thundering Express Station at Hurston's L3
            - Freight elevator at Thunderhead Station at Hurston's L4
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thunde", "Stims", 4));
    }

    [Fact]
    public void Pick_up_list_and_prose_from_a_pickup_do_not_complete_a_destination()
    {
        // A pickup sharing the prefix must not be taken for a destination: the pick-up list and "from a freight elevator".
        const string text = """
            DETAILS
            Deliver 0/9 SCU of Waste to Thunde
            1. Pick up cargo from a freight elevator at Thundering Express Station at Hurston's L3.
            PICK UP LOCATIONS (ANY ORDER)
            - Freight elevator at Thundering Express Station at Hurston's L3
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thunde", "Waste", 9));
    }

    [Theory]
    [InlineData("Taken to a freight elevator at Thundering Express Station at Hurston's L3.")]
    [InlineData("2. Deliver to a freight elevator at Thundering\nExpress Station above Hurston.")]
    [InlineData("taken to a freight elevator at Thundering Express Station. Don't")]
    public void Prose_with_to_completes_a_destination(string prose)
    {
        // The verb doesn't matter, "to a freight elevator at" names a destination (T5.3b).
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/9 SCU of Waste to Thunde\n{prose}\nACCEPT OFFER");

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thundering Express Station", "Waste", 9));
    }

    [Fact]
    public void Drop_off_list_completes_before_the_prose()
    {
        const string text = """
            DETAILS
            Deliver 0/4 SCU of Stims to Thunde
            Deliver to a freight elevator at Thunderhead Station at Hurston's L4.
            DROP OFF LOCATIONS (ANY ORDER)
            - Freight elevator at Thundering Express Station at Hurston's L3
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thundering Express Station", "Stims", 4));
    }

    [Theory]
    [InlineData(ImportFixtures.WasteTornAboy)]
    [InlineData(ImportFixtures.WasteTornAbov)]
    public void Torn_above_at_the_end_of_a_delivery_line_is_dropped(string fixture)
    {
        // Ids 47/48: "to Everus Harbor aboy" / "abov", the line broke inside "above".
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(fixture));

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Everus Harbor", "Waste", 9));
    }

    [Fact]
    public void Size_phrase_lost_in_the_ocr_stays_missing()
    {
        // Id 48: "Don't" is followed by "D", the line with "bigger than" was not read.
        ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.WasteTornAbov)).MaxCrateSize.Should().BeNull();
    }

    [Fact]
    public void Overlay_text_in_the_capture_gives_no_commodity_or_destination()
    {
        // Our own legend in ContractTracker's capture region: "MISC Hull A", "No plan. Open t", "Open editor".
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.SiliconOverlayText));

        parsed.Deliveries.Should().Equal(new ParsedDelivery("High Course Station", "Silicon", 7));
        parsed.MaxCrateSize.Should().Be(CrateSize.One);
        parsed.ListedDestinationCount.Should().BeNull();
        parsed.Reward.Should().BeNull();
    }

    [Theory]
    [InlineData("Max size willb 1 SCU.", CrateSize.One)]
    [InlineData("Max size will be l SCU.", CrateSize.One)]
    [InlineData("Maxsize will be I6 SCU.", CrateSize.Sixteen)]
    [InlineData("none of the containers are bigger than O8 SCU.", CrateSize.Eight)]
    [InlineData("containers are biggerthan |\nSCU.", CrateSize.One)]
    public void Size_phrase_tolerates_misread_words_and_digits(string phrase, CrateSize expected)
    {
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\n{phrase}\nACCEPT OFFER");

        parsed.MaxCrateSize.Should().Be(expected);
    }

    [Theory]
    [InlineData(ImportFixtures.WasteOrSmaller)]
    [InlineData(ImportFixtures.PotassiumOrCut)]
    [InlineData(ImportFixtures.CarbonOrCut)]
    public void Or_smaller_phrase_of_issue_22_is_read(string fixture)
    {
        // B9: "bunch of 8 SCU or smaller", and "(8 SCU or" whose "smaller)" OCR lost.
        ContractTextParser.Parse(ImportFixtures.RawText(fixture)).MaxCrateSize.Should().Be(CrateSize.Eight);
    }

    [Theory]
    [InlineData("bunch of 8 SCU or smaller waste containers", CrateSize.Eight)]
    [InlineData("bunch of 8 SCU or\nsmaller waste containers", CrateSize.Eight)]
    [InlineData("bunch of 8\nSCU or smaller", CrateSize.Eight)]
    [InlineData("They got cargo (4 SCU or smaller) to move.", CrateSize.Four)]
    [InlineData("They got cargo (16 SCU or", CrateSize.Sixteen)]
    [InlineData("They got cargo ( 2 SCU or\nFROM RUIN STATION", CrateSize.Two)]
    [InlineData("bunch of 8SCU orsmaller", CrateSize.Eight)]
    [InlineData("bunch of O8 SCU or smaller", CrateSize.Eight)]
    [InlineData("They got cargo (l SCU or", CrateSize.One)]
    [InlineData("bunch of8 SCU or smaller", CrateSize.Eight)]
    public void Or_smaller_phrase_tolerates_breaks_spacing_and_digits(string phrase, CrateSize expected)
    {
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\n{phrase}\nACCEPT OFFER");

        parsed.MaxCrateSize.Should().Be(expected);
    }

    [Theory]
    [InlineData("They got cargo (8 SCU or more) to move.")]
    [InlineData("They got cargo (8 SCU or\nlarger)")]
    [InlineData("They got cargo 8 SCU or")]
    [InlineData("Deliver 0/8 SCU or smaller")]
    [InlineData("Deliver 0/ 8 SCU or smaller")]
    [InlineData("Deliver 0/124 SCU or smaller")]
    [InlineData("bunch of 3 SCU or smaller")]
    public void Or_phrase_that_names_no_max_counts_as_missing(string phrase)
    {
        // Not a max: "or more/larger"; "N SCU or" without its bracket and without "smaller"; the amount of a delivery
        // ("0/8", also its tail "24" of "0/124"); no crate size.
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\n{phrase}\nACCEPT OFFER");

        parsed.MaxCrateSize.Should().BeNull();
    }

    [Fact]
    public void Corrected_size_that_is_no_crate_size_counts_as_missing()
    {
        // "l2" reads as 12: no crate size, so "?" rather than a wrong size without one (T5.3b).
        var parsed = ContractTextParser.Parse("DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\nMax size will be l2 SCU.");

        parsed.MaxCrateSize.Should().BeNull();
    }

    [Theory]
    [InlineData(ImportFixtures.WasteTornAboy, 50_250L)]
    [InlineData(ImportFixtures.WasteTornAbov, 72_500L)]
    public void Reward_is_read_from_the_start_of_the_text(string fixture, long expected) =>
        ContractTextParser.Parse(ImportFixtures.RawText(fixture)).Reward.Should().Be(expected);

    [Theory]
    [InlineData("Reward\n5O,250\nDETAILS")]
    [InlineData("Junior Rank - Small Cargo Haul\nReward\n50,250\nDETAILS")]
    [InlineData("Reward\nContract Availability\nDETAILS")]
    public void Unreadable_or_misplaced_reward_is_unknown(string text) =>
        ContractTextParser.Parse(text).Reward.Should().BeNull();

    [Theory]
    [InlineData(ImportFixtures.FuelOffer, 191_500L)]
    [InlineData(ImportFixtures.FuelAccepted, 191_500L)]
    [InlineData(ImportFixtures.SiliconAtMost, 50_500L)]
    [InlineData(ImportFixtures.WasteCollectWrapped, 72_500L)]
    public void Reward_is_read_from_its_line_after_the_prose(string fixture, long expected) =>
        ContractTextParser.Parse(ImportFixtures.RawText(fixture)).Reward.Should().Be(expected);

    // The sign before the amount is aUEC's, read as "±" at 1080p (S10.0 set-1080s c17, T10.2a hand test).
    [Theory]
    [InlineData("Reward 81,250", 81_250L)]
    [InlineData("Reward 81.250", 81_250L)]
    [InlineData("  Reward   81,250  ", 81_250L)]
    [InlineData("Reward 1,081,250", 1_081_250L)]
    [InlineData("Reward 750", 750L)]
    [InlineData("Reward ± 51,000", 51_000L)]
    [InlineData("Reward ¤ 51,000", 51_000L)]
    [InlineData("Reward ¤51,000", 51_000L)]
    public void Reward_line_anywhere_in_the_text_is_read(string line, long expected) =>
        ContractTextParser.Parse(InColumns(line)).Reward.Should().Be(expected);

    [Theory]
    [InlineData("Reward 8l,250")]
    [InlineData("Reward 81,25")]
    [InlineData("Reward")]
    [InlineData("The Reward 81,250")]
    [InlineData("Reward 81,250 aUEC")]
    [InlineData("Reward 8 51,000")]
    [InlineData("Reward ±± 51,000")]
    [InlineData("Reward x 51,000")]
    public void Unreadable_reward_line_is_unknown(string line) =>
        ContractTextParser.Parse(InColumns(line)).Reward.Should().BeNull();

    [Theory]
    [InlineData(ImportFixtures.SiliconAtMost, CrateSize.Eight)]
    [InlineData(ImportFixtures.IronCouldntHandle, CrateSize.Eight)]
    [InlineData(ImportFixtures.FuelOffer, CrateSize.Sixteen)]
    [InlineData(ImportFixtures.FuelAccepted, CrateSize.Sixteen)]
    [InlineData(ImportFixtures.WasteCollectWrapped, CrateSize.Four)]
    public void Max_size_of_a_scan_is_read(string fixture, CrateSize expected) =>
        ContractTextParser.Parse(ImportFixtures.RawText(fixture)).MaxCrateSize.Should().Be(expected);

    [Theory]
    [InlineData("At most the containers will be 8 SCU in size.", CrateSize.Eight)]
    [InlineData("above Hurston. At most the\ncontainers will be 8 SCU in size.", CrateSize.Eight)]
    [InlineData("Lagrange point. At most the containers willbe 1 SCU in size.", CrateSize.One)]
    [InlineData("At most the containers will be\n16 SCU in size.", CrateSize.Sixteen)]
    [InlineData("At most the containers will be O8 SCU in size.", CrateSize.Eight)]
    [InlineData("Atmost the containers will be 4 SCU in size.", CrateSize.Four)]
    [InlineData("their ship couldn't handle 8\nSCU containers, so", CrateSize.Eight)]
    [InlineData("their ship couldn’t handle 16 SCU containers", CrateSize.Sixteen)]
    [InlineData("their ship couldnt handle 4 SCU containers", CrateSize.Four)]
    public void At_most_and_couldnt_handle_phrases_are_read(string phrase, CrateSize expected)
    {
        // S10.0 "+max2": the two wordings of the size the parser didn't know yet, line breaks allowed.
        var parsed = ContractTextParser.Parse($"DETAILS\n{phrase}\nPRIMARY OBJECTIVES\nDeliver 0/9 SCU of Waste to Everus Harbor above\nACCEPT OFFER");

        parsed.MaxCrateSize.Should().Be(expected);
    }

    [Theory]
    [InlineData("At most 3 days for the run.")]
    [InlineData("At most the containers will be 3 SCU in size.")]
    [InlineData("their ship couldn't handle the load")]
    public void At_most_or_couldnt_handle_without_a_crate_size_counts_as_missing(string phrase)
    {
        var parsed = ContractTextParser.Parse($"DETAILS\n{phrase}\nPRIMARY OBJECTIVES\nDeliver 0/9 SCU of Waste to Everus Harbor above\nACCEPT OFFER");

        parsed.MaxCrateSize.Should().BeNull();
    }

    [Theory]
    [InlineData("SHARE TRACK")]
    [InlineData("SHARE UNTRACK")]
    [InlineData("SHARE  UNTRACK ")]
    [InlineData("SHAREUNTRACK")]
    public void Share_line_of_the_accepted_view_ends_the_block(string end)
    {
        // The accepted view has no ACCEPT OFFER; its last line is "SHARE TRACK" or, once tracked, "SHARE UNTRACK".
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/9 SCU of Waste to Everus Harbor above\n{end}\nDeliver 0/1 SCU of Noise to Nowhere at");

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Everus Harbor", "Waste", 9));
    }

    [Fact]
    public void Delivered_count_read_as_the_letter_O_keeps_every_delivery()
    {
        // Guard (T10.2a): T10.1b counted "O/" in 254 of 877 delivery lines over the game; the pattern reads them already.
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.FuelAccepted));

        parsed.Deliveries.Should().Equal(
            new ParsedDelivery("Green Glade Station", "Hydrogen Fuel", 55),
            new ParsedDelivery("Faithful Dream Station", "Quantum Fuel", 136),
            new ParsedDelivery("Green Glade Station", "Ship Ammunition", 115),
            new ParsedDelivery("Faithful Dream Station", "Hydrogen Fuel", 64));
        parsed.ListedDestinationCount.Should().Be(2);
    }

    [Fact]
    public void Collect_line_after_each_delivery_gives_its_pickup()
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.FuelOffer));

        parsed.Pickups.Should().Equal(
            new ParsedPickup("Hydrogen Fuel", "Everus Harbor"),
            new ParsedPickup("Quantum Fuel", "Everus Harbor"),
            new ParsedPickup("Ship Ammunition", "Everus Harbor"),
            new ParsedPickup("Hydrogen Fuel", "Everus Harbor"));
    }

    [Fact]
    public void Several_collect_lines_of_one_delivery_give_every_pickup_also_across_a_line_break()
    {
        // Waste: four stations, one delivery; the third place breaks before "Station.".
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.WasteCollectWrapped));

        parsed.Pickups.Should().Equal(
            new ParsedPickup("Waste", "HUR-L1 Green Glade Station"),
            new ParsedPickup("Waste", "HUR-L4 Melodic Fields Station"),
            new ParsedPickup("Waste", "HUR-L3 Thundering Express Station"),
            new ParsedPickup("Waste", "HUR-L2 Faithful Dream Station"));
        parsed.Deliveries.Should().Equal(new ParsedDelivery("Everus Harbor", "Waste", 10));
    }

    [Theory]
    [InlineData(ImportFixtures.SiliconAtMost, "Silicon", "HUR-L2 Faithful Dream Station")]
    [InlineData(ImportFixtures.IronCouldntHandle, "Iron (Ore)", "Everus Harbor")]
    public void Collect_line_of_a_single_delivery_is_read(string fixture, string commodity, string place) =>
        ContractTextParser.Parse(ImportFixtures.RawText(fixture)).Pickups.Should().Equal(new ParsedPickup(commodity, place));

    [Theory]
    [InlineData("HUR-L3 Thundering Express", "Station.", "HUR-L3 Thundering Express Station")]
    [InlineData("Green Glade", "Station. Then the next sentence", "Green Glade Station")]
    [InlineData("Everus Harbor above", "Hurston.", "Everus Harbor")]
    [InlineData("Everus Harbor", "Deliver O/6 SCU of Processed Food to Green Glade", "Everus Harbor")]
    [InlineData("Everus Harbor", "L", "Everus Harbor")]
    [InlineData("Everus Harbor", "0", "Everus Harbor")]
    [InlineData("Everus Harbor", "SHARE UNTRACK", "Everus Harbor")]
    [InlineData("Everus Harbor", "DROP OFF LOCATIONS (ANY ORDER)", "Everus Harbor")]
    [InlineData("HUR-L4 Melodic Fields", "taken to a freight elevator at Everus Harbor above Hurston. Don't", "HUR-L4 Melodic Fields")]
    [InlineData("HUR-L1 Green Glade", "1. Pick up cargo from a freight elevator at Green Glade Station at", "HUR-L1 Green Glade")]
    [InlineData("HUR-L3 Thundg", "ress", "HUR-L3 Thundg")]
    [InlineData("Everus Harbor", "Collect Stims from Riker.", "Everus Harbor")]
    public void Collect_line_without_a_sentence_end_takes_only_a_short_ending_of_words(string place, string next, string expected)
    {
        // Our OCR breaks a long place ("… Thundering Express" / "Station."); ContractTracker's text puts prose, a list or a
        // heading after a Collect line, and the objective marker ("L", "0") stands before the next delivery (T10.2a).
        var parsed = ContractTextParser.Parse($"DETAILS\nDeliver 0/7 SCU of Stims to Riker\nCollect Stims from {place}\n{next}\nACCEPT OFFER");

        parsed.Pickups.Should().StartWith(new ParsedPickup("Stims", expected));
    }

    [Theory]
    [InlineData("CoIlect Stims from Everus Harbor.")]
    [InlineData("Col1ect Stims from Everus Harbor.")]
    public void Collect_with_a_misread_letter_is_read(string line) =>
        ContractTextParser.Parse($"DETAILS\nDeliver 0/7 SCU of Stims to Riker\n{line}\nACCEPT OFFER").Pickups
            .Should().Equal(new ParsedPickup("Stims", "Everus Harbor"));

    [Theory]
    [InlineData("1. Pick up cargo from a freight elevator at Faithful Dream Station")]
    [InlineData("Need the trash collected from a few Hurston locations and then")]
    [InlineData("Collect from Everus Harbor.")]
    public void Prose_with_from_is_no_pickup(string line) =>
        ContractTextParser.Parse($"DETAILS\n{line}\nDeliver 0/7 SCU of Stims to Riker\nACCEPT OFFER").Pickups.Should().BeEmpty();

    [Fact]
    public void Collect_lines_outside_the_block_are_not_read()
    {
        var parsed = ContractTextParser.Parse("Collect Noise from Nowhere.\nDETAILS\nDeliver 0/7 SCU of Stims to Riker\nACCEPT OFFER\nCollect Noise from Nowhere.");

        parsed.Pickups.Should().BeEmpty();
    }

    [Fact]
    public void Objective_marker_after_a_delivery_that_breaks_inside_its_name_is_never_part_of_it()
    {
        // Not seen over the game (the marker always stood before a "Deliver" line), so built by hand: the line breaks
        // inside "Thundering Express", the marker follows, then the rest. The list completes the name; no "L" in it.
        const string text = """
            DETAILS
            DROP OFF LOCATIONS (ANY ORDER)
            - Freight elevator at Thundering Express Station at Hurston's L3
            PRIMARY OBJECTIVES
            Deliver 0/4 SCU of Processed Food to Thundering
            L
            Express Station at Hurston's L3 Lagrange point.
            Collect Processed Food from Everus Harbor.
            ACCEPT OFFER
            """;

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Thundering Express Station", "Processed Food", 4));
        parsed.Pickups.Should().Equal(new ParsedPickup("Processed Food", "Everus Harbor"));
    }

    [Theory]
    [InlineData(ImportFixtures.FuelOffer, ContractView.Offer)]
    [InlineData(ImportFixtures.IronCouldntHandle, ContractView.Offer)]
    [InlineData(ImportFixtures.WasteCollectWrapped, ContractView.Offer)]
    [InlineData(ImportFixtures.StimsSplit, ContractView.Offer)]
    [InlineData(ImportFixtures.SiliconOverlayText, ContractView.Offer)]
    [InlineData(ImportFixtures.FuelAccepted, ContractView.Accepted)]
    [InlineData(ImportFixtures.SiliconAtMost, ContractView.Accepted)]
    [InlineData(ImportFixtures.StimsAcceptedTab, ContractView.Accepted)]
    [InlineData(ImportFixtures.WasteOrSmaller, ContractView.Accepted)]
    public void View_is_read_from_the_buttons_and_the_deadline_line(string fixture, ContractView expected) =>
        ContractTextParser.Parse(ImportFixtures.RawText(fixture)).View.Should().Be(expected);

    [Theory]
    [InlineData("ACCEPT 0FFER", ContractView.Offer)]
    [InlineData("Contract AvaiIability 2h 1m", ContractView.Offer)]
    [InlineData("ABAND0N", ContractView.Accepted)]
    [InlineData("Contract DeadIine N/A", ContractView.Accepted)]
    [InlineData("SHARE UNTRACK", ContractView.Accepted)]
    [InlineData("UNTRACK", ContractView.Accepted)]
    [InlineData("TRACK", ContractView.Accepted)]
    [InlineData("ACCEPT OFFER\nABANDON", ContractView.Unknown)]
    [InlineData("Contract Availability 2h 1m\nContract Deadline N/A", ContractView.Unknown)]
    [InlineData("Covalex Shipping", ContractView.Unknown)]
    [InlineData("Accept the offer by tomorrow.", ContractView.Unknown)]
    public void View_tolerates_misread_signals_and_is_unknown_without_one_side(string lines, ContractView expected)
    {
        // Offers: the ACCEPT OFFER button, "Contract Availability". Accepted: ABANDON, SHARE (UN)TRACK (ContractTracker's text
        // has "TRACK" alone), "Contract Deadline". Both sides or neither: unknown, never a guess (T10.2a).
        var parsed = ContractTextParser.Parse($"Rookie Rank - Direct Small Cargo Haul\nDETAILS\nDeliver 0/7 SCU of Stims to Riker\n{lines}");

        parsed.View.Should().Be(expected);
    }

    /// <summary>
    /// Every place a fixture's Collect lines name (H2, T10.2a): ContractTracker's text puts prose or a list right after a
    /// Collect line, and an ending wrongly taken from it would give a place that is none of these.
    /// </summary>
    private static readonly string[] KnownPickups =
    [
        "Everus Harbor", "Green Glade Station", "Melodic Fields Station", "Thundering Express Station", "Faithful Dream Station",
        "High Course Station", "Ruin Station", "Rat's Nest", "Starlight Service Station", "Dudley & Daughters", "Gaslight",
    ];

    /// <summary>Places the OCR tore beyond a prefix in ContractTracker's captures, read as they are.</summary>
    private static readonly string[] KnownFragments = ["Thundg", "Green Gle"];

    public static TheoryData<string> AllRawTextFixtures() =>
    [
        .. typeof(ImportFixtures).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(name => typeof(ImportFixtures).Assembly.GetManifestResourceNames().Contains($"rawtext/{name}.txt")),
    ];

    [Theory]
    [MemberData(nameof(AllRawTextFixtures))]
    public void Every_pickup_read_from_a_fixture_is_a_known_place_or_a_torn_start_of_one(string fixture)
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(fixture));

        parsed.Pickups.Select(p => p.Place).Should().AllSatisfy(place =>
            IsKnownPickup(place).Should().BeTrue($"'{place}' in {fixture} should be a known place or a torn start of one"));
    }

    // "HUR-L2 Faithful Dream Station" and "HuR-LS High Course St": the Lagrange prefix off, then a known place or its start.
    private static bool IsKnownPickup(string place)
    {
        var name = Regex.Replace(place, @"^HUR-L\w\s+", "", RegexOptions.IgnoreCase);
        var form = ContractText.AnchorForm(name);
        return form.Length > 0
               && (KnownPickups.Any(known => ContractText.AnchorForm(known).StartsWith(form, StringComparison.Ordinal))
                   || KnownFragments.Any(fragment => ContractText.AnchorForm(fragment) == form));
    }

    /// <summary>A contract in the reading order of CargoPlan's own OCR (D99): the reward line between prose and objectives.</summary>
    private static string InColumns(string rewardLine) =>
        $"DETAILS\nSome prose.\n{rewardLine}\nContract Availability 2h 1m\nPRIMARY OBJECTIVES\nDeliver 0/9 SCU of Waste to Everus Harbor above\nACCEPT OFFER";

    [Fact]
    public void Pick_up_list_is_not_counted_as_destinations()
    {
        var parsed = ContractTextParser.Parse(ImportFixtures.RawText(ImportFixtures.WasteBiggerThan));

        parsed.ListedDestinationCount.Should().BeNull();
    }

    [Fact]
    public void Misread_anchors_still_frame_the_text()
    {
        // Delivery-like lines outside the anchors must not count, so the anchors must be found despite OCR errors.
        var text = "Deliver 0/1 SCU of Header to Nowhere at\n"
                   + ImportFixtures.RawText(ImportFixtures.StimsOcrErrors)
                       .Replace("DETAILS", "DETAlLS", StringComparison.Ordinal)
                       .Replace("ACCEPT OFFER", "ACCEPT 0FFER", StringComparison.Ordinal)
                   + "\nDeliver 0/1 SCU of Noise to Nowhere at";

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(StimsDeliveries);
    }

    [Fact]
    public void Missing_end_anchor_reads_to_the_end()
    {
        // Captures from the "Accepted" tab have no ACCEPT OFFER button (not observed, D40).
        var text = ImportFixtures.RawText(ImportFixtures.StimsOcrErrors).Replace("ACCEPT OFFER", "", StringComparison.Ordinal);

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(StimsDeliveries);
    }

    [Fact]
    public void Missing_start_anchor_reads_from_the_start_and_list_fragments_still_give_no_commodity()
    {
        var text = ImportFixtures.RawText(ImportFixtures.WasteListFragments).Replace("DETAILS", "", StringComparison.Ordinal);

        var parsed = ContractTextParser.Parse(text);

        parsed.Deliveries.Should().Equal(new ParsedDelivery("Everus Harbor", "Waste", 9));
    }

    [Fact]
    public void Line_endings_do_not_matter()
    {
        var text = ImportFixtures.RawText(ImportFixtures.StimsSplit).ReplaceLineEndings();

        ContractTextParser.Parse(text.ReplaceLineEndings("\r\n")).Should().BeEquivalentTo(ContractTextParser.Parse(text.ReplaceLineEndings("\n")));
    }

    [Fact]
    public void Text_without_delivery_lines_gives_no_deliveries()
    {
        var parsed = ContractTextParser.Parse("Covalex Independent Contractor\nDETAILS\nPRIMARY OBJECTIVES\nACCEPT OFFER");

        parsed.Deliveries.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Everus Harbor above Hurston", "Everus Harbor")]
    [InlineData("Melodic Fields Station at Hurston's L4 Lagrange point. A new haul just popped up", "Melodic Fields Station")]
    [InlineData("Faithful Dream Station at", "Faithful Dream Station")]
    [InlineData("  High   Course Station  ", "High Course Station")]
    [InlineData("HUR-LS Faithful Dream Station.", "HUR-L5 Faithful Dream Station")]
    [InlineData("Hurston's LS Lagrange point", "Hurston's L5 Lagrange point")]
    [InlineData("Lorville", "Lorville")]
    [InlineData("Everus Harbor ab", "Everus Harbor")]
    [InlineData("Everus Harbor abo", "Everus Harbor")]
    [InlineData("Everus Harbor abov", "Everus Harbor")]
    [InlineData("Everus Harbor aboy", "Everus Harbor")]
    [InlineData("Everus Harbor abeve", "Everus Harbor")]
    [InlineData("Port Olisar St", "Port Olisar St")]
    [InlineData("Everus Harbor a", "Everus Harbor a")]
    [InlineData("Everus Harbor ax", "Everus Harbor ax")]
    [InlineData("Everus Harbor Station", "Everus Harbor Station")]
    public void Destination_is_normalised(string text, string expected) =>
        ContractText.NormalizeDestination(text).Should().Be(expected);

    [Theory]
    [InlineData("Everus Harbor abov", true)]
    [InlineData("Everus Harbor aboy", true)]
    [InlineData("Everus Harbor a", false)]
    [InlineData("Thunde", false)]
    public void Torn_above_counts_as_the_end_of_the_name(string text, bool expected) =>
        ContractText.HasNameEnd(text).Should().Be(expected);

    [Theory]
    [InlineData(" Pressurized   Ice ", "Pressurized Ice")]
    [InlineData("Stims.", "Stims")]
    public void Commodity_is_normalised(string text, string expected) =>
        ContractText.NormalizeCommodity(text).Should().Be(expected);
}
