using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a plan file (schema version 2, docs/plan-format.md): every crate with its rack and position (D24).
/// <see cref="Missions"/> is a complete mission file, so the editor reads it with the mission file's checks and it can be
/// copied out as one. Kept as raw JSON, so both mission schema versions are read inside (D23); writing always gives the
/// current one.
/// </summary>
internal sealed record PlanFileDto(
    int SchemaVersion,
    string ShipId,
    JsonElement Missions,
    string FillOrder,
    IReadOnlyList<CrateDto> Crates,
    IReadOnlyList<PlanWarningDto> Warnings)
{
    private const string BowFirst = "bowFirst";
    private const string SternFirst = "sternFirst";

    /// <exception cref="JsonException">The missions are not a mission file of a supported schema version.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public SavedPlan ToSavedPlan(Ship ship)
    {
        var missions = ParseMissions(Missions);
        LoadPlan plan = new(ShipId, [.. Crates.Select(c => c.ToCrate())], [.. Warnings.Select(w => w.ToPlanWarning())], ToFillOrder());

        return Checked(missions, plan, ship);
    }

    public static PlanFileDto FromSavedPlan(SavedPlan saved) => new(
        PlanFile.SupportedSchemaVersion,
        saved.Plan.ShipId,
        JsonSerializer.SerializeToElement(MissionFileDto.FromMissionStops(saved.Missions), MissionJsonContext.Default.MissionFileDto),
        saved.Plan.FillOrder == Models.FillOrder.BowFirst ? BowFirst : SternFirst,
        [.. saved.Plan.Crates.Select(CrateDto.FromCrate)],
        [.. saved.Plan.Warnings.Select(PlanWarningDto.FromPlanWarning)]);

    /// <summary>
    /// The checks both schema versions share: ship, racks, crates inside their grid, then the missions (D24). Overlap and
    /// crates without support load; <c>LoadPlanner.Validate</c> shows them red.
    /// </summary>
    /// <exception cref="ArgumentException">The plan doesn't fit the ship or its missions.</exception>
    internal static SavedPlan Checked(IReadOnlyList<MissionStop> missions, LoadPlan plan, Ship ship)
    {
        // Ship first: on another ship every rack would be "unknown", which hides the actual reason.
        plan.EnsureBelongsTo(ship);
        var racks = ship.Racks.ToDictionary(r => r.Id);
        if (plan.Crates.FirstOrDefault(c => !racks.ContainsKey(c.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Crate for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(Crates));
        }

        if (plan.Warnings.FirstOrDefault(w => !racks.ContainsKey(w.RackId)) is { } warning)
        {
            throw new ArgumentException(
                $"Warning \"{warning.Message}\" is for rack '{warning.RackId}', which ship '{ship.Id}' doesn't have.", nameof(Warnings));
        }

        // A crate outside its rack can't be drawn or moved: a file with one is broken, not red.
        if (plan.Crates.FirstOrDefault(c => !CrateLayout.Fits(racks[c.RackId], c)) is { } outside)
        {
            var rack = racks[outside.RackId];
            throw new ArgumentException(Invariant(
                $"{(int)outside.CrateSize} SCU crate of {outside.Commodity} for {outside.Destination} at {outside.Position} lies outside rack {rack.Number} ({rack.Grid})."),
                nameof(Crates));
        }

        return new SavedPlan(missions, plan);
    }

    /// <exception cref="JsonException">The missions are not a mission file of a supported schema version.</exception>
    internal static IReadOnlyList<MissionStop> ParseMissions(JsonElement missions)
    {
        try
        {
            return MissionFileReader.Parse(missions);
        }
        catch (JsonException ex)
        {
            // The outer file is fine; say that the problem is in the embedded mission file.
            throw new JsonException($"missions: {ex.Message}", ex);
        }
    }

    // A plain string and an explicit switch: STJ's enum converter would also accept numbers and other casing (as PositionDto).
    private Models.FillOrder ToFillOrder() => FillOrder switch
    {
        BowFirst => Models.FillOrder.BowFirst,
        SternFirst => Models.FillOrder.SternFirst,
        _ => throw new ArgumentException($"fillOrder '{FillOrder}' is unknown, use \"{BowFirst}\" or \"{SternFirst}\".", nameof(FillOrder)),
    };
}
