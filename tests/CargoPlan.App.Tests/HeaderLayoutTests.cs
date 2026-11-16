using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

/// <summary>
/// What gives way in the header when room runs short (B15, D110): the status first, then the capacity's long form, then
/// the buttons go to a second line; the ship name only trims when even it and the short capacity are wider than the row.
/// Widths in DIP with their margins; each limit is tested at the sum and one below.
/// </summary>
public class HeaderLayoutTests
{
    /// <summary>Name 80, status 100, capacity 140, compact capacity 40, buttons 200: 520 for all of it.</summary>
    private static readonly HeaderWidths Widths = new(Name: 80, Status: 100, Capacity: 140, CompactCapacity: 40, Buttons: 200);

    [Theory]
    [InlineData(1000, HeaderStage.Full)]
    [InlineData(520, HeaderStage.Full)]
    [InlineData(519, HeaderStage.NoStatus)]
    [InlineData(420, HeaderStage.NoStatus)]
    [InlineData(419, HeaderStage.CompactCapacity)]
    [InlineData(320, HeaderStage.CompactCapacity)]
    [InlineData(319, HeaderStage.ButtonsWrapped)]
    [InlineData(0, HeaderStage.ButtonsWrapped)]
    public void Each_stage_holds_up_to_its_limit(double available, HeaderStage expected)
    {
        HeaderLayout.Stage(Widths, available).Should().Be(expected);
    }

    [Fact]
    public void Without_a_limit_everything_stands()
    {
        // E1: measured without a constraint (SizeToContent, a test without a size), the header is never short of room.
        HeaderLayout.Stage(Widths, double.PositiveInfinity).Should().Be(HeaderStage.Full);
    }

    [Fact]
    public void Without_a_status_the_first_stage_changes_nothing()
    {
        // The status text is there only once every stop is delivered; until then its width is 0.
        var widths = Widths with { Status = 0 };

        HeaderLayout.Stage(widths, 420).Should().Be(HeaderStage.Full);
        HeaderLayout.Stage(widths, 419).Should().Be(HeaderStage.CompactCapacity);
    }

    [Fact]
    public void Locked_without_buttons_the_stages_count_the_text_alone()
    {
        // Locked, the buttons collapse and measure 0 (D110): nothing wraps, the room is the text's.
        var widths = Widths with { Buttons = 0 };

        HeaderLayout.Stage(widths, 320).Should().Be(HeaderStage.Full);
        HeaderLayout.Stage(widths, 120).Should().Be(HeaderStage.CompactCapacity);
        HeaderLayout.Stage(widths, 119).Should().Be(HeaderStage.ButtonsWrapped);
    }

    [Theory]
    [InlineData(1000, 80)]
    [InlineData(319, 80)]
    [InlineData(120, 80)]
    [InlineData(119, 79)]
    [InlineData(40, 0)]
    [InlineData(0, 0)]
    public void The_name_trims_only_when_it_and_the_compact_capacity_are_wider_than_the_row(double available, double expected)
    {
        // With the buttons on the second line, the first has the whole row for name and short capacity.
        HeaderLayout.NameWidth(Widths, available).Should().Be(expected);
    }
}
