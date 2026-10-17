using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>File format of a rack grid in cells (1 cell = 1 SCU).</summary>
internal sealed record GridDto(int Length, int Width, int Stack)
{
    public Dimensions ToDimensions(string rackId)
    {
        // Dimensions validates this too, but without naming the rack.
        (int Value, string Name)[] axes = [(Length, "length"), (Width, "width"), (Stack, "stack")];
        foreach (var (value, name) in axes)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    name, value, Invariant($"Rack '{rackId}' needs a positive grid {name}."));
            }
        }

        return new Dimensions(Length, Width, Stack);
    }
}
