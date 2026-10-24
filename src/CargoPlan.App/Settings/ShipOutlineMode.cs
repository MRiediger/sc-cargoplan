namespace CargoPlan.App.Settings;

/// <summary>
/// What lies behind the racks in the ship view (D62, D63): the ship's outline, its hologram in one of three looks, or
/// nothing. A ship without an outline shows the bow wedge (D52) under <see cref="Outline"/>; a hologram look shows the
/// outline until the hologram is there, and for good when there is none. <see cref="Outline"/> first, so
/// <c>default</c> is the outline.
/// </summary>
public enum ShipOutlineMode
{
    Outline,
    HoloShaded,
    HoloFlat,
    HoloHiddenline,
    None,
}

/// <summary>
/// The modes as settings.json writes them, "outline", "holo-shaded", "holo-flat", "holo-hiddenline", "none"; a string in
/// the file like <see cref="ToolWindowThemes"/>, so an unknown name draws the outline with a notice
/// (<see cref="AppearanceRules"/>) instead of making the file invalid (D13).
/// </summary>
public static class ShipOutlineModes
{
    // By hand: the enum's names lower-cased would lack the hyphen.
    private static readonly (ShipOutlineMode Mode, string Name)[] Table =
    [
        (ShipOutlineMode.Outline, "outline"),
        (ShipOutlineMode.HoloShaded, "holo-shaded"),
        (ShipOutlineMode.HoloFlat, "holo-flat"),
        (ShipOutlineMode.HoloHiddenline, "holo-hiddenline"),
        (ShipOutlineMode.None, "none"),
    ];

    /// <summary>What a missing or null key means: the outline.</summary>
    public static string DefaultName { get; } = ToName(ShipOutlineMode.Outline);

    /// <summary>Every valid name, in the enum's order; settings-format.md lists the same.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Table.Select(t => t.Name)];

    /// <summary>Exactly one of <see cref="Names"/>, lower case like the keys; not a number, unlike <c>Enum.TryParse</c>.</summary>
    public static bool TryParse(string text, out ShipOutlineMode mode)
    {
        var index = Array.FindIndex(Table, t => t.Name == text);
        mode = index >= 0 ? Table[index].Mode : default;
        return index >= 0;
    }

    /// <exception cref="FormatException">Not a mode; after <see cref="AppearanceRules.NormalizeShipOutline"/> that is a bug.</exception>
    public static ShipOutlineMode Parse(string text) =>
        TryParse(text, out var mode) ? mode : throw new FormatException($"\"{text}\" is not a ship outline mode ({string.Join(", ", Names)}).");

    public static string ToName(ShipOutlineMode mode) => Table.Single(t => t.Mode == mode).Name;

    /// <summary>One of the three hologram looks (D63).</summary>
    public static bool IsHologram(this ShipOutlineMode mode) => mode is ShipOutlineMode.HoloShaded or ShipOutlineMode.HoloFlat or ShipOutlineMode.HoloHiddenline;
}
