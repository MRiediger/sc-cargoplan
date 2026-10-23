using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>
/// The ships of a ships folder that loaded, sorted by id, and one problem per file that didn't, naming file and reason
/// (D56): a broken file costs only its own ship. A ship configuration is a catalog id (D59): selection, header,
/// switch and plan work with these entries, never with file names.
/// </summary>
public sealed record ShipCatalog(IReadOnlyList<Ship> Ships, IReadOnlyList<ShipProblem> Problems)
{
    /// <summary>The ship with this id, or null if it isn't loaded (case-sensitive).</summary>
    public Ship? Find(string shipId) => Ships.FirstOrDefault(s => s.Id == shipId);

    /// <exception cref="ShipNotFoundException">No loaded ship with this id (case-sensitive).</exception>
    public Ship Get(string shipId) =>
        Find(shipId) ?? throw new ShipNotFoundException(shipId, [.. Ships.Select(s => s.Id)]);
}
