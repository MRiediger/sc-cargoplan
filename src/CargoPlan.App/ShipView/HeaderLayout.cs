namespace CargoPlan.App.ShipView;

/// <summary>
/// How far the header has given way (B15, D110). Each stage keeps what the one before it kept, minus one thing, so a
/// narrower window only ever shows less (R1 of the approval).
/// </summary>
public enum HeaderStage
{
    /// <summary>Ship name, capacity "0/192 · 192 free · 16 waiting", "All stops delivered.", tools, gear and exit on one line.</summary>
    Full,

    /// <summary>The status text is gone; its words stay in the capacity's tool tip.</summary>
    NoStatus,

    /// <summary>The capacity is short: "0/192 +16".</summary>
    CompactCapacity,

    /// <summary>
    /// The tool buttons go to a second line, left-aligned; the first line keeps name, short capacity, gear and exit. Locked
    /// tools and chrome measure 0, so there is no second line, and the stage only means the name may trim.
    /// </summary>
    ToolsWrapped,
}

/// <summary>
/// The header parts' own widths in DIP, margins included; <paramref name="Status"/> is 0 while it isn't shown,
/// <paramref name="Tools"/> (split … reset view) and <paramref name="Chrome"/> (gear, exit) 0 while locked.
/// </summary>
public sealed record HeaderWidths(double Name, double Status, double Capacity, double CompactCapacity, double Tools = 0, double Chrome = 0);

/// <summary>
/// What gives way in the header when room runs short (B15, D110, revises D67): the status first, then the capacity's long
/// form, then the tools move to a second line while gear and exit stay. The ship name trims only when it, the short
/// capacity and the chrome are wider than the row, which MinWidth rules out. A function of measured widths, tested without
/// WPF; <see cref="HeaderPanel"/> measures.
/// </summary>
public static class HeaderLayout
{
    public static HeaderStage Stage(HeaderWidths w, double available) =>
        w.Name + w.Capacity + w.Status + w.Tools + w.Chrome <= available ? HeaderStage.Full
        : w.Name + w.Capacity + w.Tools + w.Chrome <= available ? HeaderStage.NoStatus
        : w.Name + w.CompactCapacity + w.Tools + w.Chrome <= available ? HeaderStage.CompactCapacity
        : HeaderStage.ToolsWrapped;

    /// <summary>The name's own width, or what short capacity and chrome leave of the row when that is less.</summary>
    public static double NameWidth(HeaderWidths w, double available) =>
        Math.Min(w.Name, Math.Max(0, available - w.CompactCapacity - w.Chrome));
}
