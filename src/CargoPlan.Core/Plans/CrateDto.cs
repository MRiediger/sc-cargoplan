using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a crate (plan schema 2 to 7, D24): what, for whom, which rack, which cell; from schema 3 which mission
/// (D42), from schema 4 which item of it (D68), from schema 5 whether it is turned (D71), from schema 6 whether it is
/// off the ship (K2.2: delivered, never on this ship, <c>rackId</c> empty, position [0, 0, 0]), from schema 7 whether it
/// is loaded (K6.4: on board and fixed, never delivered); the last three written only where true. Schema 2 has no mission: 0, assigned by destination on reading; schema 2 and 3 have no item, assigned on
/// reading (<c>SavedPlan.WithItems</c>).
/// </summary>
internal sealed record CrateDto(
    string Destination, string Commodity, int CrateSize, string RackId, IReadOnlyList<int> Position, bool Delivered, int Mission = 0, int? Item = null,
    bool? Rotated = null, bool? OffShip = null, bool? Loaded = null)
{
    private const int PositionAxes = 3;

    public Crate ToCrate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Destination, nameof(Destination));
        if (string.IsNullOrWhiteSpace(Commodity))
        {
            throw new ArgumentException($"A crate for '{Destination}' has no commodity.", nameof(Commodity));
        }

        // Plain int in the DTO, because STJ would accept any number for an enum (e.g. 3), as in CargoItemDto.
        // "Models." because the property CrateSize hides the enum type here.
        if (!Enum.IsDefined((Models.CrateSize)CrateSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(CrateSize), CrateSize, Invariant($"Crate for '{Destination}', {Commodity}: {CrateSize} SCU is not a crate size."));
        }

        // STJ already rejects null and non-integers; length and sign are left.
        if (Position is not [var length, var width, var stack] || length < 0 || width < 0 || stack < 0)
        {
            throw new ArgumentException(Invariant(
                $"Crate for '{Destination}', {Commodity}: position needs {PositionAxes} values [length, width, stack] of 0 or more, got [{string.Join(", ", Position)}]."),
                nameof(Position));
        }

        // Loaded is on board and fixed (K6.4): never delivered, never off the ship.
        if (Loaded == true && OffShip == true)
        {
            throw new ArgumentException($"A crate for '{Destination}', {Commodity} is loaded but off the ship.", nameof(Loaded));
        }

        if (Loaded == true && Delivered)
        {
            throw new ArgumentException($"A crate for '{Destination}', {Commodity} is loaded and delivered.", nameof(Loaded));
        }

        // The flag says it, the empty rack is how the model holds it (Crate.NoRack): both or neither, and only delivered.
        if (OffShip == true && !Delivered)
        {
            throw new ArgumentException($"A crate for '{Destination}', {Commodity} is off the ship but not delivered.", nameof(OffShip));
        }

        if ((OffShip == true) != (RackId == Crate.NoRack))
        {
            throw new ArgumentException(OffShip == true
                ? $"A crate for '{Destination}', {Commodity} is off the ship but names rack '{RackId}'."
                : $"A crate for '{Destination}', {Commodity} names no rack.", nameof(RackId));
        }

        return new Crate(Destination, Commodity, (Models.CrateSize)CrateSize, RackId, new GridPosition(length, width, stack), Delivered, Mission, Item ?? 0, Rotated ?? false, Loaded ?? false);
    }

    // Null, so not written, where the crate lies unturned, on the ship and not loaded: most crates do.
    public static CrateDto FromCrate(Crate crate) => new(
        crate.Destination, crate.Commodity, (int)crate.CrateSize, crate.RackId,
        [crate.Position.Length, crate.Position.Width, crate.Position.Stack], crate.Delivered, crate.Mission, crate.Item, crate.Rotated ? true : null,
        crate.OffShip ? true : null, crate.Loaded ? true : null);
}
