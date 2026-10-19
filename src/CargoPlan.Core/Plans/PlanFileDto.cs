using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a plan file (schema version 1, docs/plan-format.md). <see cref="Missions"/> is a complete mission
/// file, so the editor reads it with the mission file's checks and it can be copied out as one. Kept as raw JSON, so
/// both mission schema versions are read inside (D23); writing always gives the current one.
/// </summary>
internal sealed record PlanFileDto(
    int SchemaVersion,
    string ShipId,
    JsonElement Missions,
    IReadOnlyList<PlacementDto> Placements,
    IReadOnlyList<PlanWarningDto> Warnings)
{
    /// <exception cref="JsonException">The missions are not a mission file of a supported schema version.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public SavedPlan ToSavedPlan(Ship ship)
    {
        var missions = ParseMissions();

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

        return new SavedPlan(missions, plan);
    }

    public static PlanFileDto FromSavedPlan(SavedPlan saved) => new(
        PlanFile.SupportedSchemaVersion,
        saved.Plan.ShipId,
        JsonSerializer.SerializeToElement(MissionFileDto.FromMissionStops(saved.Missions), MissionJsonContext.Default.MissionFileDto),
        [.. saved.Plan.Placements.Select(PlacementDto.FromPlacement)],
        [.. saved.Plan.Warnings.Select(PlanWarningDto.FromPlanWarning)]);

    private IReadOnlyList<MissionStop> ParseMissions()
    {
        try
        {
            return MissionFileReader.Parse(Missions);
        }
        catch (JsonException ex)
        {
            // The outer file is fine; say that the problem is in the embedded mission file.
            throw new JsonException($"missions: {ex.Message}", ex);
        }
    }
}
