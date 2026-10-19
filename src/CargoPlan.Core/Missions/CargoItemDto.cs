using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>
/// File format of a cargo item of one crate size, as an SCU number (1, 2, 4, 8): a placement of a schema 1 plan file, and an
/// item of a version 1 mission file.
/// </summary>
internal sealed record CargoItemDto(string Commodity, int Scu, int CrateSize)
{
    public CargoItem ToCargoItem(string destination)
    {
        if (string.IsNullOrWhiteSpace(Commodity))
        {
            throw new ArgumentException($"Stop '{destination}' has an item without commodity.", nameof(Commodity));
        }

        // Plain int in the DTO, because STJ would accept any number for an enum (e.g. 3).
        // "Models." because the property CrateSize hides the enum type here.
        if (!Enum.IsDefined((Models.CrateSize)CrateSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(CrateSize), CrateSize, Invariant($"Stop '{destination}', {Commodity}: {CrateSize} SCU is not a crate size."));
        }

        try
        {
            return new CargoItem(Commodity, Scu, (Models.CrateSize)CrateSize);
        }
        catch (ArgumentException ex)
        {
            // CargoItem doesn't know its stop; add it so the user finds the line in the file.
            throw new ArgumentException($"Stop '{destination}', {Commodity}: {ex.Message}", ex);
        }
    }
}
