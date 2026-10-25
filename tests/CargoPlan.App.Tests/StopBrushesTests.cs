using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class StopBrushesTests
{
    private static readonly StopPalette Palette = StopPalettes.OkabeIto;

    [Fact]
    public void The_first_round_is_the_plain_colour_with_the_alpha()
    {
        var brush = StopBrushes.Fill(Palette, 3, 0x80).Should().BeOfType<SolidColorBrush>().Subject;

        brush.Color.Should().Be(Palette.Colors[3] with { A = 0x80 });
        brush.IsFrozen.Should().BeTrue();
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void Later_rounds_are_a_frozen_tile_fixed_to_the_screen(int ordinal)
    {
        // Absolute units: the polygons are in screen pixels already (D32), so the hatch keeps its size at every zoom and runs
        // on across faces.
        var brush = StopBrushes.Fill(Palette, ordinal, 0xD0).Should().BeOfType<DrawingBrush>().Subject;

        brush.IsFrozen.Should().BeTrue();
        brush.TileMode.Should().Be(TileMode.Tile);
        brush.ViewportUnits.Should().Be(BrushMappingMode.Absolute);
        brush.Viewport.Should().Be(new Rect(0, 0, StopBrushes.TileSize, StopBrushes.TileSize));
    }

    [Fact]
    public void Forward_and_backward_hatch_differ()
    {
        var forward = StopBrushes.Stripes(StopHatch.Forward).ToString(CultureInfo.InvariantCulture);
        var backward = StopBrushes.Stripes(StopHatch.Backward).ToString(CultureInfo.InvariantCulture);

        forward.Should().NotBe(backward);
        StopBrushes.Fill(Palette, 8, 0xD0).Should().NotBeSameAs(StopBrushes.Fill(Palette, 16, 0xD0));
    }

    [Fact]
    public void One_ordinal_gives_one_brush_instance_so_rendering_never_builds_one()
    {
        StopBrushes.Fill(Palette, 9, 0xD0).Should().BeSameAs(StopBrushes.Fill(Palette, 9, 0xD0));
        StopBrushes.Fill(Palette, 1, 0xD0).Should().BeSameAs(StopBrushes.Fill(Palette, 1, 0xD0));
    }

    [Fact]
    public void The_swatch_is_the_crate_brush_at_the_legend_alpha()
    {
        // The legend shows the same pattern as the view (P8); its alpha is the legend's own (D31: swatches keep theirs).
        StopBrushes.Swatch(Palette, 9).Should().BeSameAs(StopBrushes.Fill(Palette, 9, MissionPalette.SwatchAlpha));
    }

    [Theory]
    [InlineData(0xF0, 0xE4, 0x42, 0x00)] // yellow: black stripes
    [InlineData(0xFF, 0xFF, 0xFF, 0x00)] // white
    [InlineData(0x00, 0x72, 0xB2, 0xFF)] // blue: white stripes
    [InlineData(0x11, 0x77, 0x33, 0xFF)] // Tol's green
    public void Stripes_take_black_or_white_whichever_contrasts_more(byte r, byte g, byte b, byte stripe)
    {
        StopBrushes.StripeColour(Color.FromRgb(r, g, b)).Should().Be(Color.FromRgb(stripe, stripe, stripe));
    }
}
