namespace CargoPlan.App.Settings;

/// <summary>
/// What marks the bow in the ship view (D62): the ship's outline, or nothing. A ship without an outline shows the bow
/// wedge (D52) under <see cref="Outline"/>. <see cref="Outline"/> first, so <c>default</c> is the outline.
/// </summary>
public enum ShipOutlineMode
{
    Outline,
    None,
}

/// <summary>
/// The modes as settings.json writes them, "outline", "none"; a string in the file like <see cref="ToolWindowThemes"/>, so
/// an unknown name draws the outline with a notice (<see cref="AppearanceRules"/>) instead of making the file invalid (D13).
/// </summary>
public static class ShipOutlineModes
{
    /// <summary>What a missing or null key means: the outline.</summary>
    public static string DefaultName { get; } = ToName(ShipOutlineMode.Outline);

    private static readonly ShipOutlineMode[] All = Enum.GetValues<ShipOutlineMode>();

    /// <summary>Every valid name, in the enum's order; settings-format.md lists the same.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. All.Select(ToName)];

    /// <summary>Exactly one of <see cref="Names"/>, lower case like the keys; not a number, unlike <c>Enum.TryParse</c>.</summary>
    public static bool TryParse(string text, out ShipOutlineMode mode)
    {
        var index = Array.FindIndex(All, m => ToName(m) == text);
        mode = index >= 0 ? All[index] : default;
        return index >= 0;
    }

    /// <exception cref="FormatException">Not a mode; after <see cref="AppearanceRules.NormalizeShipOutline"/> that is a bug.</exception>
    public static ShipOutlineMode Parse(string text) =>
        TryParse(text, out var mode) ? mode : throw new FormatException($"\"{text}\" is not a ship outline mode ({string.Join(", ", Names)}).");

    public static string ToName(ShipOutlineMode mode) => mode.ToString().ToLowerInvariant();
}
