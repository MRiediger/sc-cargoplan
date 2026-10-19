using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A separately loadable ship section. <see cref="Index"/> 0 is Rack 1, the first in rack order;
/// where the rack physically sits is <see cref="Position"/> in player words and <see cref="Origin"/> in ship coordinates (D14).
/// The grid must hold exactly <see cref="CapacityScu"/> cells. Crates stack along <see cref="StackAxis"/> (D26); the
/// default is the old drawing convention, so racks built in code stack up.
/// </summary>
public sealed record Rack(
    string Id, int Index, int CapacityScu, Dimensions Grid, RackPosition Position, ShipPoint Origin, StackAxis StackAxis = StackAxis.PlusZ)
{
    private const int X = 0, Y = 1, Z = 2;

    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    // Declared in this order so the capacity check runs before the grid check.
    public int Index { get; } = NonNegative(Index);
    public int CapacityScu { get; } = Positive(CapacityScu);
    public Dimensions Grid { get; } = Matching(Id, Grid, CapacityScu);

    /// <summary>Player-facing rack number: racks count from 1, <see cref="Index"/> stays 0-based (D6).</summary>
    public int Number => Index + 1;

    /// <summary>The space the rack takes in the ship, from <see cref="Origin"/>, its min corner, along the axes of <see cref="StackAxis"/>.</summary>
    public ShipBox Bounds => Box(new GridPosition(0, 0, 0), Grid);

    /// <summary>
    /// The box a crate of <paramref name="size"/> takes in the ship with its min grid corner at <paramref name="position"/>.
    /// A free cell is <see cref="CrateSize.One"/>. The only grid-to-ship mapping (D26).
    /// </summary>
    public ShipBox CellBox(GridPosition position, CrateSize size) => Box(position, size.Dimensions);

    /// <summary>
    /// The inverse of <see cref="CellBox"/>: the grid position of a crate that takes <paramref name="box"/>, or null if that
    /// cell lies outside the grid. A crate sticking out keeps its position; whether it fits is the layout's call (D24).
    /// </summary>
    public GridPosition? ToGrid(ShipBox box)
    {
        int[] origin = [Origin.X, Origin.Y, Origin.Z], min = [box.Min.X, box.Min.Y, box.Min.Z], max = [box.Max.X, box.Max.Y, box.Max.Z];
        int[] extent = [Grid.Length, Grid.Width, Grid.Stack];
        var frame = FrameOf(StackAxis);
        var position = new int[3];
        for (var axis = 0; axis < 3; axis++)
        {
            var (ship, negative) = frame[axis];
            position[axis] = negative ? origin[ship] + extent[axis] - max[ship] : min[ship] - origin[ship];
            if (position[axis] < 0 || position[axis] >= extent[axis])
            {
                return null;
            }
        }

        return new GridPosition(position[0], position[1], position[2]);
    }

    /// <summary>True if one crate of this size fits the grid in its fixed orientation (no turning).</summary>
    public bool CanHold(CrateSize size)
    {
        var crate = size.Dimensions;
        return crate.Length <= Grid.Length && crate.Width <= Grid.Width && crate.Stack <= Grid.Stack;
    }

    private ShipBox Box(GridPosition position, Dimensions size)
    {
        int[] origin = [Origin.X, Origin.Y, Origin.Z];
        int[] at = [position.Length, position.Width, position.Stack], along = [size.Length, size.Width, size.Stack];
        int[] extent = [Grid.Length, Grid.Width, Grid.Stack];
        var frame = FrameOf(StackAxis);
        int[] min = new int[3], max = new int[3];
        for (var axis = 0; axis < 3; axis++)
        {
            var (ship, negative) = frame[axis];

            // A negative axis counts from the rack's max side: stack 0 lies at the plate, wherever the plate is.
            min[ship] = origin[ship] + (negative ? extent[axis] - at[axis] - along[axis] : at[axis]);
            max[ship] = min[ship] + along[axis];
        }

        return new ShipBox(new ShipPoint(min[X], min[Y], min[Z]), new ShipPoint(max[X], max[Y], max[Z]));
    }

    /// <summary>
    /// The ship axis of length, width and stack, and whether it counts down (D26). Length lies along the bow where it
    /// can (on z when stacking along x), width takes the remaining axis. Negative stacks mirror the grid; boxes don't care.
    /// </summary>
    private static (int Ship, bool Negative)[] FrameOf(StackAxis axis) => axis switch
    {
        StackAxis.PlusX => [(Z, false), (Y, false), (X, false)],
        StackAxis.MinusX => [(Z, false), (Y, false), (X, true)],
        StackAxis.PlusY => [(X, false), (Z, false), (Y, false)],
        StackAxis.MinusY => [(X, false), (Z, false), (Y, true)],
        StackAxis.PlusZ => [(X, false), (Y, false), (Z, false)],
        StackAxis.MinusZ => [(X, false), (Y, false), (Z, true)],
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unknown stack axis."),
    };

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
