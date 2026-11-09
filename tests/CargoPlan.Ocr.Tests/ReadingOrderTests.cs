namespace CargoPlan.Ocr.Tests;

public sealed class ReadingOrderTests
{
    private const float LineHeight = 20;

    // The mobiGlas layout in miniature: a title left of everything, the prose under DETAILS on the left, the objectives
    // on the right. Sorted by top edge the two columns interleave (S10.0 G2: 90.5 % → 100 % with columns).
    [Fact]
    public void Columns_are_read_one_after_the_other()
    {
        TextBox[] blocks =
        [
            Box("PRIMARY OBJECTIVES", 600, 100),
            Box("DETAILS", 100, 100),
            Box("Deliver 0/9 SCU of Silicon to Riker Memorial Spaceport", 600, 140),
            Box("Covalex needs a hauler.", 100, 140),
            Box("Covalex Shipping", 20, 40),
            Box("Collect from Everus Harbor.", 100, 180),
        ];

        ReadingOrder.Lines(blocks).Should().Equal(
            "Covalex Shipping",
            "DETAILS",
            "Covalex needs a hauler.",
            "Collect from Everus Harbor.",
            "PRIMARY OBJECTIVES",
            "Deliver 0/9 SCU of Silicon to Riker Memorial Spaceport");
    }

    // K2: the parser's DeliveryLine is anchored to one line; a long destination often comes as a second box.
    [Fact]
    public void A_delivery_in_two_boxes_is_one_line()
    {
        TextBox[] blocks =
        [
            Box("PRIMARY OBJECTIVES", 600, 100),
            Box("to Thundering Express Station", 860, 141),
            Box("Deliver 0/4 SCU of Stims", 600, 140),
        ];

        ReadingOrder.Lines(blocks).Should().Equal(
            "PRIMARY OBJECTIVES",
            "Deliver 0/4 SCU of Stims to Thundering Express Station");
    }

    // Each objective has a marker in front that the OCR reads as "0" or "L"; joined, "0 Deliver …" no longer starts its
    // line and the parser loses the delivery (hand run over the S10.0 captures: one delivery in 4 of 20 contracts per
    // set). A first block of at most two characters stays its own line, as in ContractTracker's raw text.
    [Theory]
    [InlineData("0")]
    [InlineData("L")]
    public void An_objective_marker_stays_its_own_line(string marker)
    {
        TextBox[] blocks =
        [
            Box("PRIMARY OBJECTIVES", 600, 100),
            Box("Deliver O/101 SCU of Iron (Ore) to Faithful Dream Station", 625, 140),
            Box(marker, 600, 142),
        ];

        ReadingOrder.Lines(blocks).Should().Equal(
            "PRIMARY OBJECTIVES",
            marker,
            "Deliver O/101 SCU of Iron (Ore) to Faithful Dream Station");
    }

    // The shape decides, not only the length: the marker's box is about square (measured 1.02–1.20 wide per high in the
    // S10.0 captures), a short word in front of the rest of its row is wider.
    [Fact]
    public void A_square_short_first_block_is_its_own_line()
    {
        TextBox[] blocks =
        [
            Box("PRIMARY OBJECTIVES", 600, 100),
            new("0", 600, 140, 620, 160),
            Box("Deliver 0/101 SCU of Iron to X", 630, 140),
        ];

        ReadingOrder.Lines(blocks).Should().Equal("PRIMARY OBJECTIVES", "0", "Deliver 0/101 SCU of Iron to X");
    }

    [Fact]
    public void A_wide_short_first_block_joins_its_row()
    {
        TextBox[] blocks =
        [
            Box("PRIMARY OBJECTIVES", 600, 100),
            new("to", 600, 140, 640, 160),
            Box("Thundering Express Station", 648, 140),
        ];

        ReadingOrder.Lines(blocks).Should().Equal("PRIMARY OBJECTIVES", "to Thundering Express Station");
    }

    // The same rule splits a size from its unit; the parser reads "4\nSCU or smaller" (\s* across the break).
    [Fact]
    public void A_size_before_its_unit_is_a_short_first_block_too()
    {
        TextBox[] blocks =
        [
            Box("DETAILS", 100, 100),
            Box("SCU or smaller", 130, 140),
            Box("4", 100, 141),
            Box("Reward", 100, 180),
        ];

        ReadingOrder.Lines(blocks).Should().Equal("DETAILS", "4", "SCU or smaller", "Reward");
    }

    [Fact]
    public void A_marker_alone_on_its_row_is_just_a_line()
    {
        TextBox[] blocks = [Box("PRIMARY OBJECTIVES", 600, 100), Box("0", 600, 140)];

        ReadingOrder.Lines(blocks).Should().Equal("PRIMARY OBJECTIVES", "0");
    }

    // OCR boxes jitter by a few pixels: the left edge of a line under a heading and the top of boxes in one row.
    [Fact]
    public void A_few_pixels_of_jitter_change_nothing()
    {
        TextBox[] blocks =
        [
            Box("DETAILS", 100, 100),
            Box("PRIMARY OBJECTIVES", 600, 100),
            Box("first line", 97, 143),
            Box("second line", 103, 177),
            Box("Deliver 0/2 SCU of Waste", 597, 137),
            Box("to Baijini Point", 850, 140),
        ];

        ReadingOrder.Lines(blocks).Should().Equal(
            "DETAILS",
            "first line",
            "second line",
            "PRIMARY OBJECTIVES",
            "Deliver 0/2 SCU of Waste to Baijini Point");
    }

    public static TheoryData<string, string[]> OneHeading => new()
    {
        // ACCEPTED shows no DETAILS (D40): PRIMARY OBJECTIVES is the only border, the prose is column 0.
        {
            "DETAILS",
            ["Covalex needs a hauler.", "Collect from Everus Harbor.", "PRIMARY OBJECTIVES", "Deliver 0/9 SCU of Silicon to Riker"]
        },
        // Without PRIMARY OBJECTIVES (misread) everything right of DETAILS is one column, and a row joins across both
        // sides; the delivery then no longer starts its line. Recorded, not hidden: the parser will miss it.
        {
            "PRIMARY OBJECTIVES",
            ["DETAILS", "Covalex needs a hauler. Deliver 0/9 SCU of Silicon to Riker", "Collect from Everus Harbor."]
        },
    };

    [Theory]
    [MemberData(nameof(OneHeading))]
    public void One_heading_is_enough_for_a_border(string missing, string[] expected)
    {
        TextBox[] all =
        [
            Box("DETAILS", 100, 100),
            Box("PRIMARY OBJECTIVES", 600, 100),
            Box("Covalex needs a hauler.", 100, 140),
            Box("Deliver 0/9 SCU of Silicon to Riker", 600, 140),
            Box("Collect from Everus Harbor.", 100, 180),
        ];

        ReadingOrder.Lines([.. all.Where(b => b.Text != missing)]).Should().Equal(expected);
    }

    [Fact]
    public void Headings_are_found_in_any_case()
    {
        TextBox[] blocks = [Box("Primary Objectives", 600, 100), Box("left", 100, 140), Box("right", 600, 140)];

        ReadingOrder.Lines(blocks).Should().Equal("left", "Primary Objectives", "right");
    }

    [Fact]
    public void Without_a_heading_there_is_no_contract()
    {
        TextBox[] blocks = [Box("Some HUD text", 100, 100), Box("more", 300, 100)];

        ReadingOrder.Lines(blocks).Should().BeEmpty();
    }

    [Fact]
    public void Nothing_read_is_no_lines() => ReadingOrder.Lines([]).Should().BeEmpty();

    // Width doesn't matter to the order; 12 px per character keeps the boxes realistic.
    private static TextBox Box(string text, float left, float top) =>
        new(text, left, top, left + 12 * text.Length, top + LineHeight);
}
