namespace CargoPlan.Ocr.Tests;

public sealed class ScanRegionTests
{
    // The two regions measured in S10.0 (TASKS.md, finding): the mobiGlas panel sits at the same place relative to the
    // centred 16:9 area at 21:9 and 16:9, in both views.
    [Fact]
    public void The_ultrawide_region_is_the_one_measured_in_the_spike() =>
        ScanRegion.Of(3440, 1440).Should().Be(new PixelRect(1300, 210, 1560, 1000));

    [Fact]
    public void The_full_hd_region_is_the_one_measured_in_the_spike() =>
        ScanRegion.Of(1920, 1080).Should().Be(new PixelRect(645, 157, 1170, 750));

    [Fact]
    public void A_16_9_window_scales_the_region_with_its_size() =>
        ScanRegion.Of(2560, 1440).Should().Be(new PixelRect(860, 210, 1560, 1000));

    // Known issue T10.5: never checked in the game. The formula says the 16:9 area is limited by the width and centred
    // vertically; whether the game lays the mobiGlas out that way is open.
    [Fact]
    public void Known_issue_16_10_limits_the_area_by_the_width() =>
        ScanRegion.Of(1920, 1200).Should().Be(new PixelRect(645, 217, 1170, 750));

    // Known issue T10.5, as above: 32:9 is limited by the height and centred horizontally.
    [Fact]
    public void Known_issue_32_9_limits_the_area_by_the_height() =>
        ScanRegion.Of(5120, 1440).Should().Be(new PixelRect(2140, 210, 1560, 1000));

    [Fact]
    public void The_region_lies_inside_the_window()
    {
        var region = ScanRegion.Of(1366, 768);

        region.X.Should().BeGreaterThanOrEqualTo(0);
        region.Y.Should().BeGreaterThanOrEqualTo(0);
        (region.X + region.Width).Should().BeLessThanOrEqualTo(1366);
        (region.Y + region.Height).Should().BeLessThanOrEqualTo(768);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    public void An_empty_window_has_no_region(int width, int height) =>
        FluentActions.Invoking(() => ScanRegion.Of(width, height)).Should().Throw<ArgumentOutOfRangeException>();
}
