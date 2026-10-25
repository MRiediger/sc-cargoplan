namespace CargoPlan.App.Settings;

/// <summary>
/// The stop palettes a player can choose (K4.2, D29): each checked for the common colour vision deficiencies by its
/// source. <see cref="OkabeIto"/> first, so <c>default</c> is the palette of before. The colours live in the ship view
/// (<c>StopPalettes</c>); the settings only know the names, WPF-free like <see cref="ToolWindowTheme"/>.
/// </summary>
public enum StopPaletteName
{
    OkabeIto,
    TolBright,
    TolMuted,
}

/// <summary>
/// The names as settings.json writes them, "okabeIto", "tolBright", "tolMuted": a string in the file, so an unknown name
/// takes Okabe-Ito with a notice (<see cref="AppearanceRules"/>) instead of making the whole file invalid (D13).
/// </summary>
public static class StopPaletteNames
{
    /// <summary>What a missing key means: the palette of before.</summary>
    public static string DefaultName { get; } = ToName(StopPaletteName.OkabeIto);

    private static readonly StopPaletteName[] All = Enum.GetValues<StopPaletteName>();

    /// <summary>Every valid name, in the enum's order; settings-format.md lists the same.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. All.Select(ToName)];

    /// <summary>Exactly one of <see cref="Names"/>, case included; not a number, unlike <c>Enum.TryParse</c>.</summary>
    public static bool TryParse(string text, out StopPaletteName name)
    {
        var index = Array.FindIndex(All, n => ToName(n) == text);
        name = index >= 0 ? All[index] : default;
        return index >= 0;
    }

    /// <exception cref="FormatException">Not a palette; after <see cref="AppearanceRules.Normalize(AppearanceSettings)"/> that is a bug.</exception>
    public static StopPaletteName Parse(string text) =>
        TryParse(text, out var name) ? name : throw new FormatException($"\"{text}\" is not a stop palette ({string.Join(", ", Names)}).");

    /// <summary>camelCase like the keys around it: OkabeIto → "okabeIto".</summary>
    public static string ToName(StopPaletteName name) => $"{char.ToLowerInvariant(name.ToString()[0])}{name.ToString()[1..]}";
}
