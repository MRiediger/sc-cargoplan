namespace CargoPlan.App.ShipView;

/// <summary>How far the header has given way (B15, D110); each stage keeps what the one before it kept, minus one thing.</summary>
public enum HeaderStage
{
    /// <summary>Ship name, capacity "0/192 · 192 free · 16 waiting" and "All stops delivered.".</summary>
    Full,

    /// <summary>The status text is gone; its words stay in the capacity's tool tip.</summary>
    NoStatus,

    /// <summary>The capacity is short: "0/192 +16".</summary>
    CompactCapacity,

    /// <summary>Even name and short capacity don't fit: the name ends in an ellipsis, the short capacity stays.</summary>
    TrimmedName,
}

/// <summary>The header parts' own widths in DIP, margins included; <paramref name="Status"/> is 0 while it isn't shown.</summary>
public sealed record HeaderWidths(double Name, double Status, double Capacity, double CompactCapacity);

/// <summary>
/// What gives way in the header when room runs short (B15, D110, revises D67): the status first, then the capacity's long
/// form, the ship name last. A function of measured widths, so it is tested without WPF; <see cref="HeaderPanel"/> measures.
/// </summary>
public static class HeaderLayout
{
    public static HeaderStage Stage(HeaderWidths widths, double available) =>
        widths.Name + widths.Capacity + widths.Status <= available ? HeaderStage.Full
        : widths.Name + widths.Capacity <= available ? HeaderStage.NoStatus
        : widths.Name + widths.CompactCapacity <= available ? HeaderStage.CompactCapacity
        : HeaderStage.TrimmedName;

    /// <summary>The name's own width up to the last stage, then what the short capacity leaves of <paramref name="available"/>.</summary>
    public static double NameWidth(HeaderWidths widths, double available) =>
        Stage(widths, available) == HeaderStage.TrimmedName ? Math.Max(0, available - widths.CompactCapacity) : widths.Name;
}
