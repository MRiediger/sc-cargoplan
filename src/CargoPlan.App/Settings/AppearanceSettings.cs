namespace CargoPlan.App.Settings;

// The overlay's look in settings.json (schema 2, D31). Colours are "#AARRGGBB" text, so the model needs no WPF types:
// AppearanceRules checks them, ThemeResources turns them into brushes. Every value is optional in the file, and the
// defaults are the look the overlay had before it was configurable.
//
// Positional records with default parameters, not init properties with initializers: up to .NET 10 the source generator
// sets every init property, an omitted one to null or 0 (checked in a spike, T4.1). "Starting in .NET 11, a
// source-generated setter runs only when the JSON payload contains the property."
// Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/immutability#init-only-properties
// A nested record can't be a default parameter value (not a constant), so those parameters are nullable and the
// property of the same name takes the section's default: a missing or null section means its defaults.

/// <summary>All sections; each has a <c>Default</c> for "Reset to defaults" per section (D30).</summary>
public sealed record AppearanceSettings(
    WindowAppearance? Window = null,
    HeaderAppearance? Header = null,
    LegendAppearance? Legend = null,
    CrateAppearance? Crates = null,
    ButtonAppearance? Buttons = null)
{
    public static AppearanceSettings Default { get; } = new();

    public WindowAppearance Window { get; init; } = Window ?? WindowAppearance.Default;

    public HeaderAppearance Header { get; init; } = Header ?? HeaderAppearance.Default;

    public LegendAppearance Legend { get; init; } = Legend ?? LegendAppearance.Default;

    public CrateAppearance Crates { get; init; } = Crates ?? CrateAppearance.Default;

    public ButtonAppearance Buttons { get; init; } = Buttons ?? ButtonAppearance.Default;
}

/// <summary>The overlay's background and frame. "Transparency" is the alpha of <paramref name="Background"/>.</summary>
/// <param name="Background">At least alpha 1, or clicks pass through the overlay (<see cref="AppearanceRules"/>).</param>
/// <param name="BorderThickness">0 = no frame.</param>
public sealed record WindowAppearance(string Background = "#A0141414", string BorderColor = "#FFE6E6E6", double BorderThickness = 0)
{
    public static WindowAppearance Default { get; } = new();
}

/// <summary>Ship name, "All stops delivered." and the glyphs in the header, padlock included.</summary>
public sealed record HeaderAppearance(string Foreground = "#FFE6E6E6", HaloSettings? Halo = null)
{
    public static HeaderAppearance Default { get; } = new();

    public HaloSettings Halo { get; init; } = Halo ?? HaloSettings.Off;
}

/// <summary>Stop lines, hints, violations and notices.</summary>
/// <param name="FontSize">null = the Windows message font size, as before settings existed; a number overrides it.</param>
/// <param name="CheckBoxColor">
/// The frame of the legend's check boxes (D69, K4.1). The default grey is Martin's from B6's look gate (not the selection's
/// magenta): lighter than the grey of a ticked line's text, which has no box anyway, darker than the legend's text.
/// Alpha 0 is allowed, the box takes clicks anyway.
/// </param>
/// <param name="CheckMarkColor">What stands in the box: the tick for all selected, the bar for part of them.</param>
public sealed record LegendAppearance(
    string Foreground = "#FFE6E6E6",
    double? FontSize = null,
    HaloSettings? Halo = null,
    string CheckBoxColor = "#FFB0B0B0",
    string CheckMarkColor = "#FFB0B0B0")
{
    public static LegendAppearance Default { get; } = new();

    public HaloSettings Halo { get; init; } = Halo ?? HaloSettings.Off;
}

/// <summary>
/// Crates in the ship view. Their colours are the stop colours and stay fixed (D29), so only their alpha is set here.
/// The halo is the outline of the rack numbers (D28).
/// </summary>
/// <param name="FillAlpha">0–255; int, not byte, so 300 in the file is clamped instead of breaking it.</param>
/// <param name="EdgeAlpha">0–255, the edges of the crates.</param>
/// <param name="RackNumberFontSize">
/// Pixels, the same at every zoom; smaller only where the number would no longer fit its rack (D32). The default is the
/// size at zoom 1 in the default overlay before it was a setting.
/// </param>
public sealed record CrateAppearance(
    int FillAlpha = 0xD0,
    int EdgeAlpha = 0xC0,
    string RackNumberColor = "#FFFFFFFF",
    double RackNumberFontSize = 27,
    HaloSettings? Halo = null)
{
    public static CrateAppearance Default { get; } = new();

    public HaloSettings Halo { get; init; } = Halo ?? HaloSettings.On;
}

/// <summary>
/// The glyph buttons (pencil, split, merge, padlock) and "Open editor". The border is the glyph buttons' only:
/// "Open editor" keeps its outline in the legend's text colour.
/// </summary>
/// <param name="Background">At least alpha 1, or clicks pass through the button.</param>
/// <param name="BorderThickness">0 = no frame.</param>
public sealed record ButtonAppearance(
    string Background = "#01000000",
    string BorderColor = "#FFE6E6E6",
    double BorderThickness = 0,
    double CornerRadius = 3,
    HaloSettings? Halo = null)
{
    public static ButtonAppearance Default { get; } = new();

    public HaloSettings Halo { get; init; } = Halo ?? HaloSettings.Off;
}

/// <summary>
/// A soft outline around text and glyphs (D28). Enabled is its own switch, so turning it off keeps colour and size.
/// Missing keys inside a halo take these defaults, not the section's: a crates halo written as <c>{ "size": 5 }</c> is off.
/// </summary>
/// <param name="Size">Blur radius in pixels for text, outline width for rack numbers.</param>
public sealed record HaloSettings(bool Enabled = false, string Color = "#E0141414", double Size = 3)
{
    public static HaloSettings Off { get; } = new();

    public static HaloSettings On { get; } = new(Enabled: true);
}
