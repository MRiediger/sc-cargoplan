using System.Globalization;
using System.Windows;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class PaletteNoticeConverterTests
{
    private static readonly PaletteNoticeConverter Converter = new();

    [Fact]
    public void More_stops_than_colours_give_the_palettes_notice()
    {
        Converter.Convert([7, StopPalettes.TolBright], typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(StopPalettes.TolBright.RepeatNotice(7));
    }

    [Fact]
    public void No_repeat_gives_an_empty_text_the_style_collapses()
    {
        Converter.Convert([8, StopPalettes.OkabeIto], typeof(string), null, CultureInfo.InvariantCulture).Should().Be("");
        Converter.Convert([DependencyProperty.UnsetValue, StopPalettes.OkabeIto], typeof(string), null, CultureInfo.InvariantCulture).Should().Be("");
    }
}
