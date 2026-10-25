using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// The missions as entered and the plan made from them, with the player's moves and ticks. Once the plan has either,
/// it can't be planned again from the missions, so both are kept together (D18). The missions are kept as entered
/// (SCU, max crate size, override), so the editor shows them as typed (D23); every crate names its mission (D42).
/// </summary>
/// <param name="CrateCap">The cap the derived mixes on board were split under: the ship's (<see cref="CrateCap.Of(Ship)"/>,
/// D70). Not stored in the file; the reader takes it from the ship. No default, so no way into a plan forgets it.</param>
/// <param name="Watermark">The highest ContractTracker id taken into the plan (D39 addendum); 0 = none yet.</param>
public sealed record SavedPlan(IReadOnlyList<Mission> Missions, LoadPlan Plan, CrateCap CrateCap, long Watermark = 0)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public IReadOnlyList<Mission> Missions { get; } = ValidatedMissions(Missions);
    public LoadPlan Plan { get; } = Validated(Missions, Plan, CrateCap);
    public long Watermark { get; } =
        Watermark >= 0 ? Watermark : throw new ArgumentOutOfRangeException(nameof(Watermark), Watermark, "The watermark is 0 or more.");

    /// <summary>
    /// One stop per destination with the items of every mission, as planner, editor and mission file see them: in
    /// <see cref="LoadPlan.Route"/> (the order of the legend, D64), then destinations only unplaced missions go to, in
    /// mission order. Within a stop, items in mission order. Derived on every read.
    /// </summary>
    public IReadOnlyList<MissionStop> Stops
    {
        get
        {
            var items = Missions
                .SelectMany(m => m.Stops)
                .GroupBy(s => s.Destination)
                .ToDictionary(g => g.Key, g => g.SelectMany(s => s.Items).ToArray());
            string[] order = [.. Plan.Route.Where(items.ContainsKey).Union(items.Keys)];
            return [.. order.Select((destination, index) => new MissionStop(destination, index + 1, items[destination]))];
        }
    }

    /// <summary>True if a derived mix is split into smaller crates than its contract allows, for the overlay's notice (D70).</summary>
    public bool CratesCapped => Missions.SelectMany(m => m.Items).Any(at => at.Item.CappedBy(CrateCap));

    /// <summary>The id the next mission gets: one above the highest, so an id is never reused within a plan.</summary>
    public int NextMissionId => Missions.Count == 0 ? 1 : Missions.Max(m => m.Id) + 1;

    /// <exception cref="ArgumentException">The plan has no mission <paramref name="id"/>.</exception>
    public Mission MissionOf(int id) =>
        Missions.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException(Invariant($"The plan has no mission {id}."), nameof(id));

    /// <summary>
    /// A plan with one mission per stop, its crates assigned by destination and then to the items holding their kind
    /// (<see cref="WithItems"/>): the missions of a plan made before missions existed (plan schema 1 and 2, D42), and of a
    /// plan made from a route alone.
    /// </summary>
    /// <exception cref="ArgumentException">The plan doesn't fit the stops (see the constructor).</exception>
    public static SavedPlan FromStops(IReadOnlyList<MissionStop> stops, LoadPlan plan, CrateCap cap, long watermark = 0)
    {
        Mission[] missions = [.. stops.OrderBy(s => s.Order).Select((s, index) => new Mission(index + 1, [s with { Order = 1 }]))];
        var idOf = missions.ToDictionary(m => m.Stops[0].Destination, m => m.Id);
        Crate[] owned = [.. plan.Crates.Select(c => c with { Mission = idOf.GetValueOrDefault(c.Destination) })];
        return new SavedPlan(missions, plan with { Crates = WithItems(missions, owned, _ => false, cap) }, cap, watermark);
    }

    /// <summary>
    /// The crates with their items (D68): each keeps its mission, those <paramref name="keep"/> picks keep their item too,
    /// the others take the items of their mission still free for their kind (destination, commodity, crate size), in item
    /// order and plan order. Which of two equal crates gets which item is no choice the game knows of (D38). A crate with
    /// no item left keeps its index, and the constructor names the mismatch. Derived mixes are split up to <paramref name="cap"/>.
    /// </summary>
    internal static IReadOnlyList<Crate> WithItems(IReadOnlyList<Mission> missions, IReadOnlyList<Crate> crates, Func<Crate, bool> keep, CrateCap cap)
    {
        var free = missions
            .SelectMany(m => m.Items.SelectMany((at, index) => at.Item.CrateListUnder(cap).Select(size =>
                (Key: (m.Id, at.Stop.Destination, at.Item.Commodity, size), Index: index))))
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Index).ToList());

        foreach (var kept in crates.Where(keep))
        {
            free.GetValueOrDefault(KindOf(kept))?.Remove(kept.Item);
        }

        return [.. crates.Select(c => keep(c) || free.GetValueOrDefault(KindOf(c)) is not [var next, ..] list ? c : Took(list, c with { Item = next }))];

        static (int, string, string, CrateSize) KindOf(Crate crate) => (crate.Mission, crate.Destination, crate.Commodity, crate.CrateSize);

        static Crate Took(List<int> list, Crate crate)
        {
            list.RemoveAt(0);
            return crate;
        }
    }

    private static IReadOnlyList<Mission> ValidatedMissions(IReadOnlyList<Mission> missions)
    {
        if (missions.GroupBy(m => m.Id).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException(Invariant($"Mission id {twice.Key} is used more than once."), nameof(Missions));
        }

        // One stop per destination at the elevator, so one spelling: "Riker" and "riker" would be two legend lines.
        var spellings = missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()
            .GroupBy(d => d, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (spellings is not null)
        {
            throw new ArgumentException($"Destination '{spellings.First()}' is spelt more than one way: {string.Join(", ", spellings)}.", nameof(Missions));
        }

        return missions;
    }

    /// <summary>
    /// The crates of every placed mission are the crates of its expanded items (D23, D24): per item (D68) and crate size
    /// the same number, every crate naming an item of its destination and commodity; an unplaced mission has none (D41). Moving and ticking never change that, split and
    /// merge change crates and missions together (D27), so a pair that fails here was made from other missions or edited
    /// by hand. Where the crates lie needs the ship and is checked on reading (PlanFile). A derived mix is the one split up
    /// to <paramref name="cap"/> (D70).
    /// </summary>
    private static LoadPlan Validated(IReadOnlyList<Mission> missions, LoadPlan plan, CrateCap cap)
    {
        var byId = missions.ToDictionary(m => m.Id);
        foreach (var crate in plan.Crates)
        {
            if (!byId.TryGetValue(crate.Mission, out var mission))
            {
                throw new ArgumentException(missions.Any(m => m.Stops.Any(s => s.Destination == crate.Destination))
                    ? Invariant($"A crate for '{crate.Destination}' belongs to mission {crate.Mission}, which the plan doesn't have.")
                    : $"Cargo for '{crate.Destination}' is placed, but the missions have no such stop.", nameof(Plan));
            }

            if (mission.Unplaced)
            {
                throw new ArgumentException(Invariant($"Mission {mission.Id} didn't fit, but a crate for '{crate.Destination}' is placed."), nameof(Plan));
            }

            if (!mission.Stops.Any(s => s.Destination == crate.Destination))
            {
                throw new ArgumentException(Invariant($"Cargo for '{crate.Destination}' is placed for mission {mission.Id}, which doesn't go there."), nameof(Plan));
            }

            var items = mission.Items;
            if (crate.Item < 0 || crate.Item >= items.Count)
            {
                throw new ArgumentException(Invariant(
                    $"A crate for '{crate.Destination}' names item {crate.Item} of mission {mission.Id}, which has {items.Count} items."), nameof(Plan));
            }

            var (stop, item) = items[crate.Item];
            if ((stop.Destination, item.Commodity) != (crate.Destination, crate.Commodity))
            {
                throw new ArgumentException(Invariant(
                    $"A crate of {crate.Commodity} for '{crate.Destination}' names item {crate.Item} of mission {mission.Id}, which is {item.Commodity} for '{stop.Destination}'."),
                    nameof(Plan));
            }
        }

        // Per contract item (D68): an item's crates are its crate list, and no crate counts for another item of its kind.
        var needed = missions.Where(m => !m.Unplaced)
            .SelectMany(m => m.Items.SelectMany((at, index) => at.Item.CrateListUnder(cap).Select(size =>
                (m.Id, Item: index, at.Stop.Destination, at.Item.Commodity, Size: size))))
            .GroupBy(key => key)
            .ToDictionary(g => g.Key, g => g.Count());
        var placed = plan.Crates
            .GroupBy(c => (c.Mission, c.Item, c.Destination, c.Commodity, Size: c.CrateSize))
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var key in needed.Keys.Union(placed.Keys))
        {
            var (countNeeded, countPlaced) = (needed.GetValueOrDefault(key), placed.GetValueOrDefault(key));
            if (countNeeded != countPlaced)
            {
                throw new ArgumentException(Invariant(
                    $"{key.Destination}, {key.Commodity}: {countPlaced} of {countNeeded} crates of {(int)key.Size} SCU placed."),
                    nameof(Plan));
            }
        }

        return plan;
    }
}
