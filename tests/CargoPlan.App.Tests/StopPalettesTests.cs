using System.Windows.Media;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class StopPalettesTests
{
    /// <summary>
    /// The closest any stop colour came to the problem red before palettes (Okabe-Ito vermillion, ΔE76 33.6, K4.2): no
    /// palette may come closer to the problem red or to the delivered grey (Martin at the approval, P4b).
    /// </summary>
    private const double MinDistance = 33.6;

    private static string[] Hex(StopPalette palette) => [.. palette.Colors.Select(c => $"{c.R:X2}{c.G:X2}{c.B:X2}")];

    [Fact]
    public void Okabe_ito_is_the_palette_of_before()
    {
        // Okabe & Ito 2008 without black, white as eighth (D15): the default looks as before palettes.
        Hex(StopPalettes.OkabeIto).Should().Equal("E69F00", "56B4E9", "009E73", "F0E442", "0072B2", "D55E00", "CC79A7", "FFFFFF");
    }

    [Fact]
    public void Tol_bright_is_the_source_without_its_grey()
    {
        // Paul Tol, "Colour schemes", bright; grey BBBBBB out: grey means delivered in the overlay (K1 at the approval).
        Hex(StopPalettes.TolBright).Should().Equal("4477AA", "EE6677", "228833", "CCBB44", "66CCEE", "AA3377");
    }

    [Fact]
    public void Tol_muted_is_the_source_without_indigo_and_wine()
    {
        // Paul Tol, "Colour schemes", muted; 332288 and 882255 out: invisible on the dark overlay, like Okabe's black (D15).
        Hex(StopPalettes.TolMuted).Should().Equal("CC6677", "DDCC77", "117733", "88CCEE", "44AA99", "999933", "AA4499");
    }

    [Fact]
    public void Every_palette_has_a_name_of_the_file_and_unique_opaque_colours()
    {
        StopPalettes.All.Select(p => StopPaletteNames.ToName(p.Name)).Should().Equal(StopPaletteNames.Names);
        StopPalettes.All.Should().OnlyContain(p => p.Colors.Distinct().Count() == p.Colors.Count && p.Colors.All(c => c.A == 0xFF));
    }

    [Fact]
    public void No_stop_colour_comes_close_to_the_problem_red_or_the_delivered_grey()
    {
        // Nearness, not equality: also catches the next palette someone adds (P4b). Selection and drop target are edges on
        // top, kept apart by their dark under-stroke instead (P6).
        foreach (var palette in StopPalettes.All)
        {
            foreach (var colour in palette.Colors)
            {
                DeltaE76(colour, MissionPalette.Violation).Should().BeGreaterThanOrEqualTo(MinDistance, $"{palette.Name} {colour} against the problem red");
                DeltaE76(colour, MissionPalette.Delivered).Should().BeGreaterThanOrEqualTo(MinDistance, $"{palette.Name} {colour} against delivered");
            }
        }
    }

    [Theory]
    [InlineData(StopPaletteName.OkabeIto, "Okabe-Ito · 8 colors")]
    [InlineData(StopPaletteName.TolBright, "Tol Bright · 6 colors")]
    [InlineData(StopPaletteName.TolMuted, "Tol Muted · 7 colors")]
    public void A_palette_names_itself_with_its_count(StopPaletteName name, string text)
    {
        StopPalettes.For(name).Text.Should().Be(text);
    }

    [Theory]
    [InlineData(0, 0, StopHatch.None)]
    [InlineData(7, 7, StopHatch.None)]
    [InlineData(8, 0, StopHatch.Forward)]
    [InlineData(15, 7, StopHatch.Forward)]
    [InlineData(16, 0, StopHatch.Backward)]
    [InlineData(24, 0, StopHatch.None)]
    public void The_ordinal_gives_colour_and_hatch(int ordinal, int colour, StopHatch hatch)
    {
        var palette = StopPalettes.OkabeIto;

        palette.ColorOf(ordinal).Should().Be(palette.Colors[colour]);
        palette.HatchOf(ordinal).Should().Be(hatch);
    }

    [Fact]
    public void Another_palette_keeps_the_ordinal_and_changes_only_its_colour()
    {
        // The stop keeps its number (D64's order); with fewer colours it is hatched sooner.
        StopPalettes.TolBright.ColorOf(6).Should().Be(StopPalettes.TolBright.Colors[0]);
        StopPalettes.TolBright.HatchOf(6).Should().Be(StopHatch.Forward);
        StopPalettes.OkabeIto.HatchOf(6).Should().Be(StopHatch.None);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(8, null)]
    [InlineData(9, "More than 8 stops: colors repeat, hatched from stop 9.")]
    [InlineData(24, "More than 8 stops: colors repeat, hatched from stop 9.")]
    [InlineData(25, "More than 24 stops: colors and hatching repeat.")]
    public void The_notice_says_when_colours_repeat(int stops, string? notice)
    {
        StopPalettes.OkabeIto.RepeatNotice(stops).Should().Be(notice);
    }

    [Fact]
    public void A_name_that_is_not_one_is_refused()
    {
        StopPaletteNames.TryParse("okabeito", out _).Should().BeFalse();
        StopPaletteNames.TryParse("ibm", out _).Should().BeFalse();
        StopPaletteNames.TryParse("tolMuted", out var name).Should().BeTrue();
        name.Should().Be(StopPaletteName.TolMuted);
        StopPaletteNames.DefaultName.Should().Be("okabeIto");
    }

    /// <summary>CIE76: the distance in CIELAB (D65 white), from sRGB as WCAG linearises it.</summary>
    private static double DeltaE76(Color a, Color b)
    {
        var (l1, a1, b1) = Lab(a);
        var (l2, a2, b2) = Lab(b);
        return Math.Sqrt(Math.Pow(l1 - l2, 2) + Math.Pow(a1 - a2, 2) + Math.Pow(b1 - b2, 2));
    }

    private static (double L, double A, double B) Lab(Color c)
    {
        static double Linear(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;

        var (r, g, b) = (Linear(c.R), Linear(c.G), Linear(c.B));
        var x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047;
        var y = r * 0.2126 + g * 0.7152 + b * 0.0722;
        var z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;
        return (116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)));
    }
}
