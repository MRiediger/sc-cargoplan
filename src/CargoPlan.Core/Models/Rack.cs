using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A separately loadable ship section. <see cref="Index"/> 0 is Rack 1, the first in rack order;
/// where the rack physically sits is <see cref="Position"/> in player words and <see cref="Origin"/> in ship coordinates (D14).
/// The grid must hold exactly <see cref="CapacityScu"/> cells.
/// </summary>
public sealed record Rack(string Id, int Index, int CapacityScu, Dimensions Grid, RackPosition Position, ShipPoint Origin)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    // Declared in this order so the capacity check runs before the grid check.
    public int Index { get; } = NonNegative(Index);
    public int CapacityScu { get; } = Positive(CapacityScu);
    public Dimensions Grid { get; } = Matching(Id, Grid, CapacityScu);

    /// <summary>Player-facing rack number: racks count from 1, <see cref="Index"/> stays 0-based (D6).</summary>
    public int Number => Index + 1;

    /// <summary>The space the rack takes in the ship: grid length, width and stack along x, y and z from <see cref="Origin"/>.</summary>
    public ShipBox Bounds => new(Origin, new ShipPoint(Origin.X + Grid.Length, Origin.Y + Grid.Width, Origin.Z + Grid.Stack));

    /// <summary>True if one crate of this size fits the grid in its fixed orientation (no turning).</summary>
    public bool CanHold(CrateSize size)
    {
        var crate = size.Dimensions;
        return crate.Length <= Grid.Length && crate.Width <= Grid.Width && crate.Stack <= Grid.Stack;
    }

    private static int NonNegative(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return value;
    }

    private static int Positive(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return value;
    }

    private static Dimensions Matching(string id, Dimensions grid, int capacityScu) =>
        grid.Volume == capacityScu
            ? grid
            : throw new ArgumentException(
                Invariant($"Rack '{id}': grid {grid} holds {grid.Volume} SCU, but capacityScu is {capacityScu}."),
                nameof(Grid));
}
