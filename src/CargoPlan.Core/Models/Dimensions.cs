using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A box measured in cells (1 cell = 1 SCU = 1.25 m): <see cref="Length"/> along the rack's plate,
/// <see cref="Width"/> across it, <see cref="Stack"/> away from it. Used for rack grids and crates.
/// For drawing, a rack grid maps Length/Width/Stack to the ship's x/y/z (D14); the physical stacking direction is not modelled.
/// </summary>
public sealed record Dimensions(int Length, int Width, int Stack)
{
    public int Length { get; } = Positive(Length, nameof(Length));
    public int Width { get; } = Positive(Width, nameof(Width));
    public int Stack { get; } = Positive(Stack, nameof(Stack));

    public int Volume => Length * Width * Stack;

    public override string ToString() => Invariant($"{Length}×{Width}×{Stack}");

    private static int Positive(int value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, name);
        return value;
    }
}
