using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

/// <summary>
/// What gives way in the header when room runs short (B15, D110): the status first, then the capacity's long form, the ship
/// name last and only to an ellipsis. Widths in DIP with their margins; each limit is tested at the sum and one below.
/// </summary>
public class HeaderLayoutTests
{
    /// <summary>Name 80, status 100, capacity 140, compact capacity 40: 320 for all of it.</summary>
    private static readonly HeaderWidths Widths = new(Name: 80, Status: 100, Capacity: 140, CompactCapacity: 40);

    [Theory]
    [InlineData(1000, HeaderStage.Full)]
    [InlineData(320, HeaderStage.Full)]
    [InlineData(319, HeaderStage.NoStatus)]
    [InlineData(220, HeaderStage.NoStatus)]
    [InlineData(219, HeaderStage.CompactCapacity)]
    [InlineData(120, HeaderStage.CompactCapacity)]
    [InlineData(119, HeaderStage.TrimmedName)]
    [InlineData(0, HeaderStage.TrimmedName)]
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

        HeaderLayout.Stage(widths, 220).Should().Be(HeaderStage.Full);
        HeaderLayout.Stage(widths, 219).Should().Be(HeaderStage.CompactCapacity);
    }

    [Theory]
    [InlineData(1000, 80)]
    [InlineData(120, 80)]
    [InlineData(119, 79)]
    [InlineData(40, 0)]
    [InlineData(0, 0)]
    public void The_name_keeps_its_width_until_the_last_stage_then_takes_what_the_compact_capacity_leaves(double available, double expected)
    {
        HeaderLayout.NameWidth(Widths, available).Should().Be(expected);
    }
}
