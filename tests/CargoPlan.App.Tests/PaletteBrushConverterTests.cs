using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class PaletteBrushConverterTests
{
    private static readonly PaletteBrushConverter Converter = new();

    [Fact]
    public void A_palette_index_becomes_the_brush_the_ship_view_paints_with()
    {
        Converter.Convert(2, typeof(Brush), null, CultureInfo.InvariantCulture).Should().BeSameAs(MissionPalette.Brush(2));
    }

    [Fact]
    public void Anything_else_leaves_the_target_unset()
    {
        Converter.Convert("2", typeof(Brush), null, CultureInfo.InvariantCulture).Should().Be(DependencyProperty.UnsetValue);
    }
}
