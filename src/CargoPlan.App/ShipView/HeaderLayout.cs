namespace CargoPlan.App.ShipView;

/// <summary>
/// How far the header has given way (B15, D110). Each stage keeps what the one before it kept, minus one thing, so a
/// narrower window only ever shows less (R1 of the approval).
/// </summary>
public enum HeaderStage
{
    /// <summary>Ship name, capacity "0/192 · 192 free · 16 waiting", "All stops delivered." and the buttons on one line.</summary>
    Full,

    /// <summary>The status text is gone; its words stay in the capacity's tool tip.</summary>
    NoStatus,

    /// <summary>The capacity is short: "0/192 +16".</summary>
    CompactCapacity,

    /// <summary>
    /// The buttons go to a second line, right-aligned where they stood; the first line keeps name and short capacity. Locked
    /// the buttons measure 0, so there is no second line, and the stage only means the name may trim.
    /// </summary>
    ButtonsWrapped,
}

/// <summary>
/// The header parts' own widths in DIP, margins included; <paramref name="Status"/> is 0 while it isn't shown,
/// <paramref name="Buttons"/> 0 while locked.
/// </summary>
public sealed record HeaderWidths(double Name, double Status, double Capacity, double CompactCapacity, double Buttons = 0);

/// <summary>
/// What gives way in the header when room runs short (B15, D110, revises D67): the status first, then the capacity's long
/// form, then the buttons move to a second line. The ship name trims only when it and the short capacity alone are wider
/// than the row. A function of measured widths, tested without WPF; <see cref="HeaderPanel"/> measures.
/// </summary>
public static class HeaderLayout
{
    public static HeaderStage Stage(HeaderWidths widths, double available) =>
        widths.Name + widths.Capacity + widths.Status + widths.Buttons <= available ? HeaderStage.Full
        : widths.Name + widths.Capacity + widths.Buttons <= available ? HeaderStage.NoStatus
        : widths.Name + widths.CompactCapacity + widths.Buttons <= available ? HeaderStage.CompactCapacity
        : HeaderStage.ButtonsWrapped;

    /// <summary>The name's own width, or what the short capacity leaves of the row when that is less.</summary>
    public static double NameWidth(HeaderWidths widths, double available) =>
        Math.Min(widths.Name, Math.Max(0, available - widths.CompactCapacity));
}
