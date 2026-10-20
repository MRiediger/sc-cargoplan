using System.Text.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a plan file (schema version 3, docs/plan-format.md): the missions with their stops and where they came
/// from (D42), every crate with its mission, rack and position (D24), and the ContractTracker watermark (D39 addendum).
/// </summary>
internal sealed record PlanFileDto(
    int SchemaVersion,
    string ShipId,
    string FillOrder,
    long ContractTrackerWatermark,
    IReadOnlyList<MissionDto> Missions,
    IReadOnlyList<CrateDto> Crates,
    IReadOnlyList<PlanWarningDto> Warnings)
{
    private const string BowFirst = "bowFirst";
    private const string SternFirst = "sternFirst";

    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public SavedPlan ToSavedPlan(Ship ship)
    {
        Mission[] missions = [.. Missions.Select(m => m.ToMission())];
        LoadPlan plan = new(ShipId, [.. Crates.Select(c => c.ToCrate())], [.. Warnings.Select(w => w.ToPlanWarning())], ParseFillOrder(FillOrder));

        return new SavedPlan(missions, Checked(plan, ship), ContractTrackerWatermark);
    }

    public static PlanFileDto FromSavedPlan(SavedPlan saved) => new(
        PlanFile.SupportedSchemaVersion,
        saved.Plan.ShipId,
        saved.Plan.FillOrder == Models.FillOrder.BowFirst ? BowFirst : SternFirst,
        saved.Watermark,
        [.. saved.Missions.Select(MissionDto.FromMission)],
        [.. saved.Plan.Crates.Select(CrateDto.FromCrate)],
        [.. saved.Plan.Warnings.Select(PlanWarningDto.FromPlanWarning)]);

    /// <summary>
    /// The checks every schema version shares: ship, racks, crates inside their grid (D24). Overlap and crates without
    /// support load; <c>LoadPlanner.Validate</c> shows them red. The missions are the caller's, via <see cref="SavedPlan"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The plan doesn't fit the ship.</exception>
    internal static LoadPlan Checked(LoadPlan plan, Ship ship)
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

        return plan;
    }

    /// <summary>The embedded mission file of schema 1 and 2.</summary>
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
    internal static Models.FillOrder ParseFillOrder(string fillOrder) => fillOrder switch
    {
        BowFirst => Models.FillOrder.BowFirst,
        SternFirst => Models.FillOrder.SternFirst,
        _ => throw new ArgumentException($"fillOrder '{fillOrder}' is unknown, use \"{BowFirst}\" or \"{SternFirst}\".", nameof(FillOrder)),
    };
}
