namespace CargoPlan.Core.Models;

/// <summary>
/// A separately loadable ship section. <see cref="Index"/> 0 is the front.
/// No slots means the rack is planned by capacity only.
/// </summary>
public sealed record Rack(string Id, int Index, int CapacityScu, IReadOnlyList<Slot> Slots)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public int Index { get; } = NonNegative(Index);
    public int CapacityScu { get; } = Positive(CapacityScu);

    /// <summary>Player-facing rack number: racks count from 1, <see cref="Index"/> stays 0-based (D6).</summary>
    public int Number => Index + 1;

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
}
