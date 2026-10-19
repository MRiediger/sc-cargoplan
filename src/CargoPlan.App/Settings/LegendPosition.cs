namespace CargoPlan.App.Settings;

/// <summary>Which side of the ship view the legend stands on (D33).</summary>
public enum LegendEdge
{
    Top,
    Bottom,
}

/// <summary>Where the legend block stands across the window: it is as wide as its lines, not the window (D33).</summary>
public enum LegendAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>
/// The legend's place, written "bottom-left" in settings.json. The file holds a string, not an enum the serializer
/// checks: an unknown name then takes the default with a notice (<see cref="AppearanceRules"/>) instead of making the
/// whole file invalid (D13). WPF-free like <see cref="Argb"/>.
/// </summary>
public readonly record struct LegendPosition(LegendEdge Edge, LegendAlignment Alignment)
{
    /// <summary>The layout before it was a setting.</summary>
    public static LegendPosition Default { get; } = new(LegendEdge.Bottom, LegendAlignment.Left);

    private static readonly LegendPosition[] All =
        [.. Enum.GetValues<LegendEdge>().SelectMany(edge => Enum.GetValues<LegendAlignment>().Select(alignment => new LegendPosition(edge, alignment)))];

    /// <summary>Every valid name, top first; settings-format.md lists the same.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. All.Select(p => p.ToString())];

    /// <summary>Exactly one of <see cref="Names"/>, lower case like the keys; nothing else, no spaces.</summary>
    public static bool TryParse(string text, out LegendPosition position)
    {
        var index = Array.FindIndex(All, p => p.ToString() == text);
        position = index >= 0 ? All[index] : default;
        return index >= 0;
    }

    /// <exception cref="FormatException">Not a position; after <see cref="AppearanceRules.Normalize(LegendSettings)"/> that is a bug.</exception>
    public static LegendPosition Parse(string text) =>
        TryParse(text, out var position) ? position : throw new FormatException($"\"{text}\" is not a legend position ({string.Join(", ", Names)}).");

    public override string ToString() => $"{Edge}-{Alignment}".ToLowerInvariant();
}
