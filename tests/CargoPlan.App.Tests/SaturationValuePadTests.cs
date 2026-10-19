using System.Windows;

namespace CargoPlan.App.Tests;

public class SaturationValuePadTests
{
    private static readonly Size Pad = new(200, 100);

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(200, 0, 1, 1)]
    [InlineData(0, 100, 0, 0)]
    [InlineData(200, 100, 1, 0)]
    [InlineData(100, 50, 0.5, 0.5)]
    public void Saturation_grows_to_the_right_brightness_to_the_top(double x, double y, double saturation, double brightness)
    {
        var (s, b) = SaturationValuePad.At(new Point(x, y), Pad);

        s.Should().Be(saturation);
        b.Should().Be(brightness);
    }

    [Theory]
    [InlineData(-50, -50, 0, 1)]
    [InlineData(300, 300, 1, 0)]
    public void A_drag_beyond_the_pad_stops_at_its_edge(double x, double y, double saturation, double brightness)
    {
        var (s, b) = SaturationValuePad.At(new Point(x, y), Pad);

        s.Should().Be(saturation);
        b.Should().Be(brightness);
    }

    [Fact]
    public void Position_is_the_inverse()
    {
        SaturationValuePad.PositionOf(0.25, 0.75, Pad).Should().Be(new Point(50, 25));
    }
}
