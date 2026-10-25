using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>Why a ship takes no crate above its <see cref="CrateCap"/> (D70).</summary>
public enum CapReason
{
    /// <summary>Some rack takes a 32: nothing is capped.</summary>
    None,

    /// <summary>A rack's grid would take a larger crate, its door doesn't (<see cref="Rack.MaxCrateSize"/>).</summary>
    Door,

    /// <summary>No rack's grid takes a larger crate.</summary>
    LargestRack,

    /// <summary>Read from a plan file whose crates were derived under another cap; the overlay plans it anew.</summary>
    Saved,
}

/// <summary>
/// The largest crate any rack of a ship takes (<see cref="Rack.CanHold"/>), and why (D70). A mix the planner derives is
/// split up to it (<see cref="Missions.MissionItem.CrateListUnder"/>), a mix the player set is not. Crate shapes nest, so
/// every smaller crate fits the rack that takes this one. The one place for the texts that name a door or a rack limit.
/// </summary>
public sealed record CrateCap(CrateSize Size, CapReason Reason)
{
    /// <summary>No cap: the contract alone decides the mix, as before D70.</summary>
    public static CrateCap None { get; } = new(CrateSize.ThirtyTwo, CapReason.None);

    public static CrateCap Of(Ship ship) => Of(ship.Racks);

    public static CrateCap Of(Rack rack) => Of([rack]);

    /// <summary>The largest size any of <paramref name="racks"/> takes; a door if some rack's grid would take more.</summary>
    public static CrateCap Of(IEnumerable<Rack> racks)
    {
        Rack[] all = [.. racks];
        var size = Largest(all, r => r.CanHold);
        var reason = Largest(all, r => r.FitsGrid) > size ? CapReason.Door
            : size == CrateSize.ThirtyTwo ? CapReason.None
            : CapReason.LargestRack;
        return new CrateCap(size, reason);
    }

    /// <summary>For refusals: "door limit 2 SCU", "largest rack takes 16 SCU".</summary>
    public string Text => Reason switch
    {
        CapReason.Door => Invariant($"door limit {(int)Size} SCU"),
        CapReason.LargestRack => Invariant($"largest rack takes {(int)Size} SCU"),
        CapReason.None => "no crate limit",
        _ => Invariant($"crates up to {(int)Size} SCU"),
    };

    /// <summary>For the overlay's notice while a derived mix is capped: "crates capped to 2 SCU (door limit)".</summary>
    public string Notice => Reason switch
    {
        CapReason.Door => Invariant($"crates capped to {(int)Size} SCU (door limit)"),
        CapReason.LargestRack => Invariant($"crates capped to {(int)Size} SCU (largest rack)"),
        _ => Invariant($"crates capped to {(int)Size} SCU"),
    };

    /// <summary>The contract's max crate size, or the cap where that is smaller.</summary>
    public CrateSize Limit(CrateSize max) => max < Size ? max : Size;

    // Every rack takes a 1 SCU crate (a grid has at least one cell), so the largest always exists.
    private static CrateSize Largest(Rack[] racks, Func<Rack, Func<CrateSize, bool>> takes) =>
        racks.SelectMany(r => Enum.GetValues<CrateSize>().Where(takes(r))).Max();
}
