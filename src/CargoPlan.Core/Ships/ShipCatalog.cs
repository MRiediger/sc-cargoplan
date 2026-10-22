using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>
/// The ships of a ships folder that loaded, sorted by id, and one line per file that didn't, naming file and reason
/// (D56): a broken file costs only its own ship.
/// </summary>
public sealed record ShipCatalog(IReadOnlyList<Ship> Ships, IReadOnlyList<string> Problems)
{
    /// <exception cref="ShipNotFoundException">No loaded ship with this id (case-sensitive).</exception>
    public Ship Get(string shipId) =>
        Ships.FirstOrDefault(s => s.Id == shipId) ?? throw new ShipNotFoundException(shipId, [.. Ships.Select(s => s.Id)]);
}
