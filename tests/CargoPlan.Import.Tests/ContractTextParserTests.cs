using CargoPlan.Core.Models;
using CargoPlan.Import.ContractTracker;

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
