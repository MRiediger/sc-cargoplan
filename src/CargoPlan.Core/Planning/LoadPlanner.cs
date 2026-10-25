using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using static System.FormattableString;

namespace CargoPlan.Core.Planning;

/// <summary>Decides which rack each stop goes into, and where its crates lie there (D24), without a route (D64).</summary>
public static class LoadPlanner
{
    /// <summary>
    /// Plans the stops into the ship's racks without assuming a route (D64): the stops' order is the order of the crate
    /// list (legend and colours), never where a crate goes. A destination stays in one rack where it can, a rack holds one
    /// destination where it can, and no crate sits on another destination's cargo where a layout avoids it
    /// (<see cref="RackAssignment"/>); each rack's crates are laid out in <paramref name="fillOrder"/>, stored with the
    /// plan. Every shared rack, every rack holding part of a destination that spans racks and every crate on another
    /// destination's cargo gets a hint. If the search finds no assignment that lays out, every crate goes largest first
    /// into the first rack that takes it (<see cref="FirstFitDecreasing"/>, D54).
    /// </summary>
    /// <exception cref="ArgumentException">Two stops share an order or a destination.</exception>
    /// <exception cref="PlanException">
    /// A crate's shape fits no rack, the stops exceed the ship, or a crate finds no gap of its shape in any rack.
    /// </exception>
    public static LoadPlan Plan(Ship ship, IReadOnlyList<Stop> stops, FillOrder fillOrder = FillOrder.BowFirst)
    {
        EnsureUnique(stops);

        Stop[] input = [.. stops.OrderBy(s => s.Order)];
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        EnsureCratesFitShip(ship, input);
        EnsureFitsShip(ship, input);

        if (RackAssignment.Assign(racks, input, fillOrder) is { } assignment)
        {
            return WithHints(new LoadPlan(ship.Id, Crates(input, assignment.Racks), [], fillOrder), ship,
                StackingUncheckedNotes(assignment.Racks.Where(l => l.StackingUnchecked).Select(l => l.Rack)));
        }

        // Largest first, then destination by SCU and name, then commodity: the search's order, so the fallback is order-free too.
        (int Stop, int Item, CrateSize Size)[] all =
        [
            .. input.SelectMany((stop, s) => stop.Items.SelectMany((item, i) => Enumerable.Repeat((s, i, item.CrateSize), item.Scu / (int)item.CrateSize)))
                .OrderByDescending(c => c.CrateSize)
                .ThenByDescending(c => input[c.s].TotalScu)
                .ThenBy(c => input[c.s].Destination, StringComparer.Ordinal)
                .ThenBy(c => input[c.s].Items[c.i].Commodity, StringComparer.Ordinal),
        ];
        var (rackOf, failed) = FirstFitDecreasing(racks, [], [.. all.Select(c => (c.Size, input[c.Stop].Destination))], fillOrder);
        if (failed is { } crate)
        {
            throw NoGap(ship, input, all, rackOf, crate);
        }

        // Per rack the crates are the ones the probe took largest first, and Lay lays them largest first again: they fit.
        var budget = new CrateLayout.StackingBudget();
        List<RackAssignment.RackLoad> loads = [];
        for (var r = 0; r < racks.Length; r++)
        {
            int[] inRack = [.. Enumerable.Range(0, all.Length).Where(i => rackOf[i] == r)];
            if (inRack.Length == 0)
            {
                continue;
            }

            var laying = CrateLayout.Lay(racks[r], [], [.. inRack.Select(i => (all[i].Size, input[all[i].Stop].Destination))], fillOrder, budget.For(racks[r]))!;
            budget.Spend(racks[r], laying);
            loads.Add(new RackAssignment.RackLoad(
                racks[r], [.. inRack.Zip(laying.Positions).Select(p => (new RackAssignment.Piece(all[p.First].Stop, all[p.First].Item, all[p.First].Size), p.Second))], laying.GaveUp));
        }

        return WithHints(new LoadPlan(ship.Id, Crates(input, loads), [], fillOrder), ship,
            StackingUncheckedNotes(loads.Where(l => l.StackingUnchecked).Select(l => l.Rack)));
    }

    /// <summary>
    /// The crates of <paramref name="loads"/> in the order of <paramref name="stops"/> and their items, which the legend
    /// and the colours show (<see cref="LoadPlan.Route"/>); an item's crates rack by rack.
    /// </summary>
    private static List<Crate> Crates(Stop[] stops, IReadOnlyList<RackAssignment.RackLoad> loads) =>
    [
        .. stops.SelectMany((stop, s) => stop.Items.SelectMany((item, i) => loads.SelectMany(load => load.Crates
            .Where(c => c.Piece.Stop == s && c.Piece.Item == i)
            .Select(c => new Crate(stop.Destination, item.Commodity, c.Piece.Size, load.Rack.Id, c.Position))))),
    ];

    /// <summary>
    /// Puts one crate at a cell of a rack, as the player sees it at the cargo lift (D24): <see cref="MoveCrates"/> with one move.
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    /// <exception cref="PlanException">See <see cref="MoveCrates"/>.</exception>
    public static LoadPlan MoveCrate(LoadPlan plan, Ship ship, Crate crate, string targetRackId, GridPosition targetPosition) =>
        MoveCrates(plan, ship, [new CrateMove(crate, targetRackId, targetPosition)]);

    /// <summary>
    /// Puts crates at cells of racks, all at once (D25), and finds room where a cell doesn't hold them (D65): the plan of
    /// <see cref="ResolveMove(LoadPlan, Ship, IReadOnlyList{CrateMove}, MoveStage)"/>, its notices dropped. Crates keep
    /// their index, so placements keep their order; hints are derived anew. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, the ship has no such rack, or a crate isn't part of the plan, is delivered or
    /// moves twice.
    /// </exception>
    /// <exception cref="PlanException">No rack takes the crates (<see cref="MoveOutcome.Refusal"/>).</exception>
    public static LoadPlan MoveCrates(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves) =>
        moves.Count == 0 ? plan : Applied(ResolveMove(plan, ship, moves));

    /// <summary>
    /// Where crates go when the player puts each at a cell (D65): the cell itself, fallen onto the next crate or the plate;
    /// else the nearest free place in its rack; else that rack laid out anew; else other destinations' cargo displaced from
    /// it; else another rack. A selection moves as a whole or not at all. One function for the overlay's preview and the
    /// drop: with <paramref name="upTo"/> below <see cref="MoveStage.Relayout"/> it answers
    /// <see cref="MoveStage.Pending"/> instead of laying a rack out anew. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    public static MoveOutcome ResolveMove(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves, MoveStage upTo = MoveStage.Refused)
    {
        plan.EnsureBelongsTo(ship);
        HashSet<int> taken = [];
        return MoveResolution.Crates(plan, ship,
            [.. moves.Select(m => new MoveResolution.Intent(IndexOnBoard(plan, m.Crate, taken, "moves", nameof(moves)), RackOf(ship, m.RackId, nameof(moves)), m.Position))],
            upTo);
    }

    /// <summary>
    /// A drag of <paramref name="crates"/> that puts <paramref name="anchor"/> at <paramref name="position"/> of
    /// <paramref name="rackId"/>, the others at their offset to it (<see cref="CrateMove.Shift"/>), resolved as
    /// <see cref="ResolveMove(LoadPlan, Ship, IReadOnlyList{CrateMove}, MoveStage)"/> does. If a crate's offset lands in no
    /// rack, the crates go into the anchor's rack without cells (D65).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    public static MoveOutcome ResolveMove(
        LoadPlan plan, Ship ship, IReadOnlyList<Crate> crates, Crate anchor, string rackId, GridPosition position, MoveStage upTo = MoveStage.Refused)
    {
        plan.EnsureBelongsTo(ship);
        if (CrateMove.Shift(ship, crates, anchor, rackId, position) is { } moves)
        {
            return ResolveMove(plan, ship, moves, upTo);
        }

        var target = RackOf(ship, rackId, nameof(rackId));
        HashSet<int> taken = [];
        return MoveResolution.Crates(plan, ship, [.. crates.Select(c => new MoveResolution.Intent(IndexOnBoard(plan, c, taken, "moves", nameof(crates)), target, null))], upTo);
    }

    /// <summary>
    /// Moves every crate of a stop still on board to another rack (D25): the plan of <see cref="ResolveStopMove"/>, its
    /// notices dropped. A stop whose crates on board all lie in the target rack stays as it is. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, has no stop <paramref name="destination"/>, or the ship has no such rack.
    /// </exception>
    /// <exception cref="PlanException">No crate of the stop gets into the target rack.</exception>
    public static LoadPlan MoveStop(LoadPlan plan, Ship ship, string destination, string targetRackId) =>
        Applied(ResolveStopMove(plan, ship, destination, targetRackId));

    /// <summary>
    /// Where a stop goes when the player drags its legend line onto a rack (D65): its crates on board laid around the
    /// cargo there, else the rack laid out anew, else other destinations' cargo displaced; what still doesn't fit stays
    /// where it lies, with a notice. Delivered cargo is off the ship and stays, so a delivered stop moves nothing. Preview
    /// and drop as with <see cref="ResolveMove(LoadPlan, Ship, IReadOnlyList{CrateMove}, MoveStage)"/>.
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveStop"/>.</exception>
    public static MoveOutcome ResolveStopMove(LoadPlan plan, Ship ship, string destination, string targetRackId, MoveStage upTo = MoveStage.Refused)
    {
        plan.EnsureBelongsTo(ship);
        var target = RackOf(ship, targetRackId, nameof(targetRackId));
        EnsureStop(plan, destination);

        int[] moving = [.. Enumerable.Range(0, plan.Crates.Count).Where(i => plan.Crates[i] is { Delivered: false } c && c.Destination == destination && c.RackId != target.Id)];
        return MoveResolution.Stop(plan, ship, moving, target, upTo);
    }

    private static LoadPlan Applied(MoveOutcome outcome) =>
        outcome.Plan ?? throw new PlanException(outcome.Refusal ?? "");

    /// <summary>
    /// Marks every crate of a stop as delivered, or back. A stop is unloaded in one visit, split parts included,
    /// so the unit is the stop. The hints are derived anew without delivered cargo. Delivered crates keep their position
    /// but free their cells (D24 addendum): ticking back gives the plan before unless another crate took one of those
    /// cells since, then it is refused. A crate resting on the delivered stop floats and is marked, not refused: the
    /// player unloaded out of order and restacked in the game. The original plan stays unchanged, crate order too.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no stop <paramref name="destination"/>.</exception>
    /// <exception cref="PlanException">Ticking back: a crate of the stop would break a rule (<see cref="CrateLayout.NewProblem"/>).</exception>
    public static LoadPlan SetDelivered(LoadPlan plan, Ship ship, string destination, bool delivered)
    {
        plan.EnsureBelongsTo(ship);
        EnsureStop(plan, destination);
        return SetDelivered(plan, ship, destination, c => c.Destination == destination, delivered);
    }

    /// <summary>
    /// Marks the crates of one mission for a stop as delivered, or back: the legend's line (B6, D69), for the player who
    /// unloads one contract's crates at a time. Otherwise as the stop's overload, ticking back refused the same way.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship or has no crate of <paramref name="mission"/> for <paramref name="destination"/>.
    /// </exception>
    /// <exception cref="PlanException">Ticking back: a crate would break a rule (<see cref="CrateLayout.NewProblem"/>).</exception>
    public static LoadPlan SetDelivered(LoadPlan plan, Ship ship, string destination, int mission, bool delivered)
    {
        plan.EnsureBelongsTo(ship);
        if (!plan.Crates.Any(c => c.Destination == destination && c.Mission == mission))
        {
            throw new ArgumentException(Invariant($"The plan has no crate of mission {mission} for '{destination}'."), nameof(mission));
        }

        return SetDelivered(plan, ship, destination, c => c.Destination == destination && c.Mission == mission, delivered);
    }

    private static LoadPlan SetDelivered(LoadPlan plan, Ship ship, string destination, Func<Crate, bool> ticked, bool delivered)
    {
        var changed = plan with { Crates = [.. plan.Crates.Select(c => ticked(c) ? c with { Delivered = delivered } : c)] };
        if (!delivered)
        {
            HashSet<string> racks = [.. plan.Crates.Where(ticked).Select(c => c.RackId)];
            var problem = ship.Racks.OrderBy(r => r.Index)
                .Where(r => racks.Contains(r.Id))
                .Select(r => CrateLayout.NewProblem(r, plan.CratesForRack(r.Id), changed.CratesForRack(r.Id)))
                .FirstOrDefault(p => p is not null);
            if (problem is not null)
            {
                throw new PlanException($"{destination} can't go back on board: {problem}");
            }
        }

        return WithHints(changed, ship);
    }

    /// <summary>
    /// Plans missions from scratch, as the editor's Plan does (D19): their stops merged per destination in
    /// <paramref name="stopOrder"/> go through <see cref="Plan(Ship, IReadOnlyList{Stop}, FillOrder)"/>, then every crate
    /// gets a mission that holds its kind. Equal crates of two missions can't be told apart in the game (D38), so which of
    /// them goes to which is no choice. Every mission is placed afterwards; ids, sources and the watermark stay.
    /// </summary>
    /// <param name="stopOrder">
    /// Every destination of the missions once, in the order the player entered them: the order of legend and colours,
    /// never of the racks (D64).
    /// </param>
    /// <exception cref="ArgumentException">The order doesn't name every destination once, or see the other overload.</exception>
    /// <exception cref="PlanException">See the other overload.</exception>
    public static SavedPlan Plan(
        Ship ship, IReadOnlyList<Mission> missions, IReadOnlyList<string> stopOrder, FillOrder fillOrder = FillOrder.BowFirst, long watermark = 0)
    {
        Mission[] placed = [.. missions.Select(m => m with { Unplaced = false })];
        var items = placed.SelectMany(m => m.Stops)
            .GroupBy(s => s.Destination)
            .ToDictionary(g => g.Key, g => g.SelectMany(s => s.Items).ToArray());
        if (stopOrder.Count != items.Count || stopOrder.Distinct().Count() != stopOrder.Count || !stopOrder.All(items.ContainsKey))
        {
            throw new ArgumentException("The stop order has to name every destination of the missions once.", nameof(stopOrder));
        }

        var plan = Plan(ship, [.. stopOrder.Select((destination, index) => new MissionStop(destination, index + 1, items[destination]).ToStop())], fillOrder);
        return new SavedPlan(placed, plan with { Crates = Assigned(plan.Crates, placed) }, watermark);
    }

    /// <summary>
    /// Adds a mission to the plan without moving a crate (D41): positions and ticks stay. Each of its stops, largest
    /// first, goes whole into a rack, tried in this order: racks with cargo on board for the same destination (one
    /// handover, D42), the first rack with nothing on board, then rack order; the first where no new crate sits on other
    /// cargo wins, else the first that takes it (D64). Otherwise crate by crate, largest first. Laid out by
    /// <see cref="CrateLayout.Place"/>, so delivered cells are free. A destination's new crates go right behind its crates
    /// in the list, others to the end. If a crate finds no place, the mission comes in without crates (<see cref="Mission.Unplaced"/>):
    /// the elevator hands out whole missions, so there is no partial placement. A destination spelt otherwise than one in
    /// the plan takes the plan's spelling. The original stays unchanged.
    /// </summary>
    /// <param name="stops">The mission's stops in contract order.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship, or the stops make no valid mission.</exception>
    public static SavedPlan PlaceInto(SavedPlan saved, Ship ship, IReadOnlyList<MissionStop> stops, ContractSource? contract = null)
    {
        saved.Plan.EnsureBelongsTo(ship);
        var spelling = saved.Missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()
            .ToDictionary(d => d, d => d, StringComparer.OrdinalIgnoreCase);
        Mission mission = new(saved.NextMissionId, [.. stops.Select(s => s with { Destination = spelling.GetValueOrDefault(s.Destination, s.Destination) })], contract);

        return TryPlace(saved.Plan, ship, mission) is ({ } crates, _, var stackingUnchecked)
            ? new SavedPlan([.. saved.Missions, mission], WithHints(saved.Plan with { Crates = crates }, ship, StackingUncheckedNotes(stackingUnchecked)), saved.Watermark)
            : new SavedPlan([.. saved.Missions, mission with { Unplaced = true }], saved.Plan, saved.Watermark);
    }

    /// <summary>
    /// Why the unplaced mission <paramref name="missionId"/> doesn't fit, for the legend (D41): "doesn't fit (16 SCU, 8
    /// free)" if the ship lacks the room, "doesn't fit (no room for 8 SCU crates)" if the room is there in pieces. Null for a
    /// placed mission, and for one that would fit now (placed at the next <see cref="RetryUnplaced"/>). The same check as
    /// <see cref="PlaceInto"/>, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no such mission.</exception>
    public static string? PlaceProblem(SavedPlan saved, Ship ship, int missionId)
    {
        saved.Plan.EnsureBelongsTo(ship);
        var mission = saved.MissionOf(missionId);
        if (!mission.Unplaced || TryPlace(saved.Plan, ship, mission) is not (null, { } failed, _))
        {
            return null;
        }

        var free = ship.TotalScu - saved.Plan.ScuOnBoard();
        return mission.TotalScu > free
            ? Invariant($"doesn't fit ({mission.TotalScu} SCU, {free} free)")
            : Invariant($"doesn't fit (no room for {(int)failed} SCU crates)");
    }

    /// <summary>
    /// Places every unplaced mission that fits now, in mission order, as <see cref="PlaceInto"/> would; the sync runs it
    /// first (D41, T5.4). The plan itself if none fits.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static SavedPlan RetryUnplaced(SavedPlan saved, Ship ship)
    {
        saved.Plan.EnsureBelongsTo(ship);
        var current = saved;
        foreach (var waiting in saved.Missions.Where(m => m.Unplaced))
        {
            if (TryPlace(current.Plan, ship, waiting) is ({ } crates, _, var stackingUnchecked))
            {
                current = new SavedPlan(
                    [.. current.Missions.Select(m => m.Id == waiting.Id ? m with { Unplaced = false } : m)],
                    WithHints(current.Plan with { Crates = crates }, ship, StackingUncheckedNotes(stackingUnchecked)),
                    current.Watermark);
            }
        }

        return current;
    }

    /// <summary>
    /// The plan laid out anew: on another ship when the player switches (D59), on its own ship when the player asks for
    /// it (Re-plan, D66). Missions, sources, watermark and fill order stay; positions and moves go, split crates keep their
    /// sizes (they are in the missions, D27). The only method that takes a plan of another ship.
    /// </summary>
    public static SavedPlan Replan(SavedPlan saved, Ship ship) =>
        saved.Plan.ShipId == ship.Id ? ReplanInPlace(saved, ship) : ReplanForShip(saved, ship);

    /// <summary>
    /// How many crates on board in <paramref name="before"/> lie elsewhere in <paramref name="after"/>: another rack or
    /// another cell (B4). Crates have no identity, so of equal crates (destination, commodity, size) only as many count as
    /// find no place one of them lies on in <paramref name="after"/>, ticked or not: a tick moves nothing. Crates new on board count not.
    /// </summary>
    public static int MovedCrates(LoadPlan before, LoadPlan after)
    {
        var left = after.Crates.GroupBy(Lying).ToDictionary(g => g.Key, g => g.Count());
        var moved = 0;
        foreach (var key in before.Crates.Where(c => !c.Delivered).Select(Lying))
        {
            if (left.GetValueOrDefault(key) > 0)
            {
                left[key]--;
            }
            else
            {
                moved++;
            }
        }

        return moved;
    }

    private static (string, string, CrateSize, string, GridPosition) Lying(Crate crate) =>
        (crate.Destination, crate.Commodity, crate.CrateSize, crate.RackId, crate.Position);

    /// <summary>
    /// The player's Re-plan (D66): the cargo on board laid out as a fresh <see cref="Plan(Ship, IReadOnlyList{Mission}, IReadOnlyList{string}, FillOrder, long)"/>
    /// of the same missions would lay it, waiting missions included, so the result doesn't depend on the order things came
    /// in (D64). Ticked crates are off the ship: they stay where they lay, ticked, and take no room (D24), also when only
    /// part of a stop is ticked (D41). Never loses cargo: if the waiting missions don't fit, the cargo on board is laid
    /// out without them and they wait (<see cref="RetryUnplaced"/>); if even that finds no layout, the plan stays as it is.
    /// The missions stay the same list unless one of them got placed.
    /// </summary>
    private static SavedPlan ReplanInPlace(SavedPlan saved, Ship ship)
    {
        Crate[] delivered = [.. saved.Plan.Crates.Where(c => c.Delivered)];
        Mission[] onBoard = [.. saved.Missions.Select(m => m.Unplaced ? m : WithoutDelivered(m, delivered)).OfType<Mission>()];
        if (onBoard.Length == 0)
        {
            return saved;
        }

        string[] route = [.. saved.Stops.Select(s => s.Destination)];
        if (PlannedOrNull(ship, onBoard, route, saved.Plan.FillOrder) is { } all)
        {
            IReadOnlyList<Mission> missions = saved.Missions.Any(m => m.Unplaced) ? [.. saved.Missions.Select(m => m with { Unplaced = false })] : saved.Missions;
            return new SavedPlan(missions, ItemsOf(saved.Missions, Joined(all, delivered, route, ship)), saved.Watermark);
        }

        if (PlannedOrNull(ship, [.. onBoard.Where(m => !m.Unplaced)], route, saved.Plan.FillOrder) is { } placed)
        {
            return RetryUnplaced(new SavedPlan(saved.Missions, ItemsOf(saved.Missions, Joined(placed, delivered, route, ship)), saved.Watermark), ship);
        }

        // No layout of the cargo on board (a heuristic ship, D54): the player's one stays, the waiting missions get a try.
        return RetryUnplaced(saved, ship);
    }

    /// <summary>
    /// <paramref name="mission"/> with only the crates still on board: its ticked crates taken out of its items' crate
    /// lists (D41: a stop can be ticked in part). Null if everything is ticked.
    /// </summary>
    private static Mission? WithoutDelivered(Mission mission, IReadOnlyList<Crate> delivered)
    {
        var gone = delivered.Where(c => c.Mission == mission.Id)
            .GroupBy(c => (c.Destination, c.Commodity))
            .ToDictionary(g => g.Key, g => g.Select(c => c.CrateSize).ToList());
        if (gone.Count == 0)
        {
            return mission;
        }

        MissionStop[] stops =
        [
            .. mission.Stops
                .Select(s => s with { Items = [.. s.Items.Select(i => Remaining(i, gone.GetValueOrDefault((s.Destination, i.Commodity)))).OfType<MissionItem>()] })
                .Where(s => s.Items.Count > 0),
        ];
        return stops.Length == 0 ? null : new Mission(mission.Id, stops, mission.Contract);
    }

    /// <summary>The item without the crates in <paramref name="gone"/>, which it takes out of that list; null if none is left.</summary>
    private static MissionItem? Remaining(MissionItem item, List<CrateSize>? gone)
    {
        if (gone is null)
        {
            return item;
        }

        List<CrateSize> left = [];
        foreach (var crate in item.CrateList)
        {
            if (!gone.Remove(crate))
            {
                left.Add(crate);
            }
        }

        return left.Count == 0 ? null : new MissionItem(item.Commodity, left.Sum(c => (int)c), item.MaxCrateSize, left, item.Pickup);
    }

    /// <summary>
    /// The crates with the items of <paramref name="missions"/> as they are, ticked crates keeping theirs (D68): the replan
    /// planned the missions without their ticked crates, whose items count differently.
    /// </summary>
    private static LoadPlan ItemsOf(IReadOnlyList<Mission> missions, LoadPlan plan) =>
        plan with { Crates = SavedPlan.WithItems(missions, plan.Crates, c => c.Delivered) };

    /// <summary>The editor's Plan of <paramref name="missions"/> in <paramref name="route"/>; null if they have no layout.</summary>
    private static LoadPlan? PlannedOrNull(Ship ship, IReadOnlyList<Mission> missions, string[] route, FillOrder fillOrder)
    {
        HashSet<string> destinations = [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination)];
        try
        {
            return Plan(ship, missions, [.. route.Where(destinations.Contains)], fillOrder).Plan;
        }
        catch (PlanException)
        {
            return null;
        }
    }

    /// <summary>
    /// The ticked crates as they lay and the crates planned anew, destination by destination in <paramref name="route"/>,
    /// so legend and colours stay (D64). Without ticked crates the plan itself, hints and notes as planned.
    /// </summary>
    private static LoadPlan Joined(LoadPlan planned, Crate[] delivered, string[] route, Ship ship) =>
        delivered.Length == 0
            ? planned
            : WithHints(planned with { Crates = [.. route.SelectMany(d => delivered.Where(c => c.Destination == d).Concat(planned.Crates.Where(c => c.Destination == d)))] }, ship);

    /// <summary>
    /// The plan on another ship (D59): its missions planned anew in the order the editor shows them
    /// (<see cref="SavedPlan.Stops"/>), as the editor's Plan would; if that fails, mission by mission into the empty ship
    /// in mission order (<see cref="RetryUnplaced"/>), the rest waits unplaced (D41). Every stop ticked as a whole gets its
    /// tick back.
    /// </summary>
    private static SavedPlan ReplanForShip(SavedPlan saved, Ship ship)
    {
        var fillOrder = saved.Plan.FillOrder;
        if (saved.Missions.Count == 0)
        {
            return new SavedPlan([], new LoadPlan(ship.Id, [], [], fillOrder), saved.Watermark);
        }

        string[] ticked = [.. saved.Plan.Crates.GroupBy(c => c.Destination).Where(g => g.All(c => c.Delivered)).Select(g => g.Key)];
        SavedPlan replanned;
        try
        {
            replanned = Plan(ship, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], fillOrder, saved.Watermark);
        }
        catch (PlanException)
        {
            // Too much for the ship, or no layout: what fits comes in mission by mission, as captures do (D41).
            SavedPlan empty = new([.. saved.Missions.Select(m => m with { Unplaced = true })], new LoadPlan(ship.Id, [], [], fillOrder), saved.Watermark);
            replanned = RetryUnplaced(empty, ship);
        }

        // Ticking never refuses (only ticking back can); a ticked stop whose mission waits has no crates to tick.
        var plan = ticked
            .Where(destination => replanned.Plan.Crates.Any(c => c.Destination == destination))
            .Aggregate(replanned.Plan, (current, destination) => SetDelivered(current, ship, destination, true));
        return new SavedPlan(replanned.Missions, plan, replanned.Watermark);
    }

    /// <summary>
    /// Takes a mission out of the plan with its crates (D42): deleted in the editor, dropped in the legend, or gone from
    /// ContractTracker. Nothing else moves, ticks stay; a crate that rested on one of its crates floats and is marked
    /// (<see cref="Validate"/>). The original stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no such mission.</exception>
    public static SavedPlan RemoveMission(SavedPlan saved, Ship ship, int missionId)
    {
        saved.Plan.EnsureBelongsTo(ship);
        var mission = saved.MissionOf(missionId);
        return new SavedPlan(
            [.. saved.Missions.Where(m => m != mission)],
            WithHints(saved.Plan with { Crates = [.. saved.Plan.Crates.Where(c => c.Mission != missionId)] }, ship),
            saved.Watermark);
    }

    /// <summary>
    /// Gives the stop <paramref name="from"/> the name <paramref name="to"/> (B1), in every mission and on every crate, so
    /// two names of one place become one stop. Mechanical: which names are one place, the import decides. A mission with
    /// both keeps one stop at the place of the first, items in stop order. Nothing moves, ticks stay, hints are derived anew.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship, has no stop <paramref name="from"/>, or the
    /// names are the same.</exception>
    public static SavedPlan RenameStop(SavedPlan saved, Ship ship, string from, string to)
    {
        saved.Plan.EnsureBelongsTo(ship);
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{from}' is renamed to itself.", nameof(to));
        }

        if (!saved.Missions.Any(m => m.Stops.Any(s => s.Destination == from)))
        {
            throw new ArgumentException($"The plan has no stop '{from}'.", nameof(from));
        }

        Mission[] missions = [.. saved.Missions.Select(m => m.Stops.Any(s => s.Destination == from) ? Renamed(m, from, to) : m)];

        // Merging two stops of a mission moves items between them, so a crate's item index follows its item (D68).
        var moved = saved.Missions.Zip(missions).Where(p => !ReferenceEquals(p.First, p.Second)).ToDictionary(p => p.First.Id, p => ItemIndexes(p.First, p.Second));
        Crate[] crates =
        [
            .. saved.Plan.Crates
                .Select(c => moved.TryGetValue(c.Mission, out var indexes) ? c with { Item = indexes[c.Item] } : c)
                .Select(c => c.Destination == from ? c with { Destination = to } : c),
        ];
        return new SavedPlan(missions, WithHints(saved.Plan with { Crates = crates }, ship), saved.Watermark);
    }

    /// <summary>Where each item of <paramref name="before"/> is in <paramref name="after"/>, which holds the same item records.</summary>
    private static int[] ItemIndexes(Mission before, Mission after)
    {
        List<(MissionItem Item, int Index)> left = [.. after.Items.Select((at, index) => (at.Item, index))];
        return [.. before.Items.Select(at => Take(left.FindIndex(l => ReferenceEquals(l.Item, at.Item))))];

        int Take(int found)
        {
            var index = left[found].Index;
            left.RemoveAt(found);
            return index;
        }
    }

    private static Mission Renamed(Mission mission, string from, string to)
    {
        MissionStop[] stops = [.. mission.Stops.OrderBy(s => s.Order)];
        var first = Array.FindIndex(stops, s => s.Destination == from || s.Destination == to);
        MissionItem[] items = [.. stops.Where(s => s.Destination == from || s.Destination == to).SelectMany(s => s.Items)];
        MissionStop[] kept =
        [
            .. stops.Select((s, index) => index == first ? new MissionStop(to, s.Order, items) : s)
                .Where((s, index) => index == first || (s.Destination != from && s.Destination != to)),
        ];
        return new Mission(mission.Id, [.. kept.Select((s, index) => s with { Order = index + 1 })], mission.Contract, mission.Unplaced);
    }

    /// <summary>
    /// The crates of <paramref name="plan"/> with those of <paramref name="mission"/> laid in (rule of <see cref="PlaceInto"/>);
    /// or, if a crate has no place, the size of the first that hasn't. Stop by stop first, largest stop first (then name, as
    /// the planner ranks them, D64); if a crate finds no place there, the whole mission largest first
    /// (<see cref="LargestFirst"/>, D54), and its failure is the one named. With the crates, the racks whose search for a
    /// layout without foreign stacking gave up (<see cref="CrateLayout.Lay"/>). All searches share one budget.
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> StackingUnchecked) TryPlace(LoadPlan plan, Ship ship, Mission mission)
    {
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        MissionStop[] stops = [.. mission.Stops.OrderByDescending(s => s.TotalScu).ThenBy(s => s.Destination, StringComparer.Ordinal)];
        var budget = new CrateLayout.StackingBudget();

        var placed = StopByStop(plan, mission, racks, stops, budget) is { Crates: not null } byStop
            ? byStop
            : LargestFirst(plan, mission, racks, stops, budget);

        // The crates of the mission take its items (D68); the crates already on board keep theirs.
        return placed.Crates is { } crates ? placed with { Crates = [.. SavedPlan.WithItems([mission], crates, c => c.Mission != mission.Id)] } : placed;
    }

    /// <summary>
    /// The rule of <see cref="PlaceInto"/> (D41, D64): each stop whole into a rack, tried as the D41 order has them (racks
    /// with the destination on board, the first rack with nothing on board, then rack order): the first where no new crate
    /// sits on other cargo, else the first that takes it. If none takes it, crate by crate, largest first, by the same
    /// choice over all racks.
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> StackingUnchecked) StopByStop(
        LoadPlan plan, Mission mission, Rack[] racks, MissionStop[] stops, CrateLayout.StackingBudget budget)
    {
        List<Crate> crates = [.. plan.Crates];
        List<Rack> stackingUnchecked = [];
        foreach (var stop in stops)
        {
            (CrateSize Size, string Commodity)[] kinds =
            [
                .. stop.Items.SelectMany(i => i.CrateList.Select(size => (size, i.Commodity)))
                    .OrderByDescending(k => k.size).ThenBy(k => k.Commodity, StringComparer.Ordinal),
            ];
            Rack[] candidates =
            [
                .. racks.Where(r => crates.Any(c => c.RackId == r.Id && !c.Delivered && c.Destination == stop.Destination))
                    .Concat(racks.Where(r => !crates.Any(c => c.RackId == r.Id && !c.Delivered)).Take(1))
                    .Concat(racks)
                    .Distinct(),
            ];

            List<Crate> fresh = [];
            if (Best(candidates.Select(r => Laid(r, stop.Destination, kinds, []))) is { } whole)
            {
                Take(whole);
            }
            else
            {
                foreach (var kind in kinds)
                {
                    if (Best(racks.Select(r => Laid(r, stop.Destination, [kind], fresh))) is not { } one)
                    {
                        return (null, kind.Size, []);
                    }

                    Take(one);
                }
            }

            Insert(crates, stop.Destination, fresh);

            void Take((Crate[] Crates, Rack Rack, CrateLayout.Laying Laying) laid)
            {
                fresh.AddRange(laid.Crates);
                if (laid.Laying.GaveUp && !stackingUnchecked.Contains(laid.Rack))
                {
                    stackingUnchecked.Add(laid.Rack);
                }
            }
        }

        return (crates, null, stackingUnchecked);

        // The kinds laid out in one rack around what lies there and the crates placed for this stop so far; null if they don't fit.
        (Crate[] Crates, Rack Rack, CrateLayout.Laying Laying)? Laid(
            Rack rack, string destination, (CrateSize Size, string Commodity)[] laying, IReadOnlyList<Crate> placedForStop)
        {
            Crate[] occupied = [.. crates.Concat(placedForStop).Where(c => c.RackId == rack.Id)];
            if (CrateLayout.Lay(rack, occupied, [.. laying.Select(k => (k.Size, destination))], plan.FillOrder, budget.For(rack)) is not { } laid)
            {
                return null;
            }

            budget.Spend(rack, laid);
            return ([.. laying.Zip(laid.Positions).Select(p => new Crate(destination, p.First.Commodity, p.First.Size, rack.Id, p.Second, Mission: mission.Id))],
                rack, laid);
        }
    }

    /// <summary>
    /// Of the layouts in try order, evaluated one by one: the first without a new crate on other cargo, else the first at
    /// all (D64); null if none fits.
    /// </summary>
    private static (Crate[] Crates, Rack Rack, CrateLayout.Laying Laying)? Best(IEnumerable<(Crate[] Crates, Rack Rack, CrateLayout.Laying Laying)?> tries)
    {
        (Crate[] Crates, Rack Rack, CrateLayout.Laying Laying)? first = null;
        foreach (var laid in tries)
        {
            if (laid is { Laying.StackingFree: true })
            {
                return laid;
            }

            first ??= laid;
        }

        return first;
    }

    /// <summary>
    /// The fallback of <see cref="TryPlace"/> (D54): every crate of the mission, largest first, into the first rack that
    /// takes it around the cargo on board, which stays (<see cref="FirstFitDecreasing"/>). Then each rack is laid out with
    /// all its new crates at once, and the crates go into the list stop by stop as <see cref="StopByStop"/> puts them.
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> StackingUnchecked) LargestFirst(
        LoadPlan plan, Mission mission, Rack[] racks, MissionStop[] stops, CrateLayout.StackingBudget budget)
    {
        (int Stop, CrateSize Size, string Commodity)[] all =
            [.. stops.SelectMany((stop, s) => stop.Items.SelectMany(item => item.CrateList.Select(size => (s, size, item.Commodity))))];
        var (rackOf, failed) = FirstFitDecreasing(racks, plan.Crates, [.. all.Select(k => (k.Size, stops[k.Stop].Destination))], plan.FillOrder);
        if (failed is { } crate)
        {
            return (null, all[crate].Size, []);
        }

        var laid = new Crate[all.Length];
        List<Rack> stackingUnchecked = [];
        for (var r = 0; r < racks.Length; r++)
        {
            int[] inRack = [.. Enumerable.Range(0, all.Length).Where(i => rackOf[i] == r)];
            if (inRack.Length == 0)
            {
                continue;
            }

            // The probe took these cells largest first around the same cargo, as Lay does, so they fit.
            var laying = CrateLayout.Lay(
                racks[r], [.. plan.Crates.Where(c => c.RackId == racks[r].Id)], [.. inRack.Select(i => (all[i].Size, stops[all[i].Stop].Destination))], plan.FillOrder,
                budget.For(racks[r]))!;
            budget.Spend(racks[r], laying);
            foreach (var (i, position) in inRack.Zip(laying.Positions))
            {
                laid[i] = new Crate(stops[all[i].Stop].Destination, all[i].Commodity, all[i].Size, racks[r].Id, position, Mission: mission.Id);
            }

            if (laying.GaveUp)
            {
                stackingUnchecked.Add(racks[r]);
            }
        }

        List<Crate> crates = [.. plan.Crates];
        for (var s = 0; s < stops.Length; s++)
        {
            Insert(crates, stops[s].Destination, [.. Enumerable.Range(0, all.Length).Where(i => all[i].Stop == s).Select(i => laid[i])]);
        }

        return (crates, null, stackingUnchecked);
    }

    /// <summary>
    /// The shape-aware split over the racks (D54): each crate, largest first (ties in input order, which the planner makes
    /// its own order, D64), into the first rack whose free cells still take it by the layout rule
    /// (<see cref="CrateLayout.RackSpace"/>), racks tried as <see cref="PlaceInto"/> tries them (D41): those holding cargo
    /// for its destination, the first with nothing on board, then all in rack order. <paramref name="onBoard"/> keep their
    /// cells. Every crate probes every rack at most once, so there is nothing to search. The rack index per crate; if a
    /// crate finds none, its index as <c>Failed</c>, and it and the crates not yet tried stay at -1.
    /// </summary>
    private static (int[] RackOf, int? Failed) FirstFitDecreasing(
        Rack[] racks, IReadOnlyList<Crate> onBoard, IReadOnlyList<(CrateSize Size, string Destination)> crates, FillOrder order)
    {
        var spaces = racks.Select(r => CrateLayout.RackSpace.Around(r, onBoard.Where(c => c.RackId == r.Id), order)).ToArray();
        var holding = racks.Select(r => onBoard.Where(c => c.RackId == r.Id && !c.Delivered).Select(c => c.Destination).ToHashSet()).ToArray();
        var rackOf = Enumerable.Repeat(-1, crates.Count).ToArray();

        // OrderByDescending is stable: crates of one size keep input order.
        foreach (var i in Enumerable.Range(0, crates.Count).OrderByDescending(i => crates[i].Size))
        {
            var (size, destination) = crates[i];
            int[] candidates =
            [
                .. Enumerable.Range(0, racks.Length).Where(r => holding[r].Contains(destination))
                    .Concat(Enumerable.Range(0, racks.Length).Where(r => holding[r].Count == 0).Take(1))
                    .Concat(Enumerable.Range(0, racks.Length))
                    .Distinct(),
            ];
            foreach (var r in candidates)
            {
                if (spaces[r].Add(size) is not null)
                {
                    rackOf[i] = r;
                    holding[r].Add(destination);
                    break;
                }
            }

            if (rackOf[i] < 0)
            {
                return (rackOf, i);
            }
        }

        return (rackOf, null);
    }

    /// <summary>
    /// Puts a stop's new crates behind the destination's last crate, so a destination's crates stay together in the list
    /// (legend, tool tips); a destination not in the plan goes to the end.
    /// </summary>
    private static void Insert(List<Crate> crates, string destination, IReadOnlyList<Crate> fresh) =>
        crates.InsertRange(crates.FindLastIndex(c => c.Destination == destination) is var last and >= 0 ? last + 1 : crates.Count, fresh);

    /// <summary>
    /// The plan's refusal when <see cref="FirstFitDecreasing"/> finds no rack for crate <paramref name="failed"/> of
    /// <paramref name="all"/> (D54), in the hauler's words: the destination, how many crates of that item are left over,
    /// and the SCU free in the ship, which are there, only not in that shape.
    /// </summary>
    private static PlanException NoGap(Ship ship, Stop[] stops, (int Stop, int Item, CrateSize Size)[] all, int[] rackOf, int failed)
    {
        var crate = all[failed];
        var stop = stops[crate.Stop];
        var left = Enumerable.Range(0, all.Length).Count(i => all[i].Stop == crate.Stop && all[i].Item == crate.Item && rackOf[i] < 0);
        var free = ship.TotalScu - stops.Sum(s => s.TotalScu);
        return new PlanException(Invariant(
            $"{stop.Destination} doesn't fit: no room for {left} × {(int)crate.Size} SCU crate{(left == 1 ? "" : "s")} of {stop.Items[crate.Item].Commodity} – {free} SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks."));
    }

    /// <summary>
    /// Every crate with a mission whose items hold its kind, in mission order (see the missions overload of Plan), and then
    /// with an item of it (<see cref="SavedPlan.WithItems"/>, D68).
    /// </summary>
    private static IReadOnlyList<Crate> Assigned(IReadOnlyList<Crate> crates, IReadOnlyList<Mission> missions)
    {
        var owners = missions
            .SelectMany(m => m.Stops.Select(s => s.ToStop()).SelectMany(s => s.Items.SelectMany(i =>
                Enumerable.Repeat((Key: (s.Destination, i.Commodity, i.CrateSize), m.Id), i.Scu / (int)i.CrateSize))))
            .GroupBy(o => o.Key)
            .ToDictionary(g => g.Key, g => new Queue<int>(g.Select(o => o.Id)));
        Crate[] owned = [.. crates.Select(c => c with { Mission = owners[(c.Destination, c.Commodity, c.CrateSize)].Dequeue() })];
        return SavedPlan.WithItems(missions, owned, _ => false);
    }

    /// <summary>
    /// Splits a crate the way the kiosk delivered it (D27): <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>
    /// with <see cref="CrateMix.DefaultSplit"/>, the two largest parts.
    /// </summary>
    /// <exception cref="ArgumentException">See the overload with parts.</exception>
    /// <exception cref="PlanException">See the overload with parts.</exception>
    public static SavedPlan SplitCrate(SavedPlan saved, Ship ship, Crate crate) =>
        SplitCrate(saved, ship, crate, CrateMix.DefaultSplit(crate.CrateSize));

    /// <summary>
    /// Splits a crate into <paramref name="parts"/> that lie in its footprint (D27): every cell stays taken, so what lies
    /// on and next to it keeps its support, and the hints stay. The parts take the crate's place in the plan, largest
    /// first; its contract item takes the new crates as an override (<see cref="MissionItem.WithCrateList"/>). The
    /// original stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, or the crate isn't part of the plan or is delivered.
    /// </exception>
    /// <exception cref="PlanException">The crate can't be split into these parts (<see cref="SplitProblem(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>).</exception>
    public static SavedPlan SplitCrate(SavedPlan saved, Ship ship, Crate crate, IReadOnlyList<CrateSize> parts) =>
        Split(saved, ship, crate, parts).Applied;

    /// <summary>Why <see cref="SplitCrate(SavedPlan, Ship, Crate)"/> would refuse; null if it wouldn't.</summary>
    /// <exception cref="ArgumentException">See <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>.</exception>
    public static string? SplitProblem(SavedPlan saved, Ship ship, Crate crate) =>
        SplitProblem(saved, ship, crate, CrateMix.DefaultSplit(crate.CrateSize));

    /// <summary>
    /// Why <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/> would refuse, or null if it
    /// wouldn't: a 1 SCU crate, parts that are no split of the crate or don't fit its footprint. One check for the
    /// overlay's button and the split itself, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>.</exception>
    public static string? SplitProblem(SavedPlan saved, Ship ship, Crate crate, IReadOnlyList<CrateSize> parts) =>
        Split(saved, ship, crate, parts).Problem;

    /// <summary>
    /// Merges crates into one, the way the kiosk delivered them (D27): one destination, one commodity, one rack, sizes
    /// adding up to a crate size the contract allows, and together exactly that crate's box. The new crate takes their
    /// cells, so support and hints stay, and the place of the first of them in the plan; their contract item takes the
    /// new crates (<see cref="MissionItem.WithCrateList"/>). The original stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, there are fewer than two crates, or one isn't part of the plan, is delivered or
    /// named twice.
    /// </exception>
    /// <exception cref="PlanException">The crates can't be merged (<see cref="MergeProblem"/>).</exception>
    public static SavedPlan MergeCrates(SavedPlan saved, Ship ship, IReadOnlyList<Crate> crates) =>
        Merge(saved, ship, crates).Applied;

    /// <summary>
    /// Why <see cref="MergeCrates"/> would refuse, or null if it wouldn't. One check for the overlay's button and the
    /// merge itself, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MergeCrates"/>.</exception>
    public static string? MergeProblem(SavedPlan saved, Ship ship, IReadOnlyList<Crate> crates) =>
        Merge(saved, ship, crates).Problem;

    /// <summary>
    /// Why the plan can't be loaded as it is, per rack in rack order (<see cref="CrateLayout.Problems"/>): crates that
    /// overlap, stick out, don't fit the grid or have nothing below them. The planner and its moves never make these; only
    /// a plan file edited by hand gets there (D24). Delivered crates are off the ship and not checked; one that carried
    /// another leaves it floating. A crate on another destination's cargo is no problem but a hint (D64): hints for the
    /// player (shared racks, split stops, stacking) are in <see cref="LoadPlan.Warnings"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or uses a rack the ship doesn't have.</exception>
    public static IReadOnlyList<PlanWarning> Validate(LoadPlan plan, Ship ship)
    {
        plan.EnsureBelongsTo(ship);
        if (plan.Crates.FirstOrDefault(c => !ship.Racks.Any(r => r.Id == c.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Crate for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(plan));
        }

        return [.. ship.Racks.OrderBy(r => r.Index)
            .SelectMany(rack => CrateLayout.Problems(rack, plan.CratesForRack(rack.Id)).Select(problem => new PlanWarning(rack.Id, problem)))];
    }

    /// <summary>
    /// The index of <paramref name="crate"/> in the plan, added to <paramref name="taken"/>; checks the caller's part: a crate
    /// of the plan, not delivered (D16), not named twice (<paramref name="verb"/> says how it was named).
    /// </summary>
    private static int IndexOnBoard(LoadPlan plan, Crate crate, HashSet<int> taken, string verb, string paramName)
    {
        var index = IndexOf(plan.Crates, crate);
        if (index < 0)
        {
            throw new ArgumentException(
                Invariant($"{(int)crate.CrateSize} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} in '{crate.RackId}' is not part of the plan."),
                paramName);
        }

        if (!taken.Add(index))
        {
            throw new ArgumentException(
                Invariant($"{(int)crate.CrateSize} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} {verb} more than once."),
                paramName);
        }

        EnsureOnBoard(crate.Delivered, crate.Commodity, crate.Destination, paramName);
        return index;
    }

    private static Edit Split(SavedPlan saved, Ship ship, Crate crate, IReadOnlyList<CrateSize> parts)
    {
        var plan = saved.Plan;
        plan.EnsureBelongsTo(ship);
        var index = IndexOnBoard(plan, crate, [], "is named", nameof(crate));
        var size = (int)crate.CrateSize;
        var sum = string.Join(" + ", parts.Select(p => (int)p));

        if (crate.CrateSize == CrateSize.One)
        {
            return Edit.Refused("1 SCU crates can't be split.");
        }

        if (parts.Count < 2)
        {
            return Edit.Refused(Invariant($"A {size} SCU crate splits into two or more crates."));
        }

        if (parts.Where(p => !Enum.IsDefined(p)).Select(p => (int?)p).FirstOrDefault() is { } unknown)
        {
            return Edit.Refused(Invariant($"{unknown} SCU is not a crate size."));
        }

        if (parts.Sum(p => (int)p) != size)
        {
            return Edit.Refused(Invariant($"{sum} SCU don't add up to {size} SCU."));
        }

        var rack = RackOf(ship, crate.RackId, nameof(crate));
        if (CrateLayout.PlaceWithin(rack, crate, parts, plan.FillOrder) is not { } positions)
        {
            return Edit.Refused(Invariant($"{sum} SCU don't fit the footprint of the {size} SCU crate ({crate.CrateSize.Dimensions})."));
        }

        // Largest first, as the missions expand them (MissionItem.ToCargoItems). OrderByDescending is stable.
        // The parts keep the crate's mission (D42) and item (D68).
        Crate[] pieces = [.. parts.Zip(positions)
            .OrderByDescending(p => p.First)
            .Select(p => new Crate(crate.Destination, crate.Commodity, p.First, crate.RackId, p.Second, Mission: crate.Mission, Item: crate.Item))];
        List<Crate> crates = [.. plan.Crates];
        crates.RemoveAt(index);
        crates.InsertRange(index, pieces);

        // The crate's item holds it (SavedPlan checks that).
        return new Edit(
            new SavedPlan(Rebooked(saved.Missions, saved.MissionOf(crate.Mission), crate.Item, [crate.CrateSize], parts), WithHints(plan with { Crates = crates }, ship), saved.Watermark),
            null);
    }

    private static Edit Merge(SavedPlan saved, Ship ship, IReadOnlyList<Crate> crates)
    {
        var plan = saved.Plan;
        plan.EnsureBelongsTo(ship);
        if (crates.Count < 2)
        {
            throw new ArgumentException("A merge takes two or more crates.", nameof(crates));
        }

        HashSet<int> taken = [];
        int[] indices = [.. crates.Select(c => IndexOnBoard(plan, c, taken, "is named", nameof(crates)))];
        var first = crates[0];

        if (crates.Any(c => c.Destination != first.Destination))
        {
            return Edit.Refused("The crates go to different destinations.");
        }

        if (crates.Any(c => c.Commodity != first.Commodity))
        {
            return Edit.Refused("The crates hold different commodities.");
        }

        if (crates.Any(c => c.RackId != first.RackId))
        {
            return Edit.Refused("The crates lie in different racks.");
        }

        // Crates of two contracts stay two contracts' crates (D42).
        if (crates.Any(c => c.Mission != first.Mission))
        {
            return Edit.Refused("The crates belong to different missions.");
        }

        var merged = (CrateSize)crates.Sum(c => (int)c.CrateSize);
        if (!Enum.IsDefined(merged))
        {
            return Edit.Refused(Invariant($"{string.Join(" + ", crates.Select(c => (int)c.CrateSize).OrderDescending())} is not a crate size."));
        }

        // One item: its SCU is contract data and must not move to another item, not even one of the same kind (D68).
        if (crates.Any(c => c.Item != first.Item))
        {
            return Edit.Refused("The crates belong to different contract items.");
        }

        // The kiosk never delivers a crate above the max crate size (D23).
        var mission = saved.MissionOf(first.Mission);
        if (mission.Items[first.Item].Item.MaxCrateSize is var max && max < merged)
        {
            return Edit.Refused(Invariant($"{(int)merged} SCU crates are larger than the max crate size of {(int)max} SCU for {first.Commodity}."));
        }

        // The game's limit per rack (D56): the block may have the shape, the rack still refuses the size.
        if (RackOf(ship, first.RackId, nameof(crates)) is { MaxCrateSize: { } rackMax } rack && merged > rackMax)
        {
            return Edit.Refused(Invariant($"Rack {rack.Number} takes crates up to {(int)rackMax} SCU."));
        }

        if (CrateLayout.BlockProblem(crates, merged) is { } problem)
        {
            return Edit.Refused(problem);
        }

        // The new crate takes the place of the first of them in the plan; the others go, from the back.
        GridPosition corner = new(crates.Min(c => c.Position.Length), crates.Min(c => c.Position.Width), crates.Min(c => c.Position.Stack));
        List<Crate> list = [.. plan.Crates];
        list[indices.Min()] = new Crate(first.Destination, first.Commodity, merged, first.RackId, corner, Mission: first.Mission, Item: first.Item);
        foreach (var index in indices.Order().Skip(1).Reverse())
        {
            list.RemoveAt(index);
        }

        return new Edit(
            new SavedPlan(Rebooked(saved.Missions, mission, first.Item, [.. crates.Select(c => c.CrateSize)], [merged]), WithHints(plan with { Crates = list }, ship), saved.Watermark),
            null);
    }

    /// <summary>
    /// The missions with item <paramref name="item"/> (<see cref="Mission.Items"/>) of <paramref name="mission"/> holding
    /// <paramref name="added"/> instead of <paramref name="removed"/>.
    /// </summary>
    private static IReadOnlyList<Mission> Rebooked(
        IReadOnlyList<Mission> missions, Mission mission, int item, IReadOnlyList<CrateSize> removed, IReadOnlyList<CrateSize> added)
    {
        var contractItem = mission.Items[item].Item;
        List<CrateSize> crates = [.. contractItem.CrateList];
        foreach (var size in removed)
        {
            crates.Remove(size);
        }

        var rebooked = mission.WithItem(item, contractItem.WithCrateList([.. crates, .. added]));
        return [.. missions.Select(m => m.Id == mission.Id ? rebooked : m)];
    }

    /// <summary>A split or merge: the saved plan it gives, or why it is refused.</summary>
    private sealed record Edit(SavedPlan? Result, string? Problem)
    {
        public static Edit Refused(string problem) => new(null, problem);

        public SavedPlan Applied => Result ?? throw new PlanException(Problem ?? "");
    }

    private static int IndexOf(IReadOnlyList<Crate> crates, Crate crate)
    {
        for (var i = 0; i < crates.Count; i++)
        {
            if (crates[i] == crate)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The plan with its hints derived anew from the placements, and the notes it carries plus <paramref name="added"/>
    /// (<see cref="Notes"/>).
    /// </summary>
    internal static LoadPlan WithHints(LoadPlan plan, Ship ship, IEnumerable<PlanWarning>? added = null)
    {
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        return plan with { Warnings = [.. Hints(racks, plan), .. Notes(plan, racks, added ?? [])] };
    }

    /// <summary>
    /// The planner's notes, in rack order, each once: those <paramref name="plan"/> carries and <paramref name="added"/>. A
    /// note says what the crates can't: that the search for a layout without foreign stacking gave up in a rack
    /// (<see cref="CrateLayout.Lay"/>). So it is carried, not derived, until the next Plan starts without it (D54). Notes
    /// of older plans (D54's "Racks not in route order", the old wording of this one) aren't known any more and drop (D64).
    /// </summary>
    private static IEnumerable<PlanWarning> Notes(LoadPlan plan, Rack[] racks, IEnumerable<PlanWarning> added)
    {
        HashSet<PlanWarning> present = [.. added, .. plan.Warnings];
        return racks.Select(r => new PlanWarning(r.Id, StackingUncheckedNote(r))).Where(present.Contains);
    }

    internal static IEnumerable<PlanWarning> StackingUncheckedNotes(IEnumerable<Rack> racks) =>
        racks.Select(r => new PlanWarning(r.Id, StackingUncheckedNote(r)));

    private static string StackingUncheckedNote(Rack rack) => Invariant(
        $"Rack {rack.Number}: too many crates to rearrange – a layout without crates on other cargo may exist.");

    private static Rack RackOf(Ship ship, string rackId, string paramName) =>
        ship.Racks.FirstOrDefault(r => r.Id == rackId)
        ?? throw new ArgumentException($"Ship '{ship.Id}' has no rack '{rackId}'.", paramName);

    private static void EnsureStop(LoadPlan plan, string destination)
    {
        if (!plan.Crates.Any(c => c.Destination == destination))
        {
            throw new ArgumentException($"The plan has no stop '{destination}'.", nameof(destination));
        }
    }

    /// <summary>Delivered cargo is off the ship (D16): it can't be moved.</summary>
    private static void EnsureOnBoard(bool delivered, string commodity, string destination, string paramName)
    {
        if (delivered)
        {
            throw new ArgumentException($"{commodity} for {destination} is delivered, its crates are off the ship.", paramName);
        }
    }

    /// <summary>
    /// What the player has to watch at the cargo lift, derived from the crates alone: every rack holding more than one
    /// destination, every rack holding part of a destination that spans racks, and every crate whose support chain holds
    /// another destination's cargo (<see cref="CrateLayout.StackedOn"/>, D64): physical facts, no route. Delivered cargo
    /// is off the ship and doesn't count (D16). Destinations in the order of the crate list. <paramref name="racks"/> must
    /// be in rack order.
    /// </summary>
    private static List<PlanWarning> Hints(Rack[] racks, LoadPlan plan)
    {
        Placement[] placements = [.. plan.Placements.Where(p => !p.Delivered)];
        var racksOfDestination = placements
            .GroupBy(p => p.Destination)
            .ToDictionary(g => g.Key, g => racks.Where(r => g.Any(p => p.RackId == r.Id)).ToArray());

        List<PlanWarning> warnings = [];
        foreach (var rack in racks)
        {
            string[] inRack = [.. placements.Where(p => p.RackId == rack.Id).Select(p => p.Destination).Distinct()];
            if (inRack.Length > 1)
            {
                warnings.Add(new PlanWarning(rack.Id, Invariant(
                    $"Rack {rack.Number} shared by {string.Join(", ", inRack)} – identify crates by count")));
            }

            // Same message in every spanned rack, so the overview can show it once.
            foreach (var destination in inRack)
            {
                var spanned = racksOfDestination[destination];
                if (spanned.Length > 1)
                {
                    warnings.Add(new PlanWarning(rack.Id, Invariant(
                        $"{destination} spans racks {string.Join("+", spanned.Select(r => r.Number))}")));
                }
            }

            foreach (var (upper, lower) in CrateLayout.StackedOn(rack, plan.CratesForRack(rack.Id)))
            {
                warnings.Add(new PlanWarning(rack.Id, Invariant($"Rack {rack.Number}: crate for {upper} sits on cargo for {lower}")));
            }
        }

        return warnings;
    }

    private static void EnsureUnique(IReadOnlyList<Stop> stops)
    {
        if (stops.GroupBy(s => s.Order).FirstOrDefault(g => g.Count() > 1) is { } order)
        {
            throw new ArgumentException(Invariant($"Order {order.Key} is used by more than one stop."), nameof(stops));
        }

        // Case-insensitive like the mission file: "Riker" and "riker" are the same cargo lift.
        if (stops.GroupBy(s => s.Destination, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } destination)
        {
            throw new ArgumentException($"Destination '{destination.Key}' is used by more than one stop.", nameof(stops));
        }
    }

    /// <summary>
    /// Names the first crate, in the order of <paramref name="stops"/>, whose shape or size no rack takes
    /// (<see cref="Rack.CanHold"/>). Checked per crate, not per mix: for Hull A every mix up to the rack capacity fits (D9).
    /// </summary>
    private static void EnsureCratesFitShip(Ship ship, Stop[] stops)
    {
        foreach (var stop in stops)
        {
            if (stop.Items.FirstOrDefault(i => !ship.Racks.Any(r => r.CanHold(i.CrateSize))) is { } item)
            {
                throw new PlanException(Invariant(
                    $"{(int)item.CrateSize} SCU crate of {item.Commodity} for {stop.Destination} doesn't fit any rack of {ship.Id} ({WhyNoRack(ship, item.CrateSize)})."));
            }
        }
    }

    /// <summary>
    /// Why no rack of <paramref name="ship"/> takes the size: if some rack's grid would hold it, the max crate size stops
    /// it there (D56), and the largest of those limits is named; else the largest rack's grid.
    /// </summary>
    private static string WhyNoRack(Ship ship, CrateSize size) =>
        // Max skips nulls and is null for none; a rack whose grid holds the size and has no limit would take it.
        ship.Racks.Where(r => r.FitsGrid(size)).Max(r => r.MaxCrateSize) is { } max
            ? Invariant($"racks take crates up to {(int)max} SCU")
            // MaxBy keeps the first rack on a tie.
            : Invariant($"largest rack {ship.Racks.MaxBy(r => r.Grid.Volume)?.Grid}");

    /// <summary>
    /// Refuses stops that need more SCU than the ship holds. Names no stop: without a route none of them is the one that
    /// doesn't fit (D64).
    /// </summary>
    private static void EnsureFitsShip(Ship ship, Stop[] stops)
    {
        var needed = stops.Sum(s => s.TotalScu);
        if (needed > ship.TotalScu)
        {
            throw new PlanException(Invariant(
                $"Ship capacity exceeded: {needed - ship.TotalScu} SCU missing (stops need {needed} SCU, ship '{ship.Id}' holds {ship.TotalScu} SCU)."));
        }
    }
}
