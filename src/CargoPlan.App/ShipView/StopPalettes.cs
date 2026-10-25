using System.Windows.Media;
using CargoPlan.App.Settings;

namespace CargoPlan.App.ShipView;

/// <summary>How a stop's crates are marked in its colour's later rounds (K4.2): plain, then hatched one way, then the other.</summary>
public enum StopHatch
{
    None,

    /// <summary>Stripes rising to the right, "/".</summary>
    Forward,

    /// <summary>Stripes falling to the right, "\".</summary>
    Backward,
}

/// <summary>
/// One checked stop palette (D29, K4.2). A stop keeps its ordinal (<see cref="MissionPalette.Assign"/>, in the order the
/// stops first appear, D64); the palette turns it into a colour and, once the colours run out, a hatch.
/// </summary>
/// <param name="DisplayName">As the settings window lists it, without the count.</param>
/// <param name="Colors">Opaque; the crates' alpha is a setting of its own (D31).</param>
public sealed record StopPalette(StopPaletteName Name, string DisplayName, IReadOnlyList<Color> Colors)
{
    /// <summary>The rounds of plain and hatched colours before both repeat.</summary>
    public const int Rounds = 3;

    /// <summary>"Okabe-Ito · 8 colors": what the settings window's box shows.</summary>
    public string Text => $"{DisplayName} · {Colors.Count} colors";

    public Color ColorOf(int ordinal) => Colors[ordinal % Colors.Count];

    public StopHatch HatchOf(int ordinal) => (StopHatch)(ordinal / Colors.Count % Rounds);

    /// <summary>The legend's line when colours repeat (D15), null while every stop has its own colour.</summary>
    public string? RepeatNotice(int stops) => stops switch
    {
        _ when stops > Colors.Count * Rounds => $"More than {Colors.Count * Rounds} stops: colors and hatching repeat.",
        _ when stops > Colors.Count => $"More than {Colors.Count} stops: colors repeat, hatched from stop {Colors.Count + 1}.",
        _ => null,
    };
}

/// <summary>
/// The palettes, values from their sources unchanged but for the colours named below; each source documents its palette
/// as safe for deuteranopia and protanopia. What goes out follows one rule: a colour that vanishes on the dark overlay or
/// looks like a state colour (delivered grey) is no stop colour.
/// </summary>
public static class StopPalettes
{
    /// <summary>
    /// Okabe-Ito without its black (invisible on the dark overlay) and with white as eighth colour (D15): the palette of
    /// before, so the default looks the same.
    /// Source: Okabe &amp; Ito (2008), Color Universal Design, https://jfly.uni-koeln.de/color/ (the values of Wong 2011,
    /// Nature Methods 8:441, doi:10.1038/nmeth.1618)
    /// </summary>
    public static StopPalette OkabeIto { get; } = new(StopPaletteName.OkabeIto, "Okabe-Ito",
    [
        Color.FromRgb(0xE6, 0x9F, 0x00), // orange
        Color.FromRgb(0x56, 0xB4, 0xE9), // sky blue
        Color.FromRgb(0x00, 0x9E, 0x73), // bluish green
        Color.FromRgb(0xF0, 0xE4, 0x42), // yellow
        Color.FromRgb(0x00, 0x72, 0xB2), // blue
        Color.FromRgb(0xD5, 0x5E, 0x00), // vermillion
        Color.FromRgb(0xCC, 0x79, 0xA7), // reddish purple
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white
    ]);

    /// <summary>
    /// Paul Tol's bright scheme ("colour-blind safe") without its grey BBBBBB: Tol meant it for bad data, and in the overlay
    /// grey is delivered cargo (Martin at K4's approval).
    /// Source: Paul Tol, "Colour schemes", https://sronpersonalpages.nl/~pault/ (read 25 Oct 2026)
    /// </summary>
    public static StopPalette TolBright { get; } = new(StopPaletteName.TolBright, "Tol Bright",
    [
        Color.FromRgb(0x44, 0x77, 0xAA), // blue
        Color.FromRgb(0xEE, 0x66, 0x77), // red
        Color.FromRgb(0x22, 0x88, 0x33), // green
        Color.FromRgb(0xCC, 0xBB, 0x44), // yellow
        Color.FromRgb(0x66, 0xCC, 0xEE), // cyan
        Color.FromRgb(0xAA, 0x33, 0x77), // purple
    ]);

    /// <summary>
    /// Paul Tol's muted scheme ("equally colour-blind safe") without indigo 332288 (1.5:1 on the window's #141414) and wine
    /// 882255 (2.1:1), which vanish on the dark overlay like Okabe-Ito's black (D15); its pale grey is for bad data only.
    /// Source: Paul Tol, "Colour schemes", https://sronpersonalpages.nl/~pault/ (read 25 Oct 2026)
    /// </summary>
    public static StopPalette TolMuted { get; } = new(StopPaletteName.TolMuted, "Tol Muted",
    [
        Color.FromRgb(0xCC, 0x66, 0x77), // rose
        Color.FromRgb(0xDD, 0xCC, 0x77), // sand
        Color.FromRgb(0x11, 0x77, 0x33), // green
        Color.FromRgb(0x88, 0xCC, 0xEE), // cyan
        Color.FromRgb(0x44, 0xAA, 0x99), // teal
        Color.FromRgb(0x99, 0x99, 0x33), // olive
        Color.FromRgb(0xAA, 0x44, 0x99), // purple
    ]);

    /// <summary>In <see cref="StopPaletteName"/>'s order, the settings window's too.</summary>
    public static IReadOnlyList<StopPalette> All { get; } = [OkabeIto, TolBright, TolMuted];

    public static StopPalette For(StopPaletteName name) => All.Single(p => p.Name == name);
}
