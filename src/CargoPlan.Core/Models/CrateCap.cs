using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>Why a ship takes no crate above its <see cref="CrateCap.Max"/> (D70).</summary>
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

    /// <summary>
    /// Read from a plan file with a crate outside today's rack: the racks changed since it was written (K3.2, a ship data
    /// update); the overlay plans it anew. Not a cap of its own: the sizes are those its crates fit.
    /// </summary>
    RacksChanged,
}

/// <summary>
/// The crate sizes a ship takes in some rack (<see cref="Rack.CanHold"/>), and why none above <see cref="Max"/> (D70).
/// With minimums (K3.3) the sizes need not be one range: racks of 32s only next to racks of 2s at most take no 8, so
/// <see cref="Accepts"/> decides, <see cref="Min"/> and <see cref="Max"/> are for the texts. A mix the planner derives is
/// split into accepted sizes (<see cref="CrateMix.Decompose(int, CrateSize, CrateCap)"/>), a mix the player set is not.
/// The one place for the texts that name a door, a rack limit or a minimum.
/// </summary>
public sealed record CrateCap
{
    private static readonly CrateSize[] Sizes = Enum.GetValues<CrateSize>();

    // One bit per size of Sizes: a plain value, so record equality compares the sizes, the minimum included (K2).
    private readonly int _accepted;

    /// <summary>Every crate up to <paramref name="max"/>.</summary>
    public CrateCap(CrateSize max, CapReason reason)
        : this(Mask(size => size <= max), reason)
    {
    }

    private CrateCap(int accepted, CapReason reason)
    {
        _accepted = accepted;
        Reason = reason;
    }

    /// <summary>No cap: the contract alone decides the mix, as before D70.</summary>
    public static CrateCap None { get; } = new(CrateSize.ThirtyTwo, CapReason.None);

    /// <summary>Why no crate above <see cref="Max"/>; a minimum has no reason of its own, its text names it.</summary>
    public CapReason Reason { get; }

    /// <summary>The largest crate some rack takes.</summary>
    public CrateSize Max => Sizes.Where(Accepts).Max();

    /// <summary>The smallest crate some rack takes (Martin, K3.3): below it a crate fits no rack.</summary>
    public CrateSize Min => Sizes.Where(Accepts).Min();

    public static CrateCap Of(Ship ship) => Of(ship.Racks);

    public static CrateCap Of(Rack rack) => Of([rack]);

    /// <summary>The sizes any of <paramref name="racks"/> takes; a door if some rack's grid would take more than the largest.</summary>
    public static CrateCap Of(IEnumerable<Rack> racks)
    {
        Rack[] all = [.. racks];
        var cap = new CrateCap(Mask(size => all.Any(r => r.CanHold(size))), CapReason.None);
        var reason = all.SelectMany(r => Sizes.Where(r.FitsGrid)).Max() > cap.Max ? CapReason.Door
            : cap.Max == CrateSize.ThirtyTwo ? CapReason.None
            : CapReason.LargestRack;
        return new CrateCap(cap._accepted, reason);
    }

    /// <summary>The same sizes for another reason.</summary>
    public CrateCap Because(CapReason reason) => new(_accepted, reason);

    /// <summary>True if some rack takes a crate of this size.</summary>
    public bool Accepts(CrateSize size) => (_accepted & Bit(size)) != 0;

    /// <summary>The limit above: "door limit 2 SCU", "largest rack takes 16 SCU".</summary>
    public string Text => Reason switch
    {
        CapReason.Door => Invariant($"door limit {(int)Max} SCU"),
        CapReason.LargestRack => Invariant($"largest rack takes {(int)Max} SCU"),
        CapReason.None => "no crate limit",
        _ => Invariant($"crates up to {(int)Max} SCU"),
    };

    /// <summary>For the overlay's notice while a derived mix is capped: "crates capped to 2 SCU (door limit)".</summary>
    public string Notice => Reason switch
    {
        CapReason.Door => Invariant($"crates capped to {(int)Max} SCU (door limit)"),
        CapReason.LargestRack => Invariant($"crates capped to {(int)Max} SCU (largest rack)"),
        _ => Invariant($"crates capped to {(int)Max} SCU"),
    };

    /// <summary>Why a rack refuses <paramref name="size"/>: "door limit 2 SCU", "takes 32 SCU crates only".</summary>
    public string RackText(CrateSize size) =>
        size > Max ? Text
        : size < Min ? (Min == Max ? Invariant($"takes {(int)Min} SCU crates only") : Invariant($"takes crates from {(int)Min} SCU"))
        : Invariant($"takes no {(int)size} SCU crates");

    /// <summary>Why the ship refuses <paramref name="size"/>: "door limit 2 SCU", "ship takes 32 SCU crates only".</summary>
    public string ShipText(CrateSize size) => size > Max ? Text : "ship " + RackText(size);

    /// <summary>For a mission that waits: "no rack accepts 4 SCU crates, door limit 2 SCU", "ship takes 32 SCU crates only".</summary>
    public string Refusal(CrateSize size) => size > Max ? Invariant($"no rack accepts {(int)size} SCU crates, {Text}") : ShipText(size);

    /// <summary>The contract's max crate size, or <see cref="Max"/> where that is smaller.</summary>
    public CrateSize Limit(CrateSize max) => max < Max ? max : Max;

    private static int Bit(CrateSize size) => 1 << Array.IndexOf(Sizes, size);

    private static int Mask(Func<CrateSize, bool> takes) => Sizes.Where(takes).Aggregate(0, (mask, size) => mask | Bit(size));
}
