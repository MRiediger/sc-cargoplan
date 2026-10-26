using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a plan file (schema version 7, docs/plan-format.md): the missions with their stops and where they came
/// from (D42), every crate with its mission and item (D68), rack, position (D24), whether it is turned (D71), whether it
/// is off the ship (K2.2) and whether it is loaded (K6.4), and the ContractTracker watermark (D39 addendum). Schema 6 is
/// the same with nothing loaded; schema 5 also with every crate in a rack; schema 4 also without turned crates; schema 3 also without items: read, each crate given an item of its
/// mission on reading.
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
    /// <exception cref="ArgumentException">A value violates the domain rules, or the plan doesn't fit the ship or its missions.</exception>
    public SavedPlan ToSavedPlan(Ship ship)
    {
        Mission[] missions = [.. Missions.Select(m => m.ToMission())];
        CheckItems(Crates, SchemaVersion);
        CheckRotated(Crates, SchemaVersion);
        CheckOffShip(Crates, SchemaVersion);
        CheckLoaded(Crates, SchemaVersion);
        CheckManual(Missions, SchemaVersion);
        Crate[] crates = [.. Crates.Select(c => c.ToCrate())];
        var (plan, racksChanged) = Checked(new LoadPlan(ShipId, crates, [.. Warnings.Select(w => w.ToPlanWarning())], FillOrderJson.Parse(FillOrder)), ship);

        return UnderCap(ship, cap => new SavedPlan(
            missions,
            SchemaVersion == PlanFile.ItemlessSchemaVersion ? plan with { Crates = SavedPlan.WithItems(missions, crates, _ => false, cap) } : plan,
            cap,
            ContractTrackerWatermark), racksChanged);
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

    public static PlanFileDto FromSavedPlan(SavedPlan saved) => new(
        PlanFile.SupportedSchemaVersion,
        saved.Plan.ShipId,
        FillOrderJson.Text(saved.Plan.FillOrder),
        saved.Watermark,
        [.. saved.Missions.Select(MissionDto.FromMission)],
        [.. saved.Plan.Crates.Select(CrateDto.FromCrate)],
        [.. saved.Plan.Warnings.Select(PlanWarningDto.FromPlanWarning)]);

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

    /// <summary>Every crate from schema 4 on names its item; before schema 4 none does (D68).</summary>
    /// <exception cref="ArgumentException">A crate breaks that.</exception>
    internal static void CheckItems(IReadOnlyList<CrateDto> crates, int schemaVersion)
    {
        const int withItems = PlanFile.ItemlessSchemaVersion + 1;
        if (schemaVersion >= withItems && crates.FirstOrDefault(c => c.Item is null) is { } without)
        {
            throw new ArgumentException(Invariant($"A crate for '{without.Destination}' has no item, which schema {schemaVersion} requires."), nameof(Crates));
        }

        if (schemaVersion < withItems && crates.FirstOrDefault(c => c.Item is not null) is { } named)
        {
            throw new ArgumentException(Invariant(
                $"A crate for '{named.Destination}' names an item, which is new in schema {withItems} (the file says {schemaVersion})."), nameof(Crates));
        }
    }

    /// <summary>Before schema 5 no crate turns (D71); a <c>rotated</c> there is a file from elsewhere.</summary>
    /// <exception cref="ArgumentException">A crate breaks that.</exception>
    internal static void CheckRotated(IReadOnlyList<CrateDto> crates, int schemaVersion)
    {
        if (schemaVersion <= PlanFile.UnrotatedSchemaVersion && crates.FirstOrDefault(c => c.Rotated is not null) is { } turned)
        {
            throw new ArgumentException(Invariant(
                $"A crate for '{turned.Destination}' says rotated, which is new in schema {PlanFile.UnrotatedSchemaVersion + 1} (the file says {schemaVersion})."), nameof(Crates));
        }
    }

    /// <summary>Before schema 6 every crate lies in a rack (K2.2); an <c>offShip</c> there is a file from elsewhere.</summary>
    /// <exception cref="ArgumentException">A crate breaks that.</exception>
    internal static void CheckOffShip(IReadOnlyList<CrateDto> crates, int schemaVersion)
    {
        if (schemaVersion <= PlanFile.NoOffShipSchemaVersion && crates.FirstOrDefault(c => c.OffShip is not null) is { } offShip)
        {
            throw new ArgumentException(Invariant(
                $"A crate for '{offShip.Destination}' says offShip, which is new in schema {PlanFile.NoOffShipSchemaVersion + 1} (the file says {schemaVersion})."), nameof(Crates));
        }
    }

    /// <summary>Before schema 7 no item is manual (K6.5); a <c>manual</c> there is a file from elsewhere.</summary>
    /// <exception cref="ArgumentException">An item breaks that.</exception>
    internal static void CheckManual(IReadOnlyList<MissionDto> missions, int schemaVersion)
    {
        if (schemaVersion <= PlanFile.NotLoadedSchemaVersion
            && missions.SelectMany(m => m.Stops).SelectMany(s => s.Items.Select(i => (s.Destination, i))).FirstOrDefault(x => x.i.Manual is not null) is ({ } destination, { } manual))
        {
            throw new ArgumentException(Invariant(
                $"Stop '{destination}', {manual.Commodity} says manual, which is new in schema {PlanFile.NotLoadedSchemaVersion + 1} (the file says {schemaVersion})."), nameof(Missions));
        }
    }

    /// <summary>Before schema 7 no crate is loaded (K6.4); a <c>loaded</c> there is a file from elsewhere.</summary>
    /// <exception cref="ArgumentException">A crate breaks that.</exception>
    internal static void CheckLoaded(IReadOnlyList<CrateDto> crates, int schemaVersion)
    {
        if (schemaVersion <= PlanFile.NotLoadedSchemaVersion && crates.FirstOrDefault(c => c.Loaded is not null) is { } loaded)
        {
            throw new ArgumentException(Invariant(
                $"A crate for '{loaded.Destination}' says loaded, which is new in schema {PlanFile.NotLoadedSchemaVersion + 1} (the file says {schemaVersion})."), nameof(Crates));
        }
    }

    /// <summary>The embedded mission file of schema 1 and 2; never 3, the plan has its own fill order (D72).</summary>
    /// <exception cref="JsonException">The missions are not a mission file of a supported schema version.</exception>
    internal static IReadOnlyList<MissionStop> ParseMissions(JsonElement missions)
    {
        try
        {
            return MissionFileReader.Parse(missions, MissionFileReader.EmbeddedSchemaVersions).Stops;
        }
        catch (JsonException ex)
        {
            // The outer file is fine; say that the problem is in the embedded mission file.
            throw new JsonException($"missions: {ex.Message}", ex);
        }
    }
}
