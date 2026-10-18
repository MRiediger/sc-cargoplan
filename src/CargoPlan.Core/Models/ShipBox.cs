using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>An axis-aligned box in ship coordinates (D14), from the <see cref="Min"/> corner to the <see cref="Max"/> corner.</summary>
public sealed record ShipBox(ShipPoint Min, ShipPoint Max)
{
    // Both get-only: a `with` on either corner must not bypass the check.
    public ShipPoint Min { get; } = Min;
    public ShipPoint Max { get; } = AtLeast(Max, Min);

    /// <summary>True if both boxes share volume. Boxes that only touch at a face, edge or corner don't overlap.</summary>
    public bool Overlaps(ShipBox other) =>
        Min.X < other.Max.X && other.Min.X < Max.X
        && Min.Y < other.Max.Y && other.Min.Y < Max.Y
        && Min.Z < other.Max.Z && other.Min.Z < Max.Z;

    /// <summary>The smallest box that encloses both boxes.</summary>
    public ShipBox Union(ShipBox other) => new(
        new ShipPoint(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
        new ShipPoint(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));

    private static ShipPoint AtLeast(ShipPoint max, ShipPoint min) =>
        max.X >= min.X && max.Y >= min.Y && max.Z >= min.Z
            ? max
            : throw new ArgumentException(Invariant($"Max {max} lies below Min {min} on at least one axis."), nameof(Max));
}
