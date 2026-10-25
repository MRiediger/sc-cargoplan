using System.Text.Json;

namespace CargoPlan.Core.Plans;

/// <summary>
/// Plan file schema 2 (crates with positions, missions as an embedded mission file, D24): read, never written. Each stop
/// becomes a mission of its own and its crates belong to it, the watermark is 0 (D42).
/// </summary>
internal sealed record PlanFileV2Dto(
    int SchemaVersion,
    string ShipId,
    JsonElement Missions,
    string FillOrder,
    IReadOnlyList<CrateDto> Crates,
    IReadOnlyList<PlanWarningDto> Warnings)
{
    /// <exception cref="JsonException">The missions are not a mission file of a supported schema version.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public SavedPlan ToSavedPlan(Models.Ship ship)
    {
        var stops = PlanFileDto.ParseMissions(Missions);
        PlanFileDto.CheckItems(Crates, SchemaVersion);
        PlanFileDto.CheckRotated(Crates, SchemaVersion);
        Models.LoadPlan plan = new(
            ShipId, [.. Crates.Select(c => c.ToCrate())], [.. Warnings.Select(w => w.ToPlanWarning())], PlanFileDto.ParseFillOrder(FillOrder));

        var (checkedPlan, racksChanged) = PlanFileDto.Checked(plan, ship);
        return PlanFileDto.UnderCap(ship, cap => SavedPlan.FromStops(stops, checkedPlan, cap), racksChanged);
    }
}
