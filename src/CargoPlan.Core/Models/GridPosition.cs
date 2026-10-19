using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A cell of a rack's grid, counted from the rack's min corner (<see cref="Rack.Origin"/>) along length, width and
/// stack (D14); stack is the axis away from the plate. A crate's position is its min corner (D24).
/// </summary>
public sealed record GridPosition(int Length, int Width, int Stack)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public int Length { get; } = NonNegative(Length, nameof(Length));
    public int Width { get; } = NonNegative(Width, nameof(Width));
    public int Stack { get; } = NonNegative(Stack, nameof(Stack));

    public override string ToString() => Invariant($"[{Length}, {Width}, {Stack}]");

    private static int NonNegative(int value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, name);
        return value;
    }
}
