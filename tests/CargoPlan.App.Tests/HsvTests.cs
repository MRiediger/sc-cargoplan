using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public class HsvTests
{
    [Theory]
    [InlineData("#FFFF0000", 0, 1, 1)]
    [InlineData("#FF00FF00", 120, 1, 1)]
    [InlineData("#FF0000FF", 240, 1, 1)]
    [InlineData("#FFFFFF00", 60, 1, 1)]
    [InlineData("#FFFF00FF", 300, 1, 1)]
    [InlineData("#FFFFFFFF", 0, 0, 1)]
    [InlineData("#FF000000", 0, 0, 0)]
    [InlineData("#FF808080", 0, 0, 128 / 255.0)]
    [InlineData("#FF800000", 0, 1, 128 / 255.0)]
    public void FromRgb_gives_hue_saturation_and_value(string colour, double hue, double saturation, double value)
    {
        var hsv = Hsv.FromRgb(Argb.Parse(colour));

        hsv.Hue.Should().BeApproximately(hue, 1e-9);
        hsv.Saturation.Should().BeApproximately(saturation, 1e-9);
        hsv.Value.Should().BeApproximately(value, 1e-9);
    }

    [Theory]
    [InlineData(0, 1, 1, "#FFFF0000")]
    [InlineData(120, 1, 1, "#FF00FF00")]
    [InlineData(240, 1, 1, "#FF0000FF")]
    [InlineData(360, 1, 1, "#FFFF0000")]
    [InlineData(30, 1, 1, "#FFFF8000")]
    [InlineData(200, 0, 1, "#FFFFFFFF")]
    [InlineData(200, 1, 0, "#FF000000")]
    public void ToArgb_gives_the_colour(double hue, double saturation, double value, string expected)
    {
        new Hsv(hue, saturation, value).ToArgb(byte.MaxValue).ToString().Should().Be(expected);
    }

    [Fact]
    public void ToArgb_takes_the_alpha_as_given()
    {
        new Hsv(0, 1, 1).ToArgb(0x40).Should().Be(new Argb(0x40, 0xFF, 0, 0));
    }

    [Fact]
    public void Every_colour_survives_the_round_trip()
    {
        for (var r = 0; r <= 255; r += 15)
        {
            for (var g = 0; g <= 255; g += 15)
            {
                for (var b = 0; b <= 255; b += 15)
                {
                    Argb colour = new(byte.MaxValue, (byte)r, (byte)g, (byte)b);

                    Hsv.FromRgb(colour).ToArgb(byte.MaxValue).Should().Be(colour);
                }
            }
        }
    }
}
