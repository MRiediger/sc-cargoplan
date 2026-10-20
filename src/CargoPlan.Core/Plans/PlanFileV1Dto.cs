using System.Text.Json;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.Core.Plans;

/// <summary>
/// Plan file schema 1 (placements, no crate positions): read, never written. Its placements are laid out the way the
/// planner lays out a fresh plan, so an upgrade keeps the plan of a running route (D24); one mission per stop (D42).
/// </summary>
internal sealed record PlanFileV1Dto(
    int SchemaVersion,
    string ShipId,
    JsonElement Missions,
    IReadOnlyList<PlacementDto> Placements,
    IReadOnlyList<PlanWarningDto> Warnings)
{
    /// <exception cref="JsonException">The missions are not a mission file of a supported schema version.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    /// <exception cref="PlanException">A rack holds more than its grid (an overload, allowed in schema 1, D10).</exception>
    public SavedPlan ToSavedPlan(Ship ship)
    {
        var missions = PlanFileDto.ParseMissions(Missions);
        Placement[] placements = [.. Placements.Select(p => p.ToPlacement())];

        // Ship first: on another ship every rack would be "unknown", which hides the actual reason.
        new LoadPlan(ShipId, [], []).EnsureBelongsTo(ship);
        var crates = CrateLayout.LayOut(ship, placements, FillOrder.BowFirst);

        return SavedPlan.FromStops(missions, PlanFileDto.Checked(new LoadPlan(ShipId, crates, [.. Warnings.Select(w => w.ToPlanWarning())], FillOrder.BowFirst), ship));
    }
}
