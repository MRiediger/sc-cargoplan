using System.Globalization;
using System.Windows.Media;
using System.Xml.Linq;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Routing;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// The map's route colour (D128 addendum, Martin's picture gate): apart from every rank colour of every stop palette, an
/// edge's colour being its highest rank's (<c>StopPalette.ColorOf((int)rank)</c>), by at least
/// <see cref="MinDeltaE"/> in CIELAB, for normal vision and for deuteranopia and protanopia. The notice amber the route
/// had was ΔE 9 from Okabe-Ito's orange. The threshold is fixed: on a violation the route colour changes, never it.
/// The colour is read from App.xaml, its one source.
/// </summary>
public sealed class RouteToneTests(ITestOutputHelper output)
{
    /// <summary>Clearly two colours side by side on the map's thin lines; Martin's threshold.</summary>
    private const double MinDeltaE = 20;

    public enum Vision
    {
        Normal,
        Deuteranopia,
        Protanopia,
    }

    [Theory]
    [InlineData(Vision.Normal)]
    [InlineData(Vision.Deuteranopia)]
    [InlineData(Vision.Protanopia)]
    public void The_route_colour_is_apart_from_every_rank_colour_of_every_palette(Vision vision)
    {
        var route = AppBrush("MapRouteBrush");

        var (nearest, distance) = Nearest(route, vision);

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{route} {vision}: ΔE {distance:0.0} to {nearest}"));
        distance.Should().BeGreaterThanOrEqualTo(MinDeltaE, $"{nearest} is a rank colour; change the route colour, not the threshold");
    }

    [Fact]
    public void The_notice_amber_the_route_had_is_not()
    {
        // The counter-check in the test itself: the gate finding's collision, Okabe-Ito's orange (Trainee).
        var amber = AppBrush("NoticeForegroundBrush");

        var (nearest, distance) = Nearest(amber, Vision.Normal);

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{amber}: ΔE {distance:0.0} to {nearest}"));
        (nearest, distance < MinDeltaE).Should().Be((Color.FromRgb(0xE6, 0x9F, 0x00), true));
    }

    [Fact]
    public void Delta_E_of_white_and_black_is_100_and_a_dichromat_sees_red_and_green_alike()
    {
        // The arithmetic against known values: L* 100 and 0; Viénot's deuteranope sees pure red and a matching green close.
        DeltaE(Colors.White, Colors.Black, Vision.Normal).Should().BeApproximately(100, 0.01);
        DeltaE(Color.FromRgb(0xFF, 0, 0), Color.FromRgb(0, 0x80, 0), Vision.Normal).Should().BeGreaterThan(80);
        DeltaE(Color.FromRgb(0xFF, 0, 0), Color.FromRgb(0, 0x80, 0), Vision.Deuteranopia)
            .Should().BeLessThan(DeltaE(Color.FromRgb(0xFF, 0, 0), Color.FromRgb(0, 0x80, 0), Vision.Normal) / 3);
    }

    // Every colour an edge can take: each palette's colour of each rank.
    private static (Color Nearest, double DeltaE) Nearest(Color tone, Vision vision) =>
        StopPalettes.All
            .SelectMany(p => Enum.GetValues<HaulRank>().Select(r => p.ColorOf((int)r)))
            .Distinct()
            .Select(c => (c, DeltaE(tone, c, vision)))
            .MinBy(x => x.Item2);

    private static Color AppBrush(string key)
    {
        using var stream = typeof(RouteToneTests).Assembly.GetManifestResourceStream("xaml/App.xaml")
            ?? throw new InvalidOperationException("Test resource 'xaml/App.xaml' not found.");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var brush = XDocument.Load(stream).Descendants().Single(e => (string?)e.Attribute(x + "Key") == key);
        return (Color)ColorConverter.ConvertFromString((string)brush.Attribute("Color")!);
    }

    // CIE 1976 ΔE*ab of two sRGB colours (IEC 61966-2-1, D65) as <paramref name="vision"/> sees them.
    private static double DeltaE(Color a, Color b, Vision vision)
    {
        var (l1, a1, b1) = Lab(a, vision);
        var (l2, a2, b2) = Lab(b, vision);
        return Math.Sqrt(Math.Pow(l1 - l2, 2) + Math.Pow(a1 - a2, 2) + Math.Pow(b1 - b2, 2));
    }

    private static (double L, double A, double B) Lab(Color color, Vision vision)
    {
        static double Linear(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var (r, g, b) = Simulated(Linear(color.R), Linear(color.G), Linear(color.B), vision);

        // sRGB → XYZ (D65), normalised to the white point. Source: IEC 61966-2-1:1999.
        var x = ((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047;
        var y = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        var z = ((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883;

        // XYZ → L*a*b*. Source: CIE 15:2004, Colorimetry, 3rd ed.
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + (16.0 / 116);
        return ((116 * F(y)) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)));
    }

    // Dichromat simulation in linear RGB. Source: Viénot, Brettel & Mollon 1999, "Digital video colourmaps for checking the
    // legibility of displays by dichromats", Color Research & Application 24 (4); the linear-RGB matrices as DaltonLens
    // gives them, https://daltonlens.org/opensource-cvd-simulation/
    private static (double R, double G, double B) Simulated(double r, double g, double b, Vision vision)
    {
        static double Clamp(double v) => Math.Clamp(v, 0, 1);
        switch (vision)
        {
            case Vision.Deuteranopia:
                var d = (0.29275 * r) + (0.70725 * g);
                return (Clamp(d), Clamp(d), Clamp((-0.02234 * r) + (0.02234 * g) + b));
            case Vision.Protanopia:
                var p = (0.11238 * r) + (0.88762 * g);
                return (Clamp(p), Clamp(p), Clamp((0.00401 * r) - (0.00401 * g) + b));
            default:
                return (r, g, b);
        }
    }
}
