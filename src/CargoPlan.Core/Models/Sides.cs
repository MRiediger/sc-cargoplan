namespace CargoPlan.Core.Models;

/// <summary>
/// Whether a plan fills a rack one side at a time (D116): the rack's width split into two lanes, port and starboard,
/// each filled like a whole rack before the next; an odd width's middle column comes last. Stored in the plan, as
/// <see cref="FillOrder"/> and <see cref="FillRows"/>, which then say how a lane fills. Only racks whose width lies across
/// the ship and is at least four cells get lanes.
/// </summary>
public enum Sides
{
    /// <summary>The whole width as one, as before.</summary>
    None,

    /// <summary>The port half first, then the starboard half.</summary>
    PortFirst,

    /// <summary>The starboard half first, then the port half.</summary>
    StarboardFirst,
}
