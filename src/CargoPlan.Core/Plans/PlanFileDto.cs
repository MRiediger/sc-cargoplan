using System.Text.Json;
using System.Text.Json.Serialization;
using CargoPlan.Core.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a plan file (schema version 10, docs/plan-format.md): the fill order and row axis (D88), the grouping and
/// section gap (D89), the last scan number (D102), the missions with their stops and the scan they came from (D42, D102),
/// every crate with its mission and item (D68), rack, position (D24), whether it is turned (D71), whether it is off the
/// ship (K2.2) and whether it is loaded (K6.4). Schema 9 has no scans, schema 8 no row axis either, every plan lengthwise;
/// their ContractTracker captures are read as scans (D107). Older schemas are refused (F4, T10.2b1).
/// </summary>
/// <remarks><see cref="FillRows"/>, <see cref="Grouping"/> and <see cref="SectionGap"/> are optional for older files, so they stand last; the order attributes write them behind the fill order, as <see cref="Missions.MissionFileDto"/> does.
/// <see cref="ContractTrackerWatermark"/> is read from older files and dropped, never written (D107).</remarks>
internal sealed record PlanFileDto(
    int SchemaVersion,
    string ShipId,
    string FillOrder,
    [property: JsonPropertyOrder(1)] IReadOnlyList<MissionDto> Missions,
    [property: JsonPropertyOrder(1)] IReadOnlyList<CrateDto> Crates,
    [property: JsonPropertyOrder(1)] IReadOnlyList<PlanWarningDto> Warnings,
    string? FillRows = null,
    string? Grouping = null,
    int? SectionGap = null,
    int? LastScanNo = null,
    long? ContractTrackerWatermark = null)
{
    /// <summary>
    /// The plan, and the scan numbers an older file's ContractTracker captures got (D107): the next ones above the file's
    /// last number and its highest scan, in file order; empty for a file without captures.
    /// </summary>
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public (SavedPlan Plan, IReadOnlyList<int> FromContractTracker) ToSavedPlan(Ship ship)
    {
        CheckFillRows(FillRows, Grouping, SectionGap, SchemaVersion);
        CheckScans(Missions, LastScanNo, SchemaVersion);
        var next = Math.Max(LastScanNo ?? 0, Missions.Select(m => m.Contract?.Scan?.No ?? 0).DefaultIfEmpty().Max()) + 1;
        List<int> fromContractTracker = [];
        List<Mission> missions = [];
        foreach (var mission in Missions)
        {
            // An older file's ContractTracker capture has no scan of its own (D107).
            var legacyNo = mission.Contract is { Scan: null } ? next++ : 0;
            if (legacyNo > 0)
            {
                fromContractTracker.Add(legacyNo);
            }

            missions.Add(mission.ToMission(legacyNo));
        }

        CheckItems(Crates, SchemaVersion);
        Crate[] crates = [.. Crates.Select(c => c.ToCrate())];
        var (plan, racksChanged) = Checked(new LoadPlan(ShipId, crates, [.. Warnings.Select(w => w.ToPlanWarning())], FillOrderJson.Parse(FillOrder), FillRows is null ? Models.FillRows.Lengthwise : FillRowsJson.Parse(FillRows), Grouping is null ? PlanGrouping.None : GroupingJson.Parse(Grouping), SectionGap ?? 0), ship);

        return (UnderCap(ship, cap => new SavedPlan(missions, plan, cap), racksChanged), fromContractTracker);
    }

    /// <summary>
    /// The saved plan under the ship's cap (D70). A file whose derived mixes were split under another cap, or with a crate
    /// on board the ship's racks don't take (saved before D70 or K3.3, or on another ship), gets the largest cap its crates
    /// fit, as <see cref="CapReason.Saved"/>, and the overlay plans it anew (<c>LoadPlanner.Replan</c>). The cap isn't
    /// stored: the file stays what it was. Without such a cap it is read under the ship's own, its crates shown red.
    /// With <paramref name="racksChanged"/> (a crate outside today's rack, K3.2) every cap tried says
    /// <see cref="CapReason.RacksChanged"/>, so the overlay plans the file anew whichever sizes it fits.
    /// </summary>
    /// <exception cref="ArgumentException">The plan doesn't fit its missions under any cap; the ship's reason is named.</exception>
    internal static SavedPlan UnderCap(Ship ship, Func<CrateCap, SavedPlan> make, bool racksChanged = false)
    {
        var own = CrateCap.Of(ship);
        CrateCap[] all = [own, .. Enum.GetValues<CrateSize>().OrderDescending().Select(s => new CrateCap(s, CapReason.Saved)).Where(c => c != own)];
        CrateCap[] caps = racksChanged ? [.. all.Select(c => c.Because(CapReason.RacksChanged)).Distinct()] : all;
        ArgumentException? first = null;
        SavedPlan? refused = null;
        foreach (var cap in caps)
        {
            try
            {
                var saved = make(cap);
                if (saved.Plan.Crates.All(c => c.Delivered || cap.Accepts(c.CrateSize)))
                {
                    return saved;
                }

                // A crate on board the ship's racks don't take (a minimum, K3.3): kept only for when no other cap fits.
                refused ??= saved;
            }
            catch (ArgumentException ex)
            {
                // Another cap may fit; if none does, the ship's own reason is the one to name.
                first ??= ex;
            }
        }

        return refused ?? throw first!;
    }

    /// <param name="lastScanNo">The last scan number given (D102); written as at least the plan's highest, and only above 0.</param>
    public static PlanFileDto FromSavedPlan(SavedPlan saved, int lastScanNo = 0) => new(
        PlanFile.SupportedSchemaVersion,
        saved.Plan.ShipId,
        FillOrderJson.Text(saved.Plan.FillOrder),
        [.. saved.Missions.Select(MissionDto.FromMission)],
        [.. saved.Plan.Crates.Select(CrateDto.FromCrate)],
        [.. saved.Plan.Warnings.Select(PlanWarningDto.FromPlanWarning)],
        FillRowsJson.Text(saved.Plan.FillRows),
        GroupingJson.Text(saved.Plan.Grouping),
        saved.Plan.SectionGap,
        Math.Max(lastScanNo, saved.HighestScanNo) is > 0 and var last ? last : null);

    /// <summary>
    /// The checks every schema version shares: racks (D24). Overlap and crates without support load;
    /// <c>LoadPlanner.Validate</c> shows them red. The hints are derived anew from the crates, positions and ticks stay
    /// (D64). The missions are the caller's, via <see cref="SavedPlan"/>.
    /// A plan for another ship skips the grid: its racks are another ship's, and the overlay plans it anew from its
    /// missions (<c>LoadPlanner.Replan</c>, D59). A crate outside its rack means the racks changed since the file was
    /// written (K3.2: the turned racks went back to Fleetyards' orientation; any ship data update can do it), whatever the
    /// schema: <paramref name="plan"/> as it is with <c>RacksChanged</c>, planned anew like another ship's.
    /// </summary>
    /// <exception cref="ArgumentException">The plan names a rack the ship doesn't have.</exception>
    internal static (LoadPlan Plan, bool RacksChanged) Checked(LoadPlan plan, Ship ship)
    {
        if (plan.ShipId != ship.Id)
        {
            return (plan, false);
        }

        // An off-ship crate (K2.2) was never on this ship: no rack to name, no grid to fit.
        var racks = ship.Racks.ToDictionary(r => r.Id);
        if (plan.Crates.FirstOrDefault(c => !c.OffShip && !racks.ContainsKey(c.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Crate for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(Crates));
        }

        if (plan.Warnings.FirstOrDefault(w => !racks.ContainsKey(w.RackId)) is { } warning)
        {
            throw new ArgumentException(
                $"Warning \"{warning.Message}\" is for rack '{warning.RackId}', which ship '{ship.Id}' doesn't have.", nameof(Warnings));
        }

        // A crate outside its rack can't be drawn or moved; its racks changed, so the overlay plans the missions anew.
        if (plan.Crates.Any(c => !c.OffShip && !CrateLayout.Fits(racks[c.RackId], c)))
        {
            return (plan, true);
        }

        // Hints are derived anew: a plan from before D64 loses its route notes and shows what sits on what.
        return (LoadPlanner.WithHints(plan, ship), false);
    }

    /// <summary>
    /// From schema 9 on the file names its row axis; before it every plan is lengthwise and none does (D88). So with the
    /// grouping and section gap (D89), but a schema 9 file without them means none: builds of K9.1 and K9.2 wrote those.
    /// </summary>
    /// <exception cref="ArgumentException">The file breaks that.</exception>
    internal static void CheckFillRows(string? fillRows, string? grouping, int? sectionGap, int schemaVersion)
    {
        const int withFillRows = PlanFile.NoFillRowsSchemaVersion + 1;
        if (schemaVersion < withFillRows && grouping is not null)
        {
            throw new ArgumentException(Invariant($"grouping is new in schema {withFillRows} (the file says {schemaVersion})."), nameof(Grouping));
        }

        if (schemaVersion < withFillRows && sectionGap is not null)
        {
            throw new ArgumentException(Invariant($"sectionGap is new in schema {withFillRows} (the file says {schemaVersion})."), nameof(SectionGap));
        }

        if (schemaVersion < withFillRows && fillRows is not null)
        {
            throw new ArgumentException(Invariant($"fillRows is new in schema {withFillRows} (the file says {schemaVersion})."), nameof(FillRows));
        }

        if (schemaVersion >= withFillRows && fillRows is null)
        {
            throw new ArgumentException(Invariant($"The file has no fillRows, which schema {schemaVersion} requires."), nameof(FillRows));
        }
    }

    /// <summary>
    /// From schema 10 on a contract may be a scan and the file names the last scan number (D102); before it neither is
    /// there. The number is never below 0.
    /// </summary>
    /// <exception cref="ArgumentException">The file breaks that.</exception>
    internal static void CheckScans(IReadOnlyList<MissionDto> missions, int? lastScanNo, int schemaVersion)
    {
        const int withScans = PlanFile.NoScanSchemaVersion + 1;
        if (schemaVersion < withScans && missions.FirstOrDefault(m => m.Contract?.Scan is not null) is { } scanned)
        {
            throw new ArgumentException(Invariant($"Mission {scanned.Id} has a scan, which is new in schema {withScans} (the file says {schemaVersion})."), nameof(Missions));
        }

        if (schemaVersion < withScans && lastScanNo is not null)
        {
            throw new ArgumentException(Invariant($"lastScanNo is new in schema {withScans} (the file says {schemaVersion})."), nameof(LastScanNo));
        }

        if (lastScanNo < 0)
        {
            throw new ArgumentException(Invariant($"lastScanNo is {lastScanNo}; it is 0 or more."), nameof(LastScanNo));
        }
    }

    /// <summary>Every crate names its item (D68).</summary>
    /// <exception cref="ArgumentException">A crate breaks that.</exception>
    internal static void CheckItems(IReadOnlyList<CrateDto> crates, int schemaVersion)
    {
        if (crates.FirstOrDefault(c => c.Item is null) is { } without)
        {
            throw new ArgumentException(Invariant($"A crate for '{without.Destination}' has no item, which schema {schemaVersion} requires."), nameof(Crates));
        }
    }
}
