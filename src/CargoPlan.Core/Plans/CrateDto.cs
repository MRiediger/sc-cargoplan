using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a crate (plan schema 2 to 4, D24): what, for whom, which rack, which cell; from schema 3 which mission
/// (D42), from schema 4 which item of it (D68). Schema 2 has no mission: 0, assigned by destination on reading; schema 2
/// and 3 have no item, assigned on reading (<c>SavedPlan.WithItems</c>).
/// </summary>
internal sealed record CrateDto(
    string Destination, string Commodity, int CrateSize, string RackId, IReadOnlyList<int> Position, bool Delivered, int Mission = 0, int? Item = null)
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

        return new Crate(Destination, Commodity, (Models.CrateSize)CrateSize, RackId, new GridPosition(length, width, stack), Delivered, Mission, Item ?? 0);
    }

    public static CrateDto FromCrate(Crate crate) => new(
        crate.Destination, crate.Commodity, (int)crate.CrateSize, crate.RackId,
        [crate.Position.Length, crate.Position.Width, crate.Position.Stack], crate.Delivered, crate.Mission, crate.Item);
}
