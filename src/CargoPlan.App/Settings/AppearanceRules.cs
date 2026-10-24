using System.Globalization;

namespace CargoPlan.App.Settings;

/// <summary>
/// Checks the values of <see cref="AppearanceSettings"/>, <see cref="LegendSettings"/>, the tool windows' mode and ContractTracker's path after
/// loading (D31, D33, D36, D44). A number outside its range is clamped (closer
/// to what the player meant than the default), an invalid colour takes its default; either way with a problem line for
/// the overlay, and the rest of the file applies. Structural errors (unknown key, wrong JSON type) never get here:
/// they make the whole file invalid in <see cref="SettingsStore"/>. No WPF: the load tests run without a window.
/// </summary>
public static class AppearanceRules
{
    public const double MinFontSize = 8;
    public const double MaxFontSize = 32;

    // A range of its own: the default (27) is near the legend's top, and a large overlay on a large screen wants more.
    public const double MinRackNumberFontSize = 8;
    public const double MaxRackNumberFontSize = 48;
    public const double MinHaloSize = 1;
    public const double MaxHaloSize = 20;
    public const double MaxBorderThickness = 10;

    /// <summary>Half the height of a header button (26 px): more would make it a pill that no longer fits.</summary>
    public const double MaxCornerRadius = 13;

    /// <summary>
    /// The least alpha of a background that takes clicks: a layered window passes clicks on pixels with alpha 0 to the
    /// window below, so the overlay could no longer be moved or a button pressed.
    /// </summary>
    public const byte MinClickableAlpha = 1;

    /// <summary>The hologram's opacity (D63): below a tenth it can't be seen, and "None" is the way to have none.</summary>
    public const double MinHoloOpacity = 0.1;

    public const double MaxHoloOpacity = 1;

    /// <summary>Martin's look gate in the game (T7.4c-2): a backdrop, the racks stay what the eye finds first.</summary>
    public const double DefaultHoloOpacity = 0.4;

    /// <summary>The hull in the shaded and flat looks (D63): a cool grey, apart from every stop colour.</summary>
    public const string DefaultHoloHullColor = "#FF9CB0C0";

    /// <summary>The edges in the hidden-line look (D63), in the rack edges' grey.</summary>
    public const string DefaultHoloEdgeColor = "#FFE6E6E6";

    /// <returns>The checked appearance and one line per corrected value, empty if nothing was corrected.</returns>
    public static (AppearanceSettings Appearance, IReadOnlyList<string> Problems) Normalize(AppearanceSettings appearance)
    {
        Checker check = new();

        var window = appearance.Window;
        var header = appearance.Header;
        var legend = appearance.Legend;
        var crates = appearance.Crates;
        var buttons = appearance.Buttons;

        AppearanceSettings normalized = new(
            window with
            {
                Background = check.Colour("window.background", window.Background, WindowAppearance.Default.Background, MinClickableAlpha),
                BorderColor = check.Colour("window.borderColor", window.BorderColor, WindowAppearance.Default.BorderColor),
                BorderThickness = check.Range("window.borderThickness", window.BorderThickness, 0, MaxBorderThickness),
            },
            header with
            {
                Foreground = check.Colour("header.foreground", header.Foreground, HeaderAppearance.Default.Foreground),
                Halo = check.Halo("header.halo", header.Halo),
            },
            legend with
            {
                Foreground = check.Colour("legend.foreground", legend.Foreground, LegendAppearance.Default.Foreground),

                // null is the Windows size and passes; only a number has a range (resolved in ThemeResources).
                FontSize = legend.FontSize is { } size ? check.Range("legend.fontSize", size, MinFontSize, MaxFontSize) : null,
                Halo = check.Halo("legend.halo", legend.Halo),
            },
            crates with
            {
                FillAlpha = (int)check.Range("crates.fillAlpha", crates.FillAlpha, byte.MinValue, byte.MaxValue),
                EdgeAlpha = (int)check.Range("crates.edgeAlpha", crates.EdgeAlpha, byte.MinValue, byte.MaxValue),
                RackNumberColor = check.Colour("crates.rackNumberColor", crates.RackNumberColor, CrateAppearance.Default.RackNumberColor),
                RackNumberFontSize = check.Range("crates.rackNumberFontSize", crates.RackNumberFontSize, MinRackNumberFontSize, MaxRackNumberFontSize),
                Halo = check.Halo("crates.halo", crates.Halo),
            },
            buttons with
            {
                Background = check.Colour("buttons.background", buttons.Background, ButtonAppearance.Default.Background, MinClickableAlpha),
                BorderColor = check.Colour("buttons.borderColor", buttons.BorderColor, ButtonAppearance.Default.BorderColor),
                BorderThickness = check.Range("buttons.borderThickness", buttons.BorderThickness, 0, MaxBorderThickness),
                CornerRadius = check.Range("buttons.cornerRadius", buttons.CornerRadius, 0, MaxCornerRadius),
                Halo = check.Halo("buttons.halo", buttons.Halo),
            });

        return (normalized, check.Problems);
    }

    /// <summary>Checks the legend's layout (D33): an unknown position takes the default, like a colour that isn't one.</summary>
    /// <returns>The checked legend and one line per corrected value, empty if nothing was corrected.</returns>
    public static (LegendSettings Legend, IReadOnlyList<string> Problems) Normalize(LegendSettings legend)
    {
        Checker check = new();
        return (legend with { Position = check.Position("legend.position", legend.Position) }, check.Problems);
    }

    /// <summary>Checks the tool windows' mode (D36): an unknown name follows Windows, like a colour that isn't one.</summary>
    /// <returns>The checked name and one line if it was corrected, empty otherwise.</returns>
    public static (string Theme, IReadOnlyList<string> Problems) NormalizeToolWindowTheme(string theme)
    {
        if (ToolWindowThemes.TryParse(theme, out _))
        {
            return (theme, []);
        }

        var fallback = ToolWindowThemes.DefaultName;
        var names = ToolWindowThemes.Names;
        return (fallback, [$"toolWindowTheme \"{theme}\" is not valid ({string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}), using {fallback}."]);
    }

    /// <summary>Checks the bow marker's mode (D62): an unknown name draws the outline, like a colour that isn't one.</summary>
    /// <returns>The checked name and one line if it was corrected, empty otherwise.</returns>
    public static (string Mode, IReadOnlyList<string> Problems) NormalizeShipOutline(string mode)
    {
        if (ShipOutlineModes.TryParse(mode, out _))
        {
            return (mode, []);
        }

        var fallback = ShipOutlineModes.DefaultName;
        var names = ShipOutlineModes.Names;
        return (fallback, [$"shipOutline \"{mode}\" is not valid ({string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}), using {fallback}."]);
    }

    /// <summary>Checks the hologram's opacity (D63): outside its range it is clamped, like a slider value in the appearance.</summary>
    /// <returns>The checked value and one line if it was corrected, empty otherwise.</returns>
    public static (double Opacity, IReadOnlyList<string> Problems) NormalizeHoloOpacity(double opacity)
    {
        var clamped = Math.Clamp(opacity, MinHoloOpacity, MaxHoloOpacity);
        return clamped == opacity
            ? (opacity, [])
            : (clamped, [string.Create(CultureInfo.InvariantCulture, $"holoOpacity {opacity} is outside {MinHoloOpacity}–{MaxHoloOpacity}, using {clamped}.")]);
    }

    /// <summary>
    /// Checks one of the hologram's colours (D63): not a colour takes its default, an alpha is set to opaque, since WPF 3D
    /// sorts no transparent triangles (the opacity has a slider of its own); either way with a line.
    /// </summary>
    /// <param name="key">The top-level key, for the line: "holoHullColor" or "holoEdgeColor".</param>
    /// <returns>The checked colour as "#AARRGGBB" and one line if it was corrected, empty otherwise.</returns>
    public static (string Colour, IReadOnlyList<string> Problems) NormalizeHoloColor(string key, string colour, string fallback)
    {
        if (!Argb.TryParse(colour, out var argb))
        {
            return (fallback, [$"{key} \"{colour}\" is not a colour (#RRGGBB), using {fallback}."]);
        }

        if (argb.A == byte.MaxValue)
        {
            return (argb.ToString(), []);
        }

        var opaque = (argb with { A = byte.MaxValue }).ToString();
        return (opaque, [$"{key} alpha {argb.A} is not used, holograms are opaque, using {opaque}."]);
    }

    /// <summary>
    /// Checks ContractTracker's path (D44): one that isn't a full path (relative, empty) detects ContractTracker instead,
    /// like a colour that isn't one. Whether the file exists is not checked: ContractTracker may be installed later.
    /// </summary>
    /// <returns>The checked settings and one line if the path was corrected, empty otherwise.</returns>
    public static (ContractTrackerSettings ContractTracker, IReadOnlyList<string> Problems) NormalizeContractTracker(
        ContractTrackerSettings contractTracker)
    {
        // Source: https://learn.microsoft.com/dotnet/api/system.io.path.ispathfullyqualified
        if (contractTracker.Path is not { } path || System.IO.Path.IsPathFullyQualified(path))
        {
            return (contractTracker, []);
        }

        return (contractTracker with { Path = null }, [$"contractTracker.path \"{path}\" is not a full path, detecting ContractTracker instead."]);
    }

    /// <summary>Collects the problem lines while one appearance is checked.</summary>
    private sealed class Checker
    {
        public List<string> Problems { get; } = [];

        public double Range(string key, double value, double min, double max)
        {
            var clamped = Math.Clamp(value, min, max);
            if (clamped != value)
            {
                Problems.Add($"appearance.{key} {Format(value)} is outside {Format(min)}–{Format(max)}, using {Format(clamped)}.");
            }

            return clamped;
        }

        public string Colour(string key, string text, string fallback, byte minAlpha = 0)
        {
            if (!Argb.TryParse(text, out var colour))
            {
                Problems.Add($"appearance.{key} \"{text}\" is not a colour (#AARRGGBB or #RRGGBB), using {fallback}.");
                return fallback;
            }

            if (colour.A < minAlpha)
            {
                Problems.Add($"appearance.{key} alpha {colour.A} would let clicks through, using alpha {minAlpha}.");
                colour = colour with { A = minAlpha };
            }

            return colour.ToString();
        }

        /// <summary>
        /// Top level, so the key has no "appearance." in front. The line names the pattern, not all six names: in the
        /// default overlay those would be three amber lines (settings-format.md lists them).
        /// </summary>
        public string Position(string key, string text)
        {
            if (LegendPosition.TryParse(text, out _))
            {
                return text;
            }

            var fallback = LegendPosition.Default.ToString();
            Problems.Add($"{key} \"{text}\" is not valid (edge-alignment, e.g. bottom-right), using {fallback}.");
            return fallback;
        }

        public HaloSettings Halo(string key, HaloSettings halo) => halo with
        {
            Color = Colour($"{key}.color", halo.Color, HaloSettings.Off.Color),
            Size = Range($"{key}.size", halo.Size, MinHaloSize, MaxHaloSize),
        };

        private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>A colour as settings.json writes it, "#AARRGGBB"; WPF-free so the model and its tests need no window.</summary>
public readonly record struct Argb(byte A, byte R, byte G, byte B)
{
    /// <summary>"#AARRGGBB" or "#RRGGBB" (opaque), hex digits in any case; nothing else, no names, no spaces.</summary>
    public static bool TryParse(string text, out Argb colour)
    {
        colour = default;
        if (text is not ['#', .. var digits] || digits.Length is not (6 or 8)
            || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (digits.Length == 6)
        {
            value |= 0xFF000000;
        }

        colour = new((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    /// <exception cref="FormatException">Not a colour; after <see cref="AppearanceRules.Normalize"/> that is a bug.</exception>
    public static Argb Parse(string text) =>
        TryParse(text, out var colour) ? colour : throw new FormatException($"\"{text}\" is not a colour (#AARRGGBB or #RRGGBB).");

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
