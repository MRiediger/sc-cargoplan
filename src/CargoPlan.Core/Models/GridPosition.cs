using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A cell of a rack's grid, counted along length, width and stack from the corner at the plate; stack is the axis away
/// from the plate. Where that lies in the ship is <see cref="Rack.CellBox"/>'s call (D26): not always at
/// <see cref="Rack.Origin"/>, the rack's min corner in ship coordinates. A crate's position is its min corner (D24).
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
