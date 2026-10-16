using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>An amount of one commodity, packed in crates of a single size.</summary>
public sealed record CargoItem(string Commodity, int Scu, CrateSize CrateSize)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public int Scu { get; } = Validated(Scu, CrateSize);
    public CrateSize CrateSize { get; } = CrateSize;

    private static int Validated(int scu, CrateSize crateSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scu, nameof(Scu));
        if (!Enum.IsDefined(crateSize))
        {
            throw new ArgumentOutOfRangeException(nameof(CrateSize), crateSize, "Unknown crate size.");
        }

        if (scu % (int)crateSize != 0)
        {
            throw new ArgumentException(
                Invariant($"{scu} SCU can't be packed in {(int)crateSize} SCU crates."),
                nameof(Scu));
        }

        return scu;
    }
}
