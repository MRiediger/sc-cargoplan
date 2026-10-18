using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a plan file (schema version 1, docs/plan-format.md). <see cref="Missions"/> is a complete mission
/// file, so the editor reads it with the mission file's checks and it can be copied out as one.
/// </summary>
internal sealed record PlanFileDto(
    int SchemaVersion,
    string ShipId,
    MissionFileDto Missions,
    IReadOnlyList<PlacementDto> Placements,
    IReadOnlyList<PlanWarningDto> Warnings)
{
    /// <exception cref="JsonException">The missions have an unsupported schema version.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public SavedPlan ToSavedPlan(Ship ship)
    {
        // The outer version is checked before deserializing; the inner one only exists as a field by now.
        if (Missions.SchemaVersion != MissionFileReader.SupportedSchemaVersion)
        {
            throw new JsonException(Invariant(
                $"missions: schemaVersion {Missions.SchemaVersion} is not supported (expected {MissionFileReader.SupportedSchemaVersion})."));
        }

        LoadPlan plan = new(ShipId, [.. Placements.Select(p => p.ToPlacement())], [.. Warnings.Select(w => w.ToPlanWarning())]);

        // Ship first: on another ship every rack would be "unknown", which hides the actual reason.
        plan.EnsureBelongsTo(ship);
        HashSet<string> rackIds = [.. ship.Racks.Select(r => r.Id)];
        if (plan.Placements.FirstOrDefault(p => !rackIds.Contains(p.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Placement for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(Placements));
        }

        if (plan.Warnings.FirstOrDefault(w => !rackIds.Contains(w.RackId)) is { } warning)
        {
            throw new ArgumentException(
                $"Warning \"{warning.Message}\" is for rack '{warning.RackId}', which ship '{ship.Id}' doesn't have.", nameof(Warnings));
        }

        return new SavedPlan(Missions.ToStops(), plan);
    }

    public static PlanFileDto FromSavedPlan(SavedPlan saved) => new(
        PlanFile.SupportedSchemaVersion,
        saved.Plan.ShipId,
        MissionFileDto.FromStops(saved.Missions),
        [.. saved.Plan.Placements.Select(PlacementDto.FromPlacement)],
        [.. saved.Plan.Warnings.Select(PlanWarningDto.FromPlanWarning)]);
}
