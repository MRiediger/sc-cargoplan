using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>
/// File format of a contract item (schema version 2, D23). Crate sizes as SCU numbers; <see cref="Crates"/> is the
/// optional override of the derived mix and left out when writing an item without one, as is <see cref="Pickup"/>
/// (D68, optional, so no schema bump).
/// </summary>
internal sealed record MissionItemDto(string Commodity, int Scu, int MaxCrateSize, IReadOnlyList<int>? Crates = null, IReadOnlyList<string>? Pickup = null)
{
    public MissionItem ToMissionItem(string destination)
    {
        if (string.IsNullOrWhiteSpace(Commodity))
        {
            throw new ArgumentException($"Stop '{destination}' has an item without commodity.", nameof(Commodity));
        }

        // Plain ints in the DTO, because STJ would accept any number for an enum (e.g. 3).
        if (!Enum.IsDefined((CrateSize)MaxCrateSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxCrateSize), MaxCrateSize, Invariant($"Stop '{destination}', {Commodity}: {MaxCrateSize} SCU is not a crate size."));
        }

        // Checked on the numbers first, so "3 SCU is not a crate size" names the 3 instead of an enum cast.
        if (Crates is not null && Scu > 0 && CrateMix.Problem(Scu, (CrateSize)MaxCrateSize, Crates) is { } problem)
        {
            throw new ArgumentException($"Stop '{destination}', {Commodity}: {problem}", nameof(Crates));
        }

        try
        {
            // Hand-written files may spell the places loosely; the item takes them as the editor does (A6).
            return new MissionItem(Commodity, Scu, (CrateSize)MaxCrateSize, Crates?.Select(c => (CrateSize)c).ToList(), Pickups.Normalized(Pickup));
        }
        catch (ArgumentException ex)
        {
            // MissionItem doesn't know its stop; add it so the user finds the line in the file.
            throw new ArgumentException($"Stop '{destination}', {Commodity}: {ex.Message}", ex);
        }
    }

    public static MissionItemDto FromMissionItem(MissionItem item) =>
        new(item.Commodity, item.Scu, (int)item.MaxCrateSize, item.Crates?.Select(c => (int)c).ToList(), item.Pickup);
}
