namespace CargoPlan.Core.Models;

/// <summary>
/// The result of planning: every crate with its rack and position (D24), the planner's hints, the fill order and row axis
/// (D88) that lay out crates moved later, and whether Plan and Re-plan keep destinations in sections, with how many empty
/// rows between them (D89).
/// </summary>
public sealed record LoadPlan(
    string ShipId, IReadOnlyList<Crate> Crates, IReadOnlyList<PlanWarning> Warnings, FillOrder FillOrder = FillOrder.BowFirst, FillRows FillRows = FillRows.Lengthwise, PlanGrouping Grouping = PlanGrouping.None, int SectionGap = 0)
{
    /// <summary>The most empty rows a plan asks for between sections (D89).</summary>
    public const int MaxSectionGap = 2;

    // Checked in the initializer (constructor) and in init (`with`), as Crate.Rotated.
    public int SectionGap { get; init => field = CheckedGap(value); } = CheckedGap(SectionGap);

    /// <exception cref="ArgumentException">Not 0, 1 or 2.</exception>
    public static int CheckedGap(int gap) =>
        gap is >= 0 and <= MaxSectionGap ? gap : throw new ArgumentException(FormattableString.Invariant($"sectionGap {gap} is unknown, use 0, 1 or 2."), nameof(SectionGap));

    /// <summary>
    /// The crates grouped by rack, destination, commodity, crate size and delivered state, in order of first appearance:
    /// what hints, legend and text renderer work on (D24). Derived on every read, not stored: a <c>with</c> on
    /// <see cref="Crates"/> skips initializers and would leave stored placements stale. GroupBy keeps the order of first
    /// appearance (learn.microsoft.com, Enumerable.GroupBy, Remarks).
    /// </summary>
    public IReadOnlyList<Placement> Placements =>
    [
        .. Crates
            .GroupBy(c => (c.RackId, c.Destination, c.Commodity, c.CrateSize, c.Delivered))
            .Select(g => new Placement(
                g.Key.Destination, g.Key.RackId, new CargoItem(g.Key.Commodity, g.Count() * (int)g.Key.CrateSize, g.Key.CrateSize), g.Key.Delivered)),
    ];

    /// <summary>
    /// The destinations in order of their first crate: the order the player entered them, which legend and palette show
    /// (D64). Display only: the planner assumes no route. Derived like <see cref="Placements"/>; GroupBy keeps the order of
    /// first appearance.
    /// </summary>
    public IReadOnlyList<string> Route => [.. Crates.GroupBy(c => c.Destination).Select(g => g.Key)];

    /// <summary>
    /// The legend's order (B6, D69): missions in the order they first appear along <see cref="Route"/>, on a tie in
    /// <paramref name="missionOrder"/>, a mission's destinations in route order.
    /// </summary>
    /// <param name="missionOrder">The ids of the plan's missions as listed; empty for a plan without missions.</param>
    public IReadOnlyList<LegendMission> LegendMissions(IReadOnlyList<int> missionOrder)
    {
        var rank = Route.Select((destination, index) => (destination, index)).ToDictionary(r => r.destination, r => r.index);
        var listed = missionOrder.Select((id, index) => (id, index)).ToDictionary(m => m.id, m => m.index);
        return
        [
            .. Crates.GroupBy(c => c.Mission)
                .OrderBy(m => m.Min(c => rank[c.Destination]))
                .ThenBy(m => listed.GetValueOrDefault(m.Key))
                .Select(m => new LegendMission(m.Key, [.. m], [.. m.GroupBy(c => c.Destination).OrderBy(d => rank[d.Key]).Select(d => new LegendLine(d.Key, [.. d]))])),
        ];
    }

    /// <summary>
    /// The destinations as the legend shows them, top to bottom, each at its first line: the order the player sees at the
    /// elevator, which is not always <see cref="Route"/>: a mission brings all its destinations along.
    /// </summary>
    /// <param name="missionOrder">See <see cref="LegendMissions"/>.</param>
    public IReadOnlyList<string> LegendDestinations(IReadOnlyList<int> missionOrder) =>
        [.. LegendMissions(missionOrder).SelectMany(m => m.Lines).Select(l => l.Destination).Distinct()];

    public IReadOnlyList<Crate> CratesForRack(string rackId) =>
        [.. Crates.Where(c => c.RackId == rackId)];

    public IReadOnlyList<Placement> PlacementsForRack(string rackId) =>
        [.. Placements.Where(p => p.RackId == rackId)];

    public IReadOnlyList<Placement> PlacementsForStop(string destination) =>
        [.. Placements.Where(p => p.Destination == destination)];

    /// <summary>
    /// SCU of the crates on board, in the whole ship or in rack <paramref name="rackId"/>: delivered crates are off the ship
    /// and take no room (D24). What the capacity display shows (D67) and what a waiting mission's room is measured against.
    /// </summary>
    public int ScuOnBoard(string? rackId = null) =>
        Crates.Where(c => !c.Delivered && (rackId is null || c.RackId == rackId)).Sum(c => (int)c.CrateSize);

    public IReadOnlyList<PlanWarning> WarningsForRack(string rackId) =>
        [.. Warnings.Where(w => w.RackId == rackId)];

    /// <summary>Planner, text renderer, ship view and plan file all need the plan's own ship; one check for all.</summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public void EnsureBelongsTo(Ship ship)
    {
        if (ShipId != ship.Id)
        {
            throw new ArgumentException($"The plan is for ship '{ShipId}', not '{ship.Id}'.", nameof(ship));
        }
    }
}
