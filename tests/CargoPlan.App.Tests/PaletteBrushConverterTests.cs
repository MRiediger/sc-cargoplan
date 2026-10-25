using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class PaletteBrushConverterTests
{
    private static readonly PaletteBrushConverter Converter = new();

    [Fact]
    public void A_stop_ordinal_and_the_palette_become_the_swatch_the_legend_shows()
    {
        Converter.Convert([9, StopPalettes.TolBright], typeof(Brush), null, CultureInfo.InvariantCulture)
            .Should().BeSameAs(StopBrushes.Swatch(StopPalettes.TolBright, 9));
    }

    [Fact]
    public void Anything_else_leaves_the_target_unset()
    {
        Converter.Convert(["2", StopPalettes.OkabeIto], typeof(Brush), null, CultureInfo.InvariantCulture).Should().Be(DependencyProperty.UnsetValue);
        Converter.Convert([2, DependencyProperty.UnsetValue], typeof(Brush), null, CultureInfo.InvariantCulture).Should().Be(DependencyProperty.UnsetValue);
    }
}
