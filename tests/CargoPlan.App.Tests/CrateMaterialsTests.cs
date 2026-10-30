using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

/// <summary>
/// The crates' fills in the 3D view (T8.2): one material per stop, state and hatch tile, never one per crate; the same
/// colour and hatch as the legend's swatch (K4.2), with the crates' own alpha.
/// </summary>
public class CrateMaterialsTests
{
    private const byte Alpha = 0xD0;
    private static readonly StopPalette Palette = StopPalettes.OkabeIto;

    private static Brush BrushOf(Material material) => ((DiffuseMaterial)material).Brush;

    [Fact]
    public void Two_crates_of_one_stop_share_one_material()
    {
        CrateMaterials.Fill(Palette, 3, Alpha, dimmed: false, tileCells: 1)
            .Should().BeSameAs(CrateMaterials.Fill(Palette, 3, Alpha, dimmed: false, tileCells: 1));
    }

    [Fact]
    public void Stops_a_palette_apart_in_the_same_round_share_a_material_like_their_brush()
    {
        // Colours and hatches repeat after Colors × Rounds (K4.2).
        var period = Palette.Colors.Count * StopPalette.Rounds;

        CrateMaterials.Fill(Palette, 2, Alpha, false, 1).Should().BeSameAs(CrateMaterials.Fill(Palette, 2 + period, Alpha, false, 1));
    }

    [Fact]
    public void The_first_round_is_the_stop_colour_at_the_crates_alpha()
    {
        var brush = (SolidColorBrush)BrushOf(CrateMaterials.Fill(Palette, 3, Alpha, false, 1));

        brush.Color.Should().Be(Palette.ColorOf(3) with { A = Alpha });
        brush.IsFrozen.Should().BeTrue();
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void Later_rounds_carry_the_legends_hatch_on_a_tile_in_cells(int ordinal)
    {
        var brush = (DrawingBrush)BrushOf(CrateMaterials.Fill(Palette, ordinal, Alpha, false, tileCells: 0.5));
        var swatch = (DrawingBrush)StopBrushes.Fill(Palette, ordinal, Alpha);

        brush.Drawing.Should().BeSameAs(swatch.Drawing, "legend and view share one tile");
        brush.Viewport.Should().Be(new System.Windows.Rect(0, 0, 0.5, 0.5));
        brush.ViewportUnits.Should().Be(BrushMappingMode.Absolute);
        brush.TileMode.Should().Be(TileMode.Tile);
    }

    [Fact]
    public void A_dimmed_crate_keeps_its_colour_at_a_fraction_of_the_alpha()
    {
        var brush = (SolidColorBrush)BrushOf(CrateMaterials.Fill(Palette, 3, Alpha, dimmed: true, tileCells: 1));

        brush.Color.Should().Be(Palette.ColorOf(3) with { A = (byte)Math.Round(Alpha * CrateMaterials.DimmedShare) });
    }

    [Fact]
    public void A_delivered_crate_is_grey_at_the_crates_alpha()
    {
        var brush = (SolidColorBrush)BrushOf(CrateMaterials.Delivered(Alpha, dimmed: false));

        brush.Color.Should().Be(MissionPalette.Delivered with { A = Alpha });
        CrateMaterials.Delivered(Alpha, false).Should().BeSameAs(CrateMaterials.Delivered(Alpha, false));
    }

    [Theory]
    [InlineData(6, 1)]
    [InlineData(12, 0.5)]
    [InlineData(3, 2)]
    [InlineData(8, 1)]
    [InlineData(100, 0.0625)]
    public void The_hatch_tile_keeps_about_its_screen_size_in_powers_of_two(double pxPerCell, double tileCells)
    {
        CrateMaterials.TileCells(pxPerCell).Should().Be(tileCells);
    }
}
