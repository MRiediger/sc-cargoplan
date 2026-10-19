namespace CargoPlan.App.Settings;

/// <summary>
/// A colour as the picker shows it (T4.2c): hue around the colour wheel, saturation from grey to full colour, value from
/// black to full brightness. WPF-free like <see cref="Argb"/>; settings.json keeps "#AARRGGBB", this is only the way there.
/// Source: https://en.wikipedia.org/wiki/HSL_and_HSV (sections "From RGB" and "HSV to RGB")
/// </summary>
/// <param name="Hue">Degrees, 0–360; 0 and 360 are red.</param>
/// <param name="Saturation">0 (grey) to 1 (full colour).</param>
/// <param name="Value">0 (black) to 1 (full brightness).</param>
public readonly record struct Hsv(double Hue, double Saturation, double Value)
{
    private const double DegreesPerSector = 60;
    private const double FullCircle = 360;

    /// <summary>Grey has no hue and black no saturation: both come back as 0, the caller keeps its own (<see cref="ViewModels.ColourField"/>).</summary>
    public static Hsv FromRgb(Argb colour)
    {
        double r = colour.R / (double)byte.MaxValue, g = colour.G / (double)byte.MaxValue, b = colour.B / (double)byte.MaxValue;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));

        var sector = delta == 0 ? 0
            : max == r ? (g - b) / delta
            : max == g ? (b - r) / delta + 2
            : (r - g) / delta + 4;
        var hue = (sector * DegreesPerSector + FullCircle) % FullCircle;

        return new(hue, max == 0 ? 0 : delta / max, max);
    }

    /// <param name="alpha">Not part of HSV: the picker leaves the transparency to its own slider.</param>
    public Argb ToArgb(byte alpha)
    {
        var chroma = Value * Saturation;
        var sector = Hue % FullCircle / DegreesPerSector;
        var second = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };
        var match = Value - chroma; // "m" in the source: lifts each channel to the value.

        return new(alpha, Byte(r + match), Byte(g + match), Byte(b + match));
    }

    private static byte Byte(double fraction) => (byte)Math.Round(fraction * byte.MaxValue, MidpointRounding.AwayFromZero);
}
