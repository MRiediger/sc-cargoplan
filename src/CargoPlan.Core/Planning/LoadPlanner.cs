using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using static System.FormattableString;

using Score = (int Splits, int ItemCuts, int Squares);

namespace CargoPlan.Core.Planning;

/// <summary>Decides which rack each stop of a route goes into, and where its crates lie there (D24).</summary>
public static class LoadPlanner
{
    /// <summary>
    /// Plans the stops into the ship's racks. Racks fill in rack order (Rack 1 first) in route order, one rack
    /// per stop while racks last. With more stops than racks, only route-neighbours share a rack, spread evenly.
    /// A stop is split over neighbouring racks only if no plan without a split exists, preferably between
    /// its items. Every shared rack and every rack holding part of a split stop gets a warning. Then each rack's
    /// crates are laid out in <paramref name="fillOrder"/> (<see cref="CrateLayout.LayOut"/>), stored with the plan.
    /// If that split doesn't fit a rack's grid (D9 holds for the Hull A only), every crate goes largest first into the
    /// first rack that takes it (<see cref="FirstFitDecreasing"/>), and every rack with cargo notes it (D54).
    /// </summary>
    /// <exception cref="ArgumentException">Two stops share an order or a destination.</exception>
    /// <exception cref="PlanException">
    /// A crate's shape fits no rack, the stops exceed the ship, their crates don't fit in route order, or a crate finds no
    /// gap of its shape in any rack.
    /// </exception>
    public static LoadPlan Plan(Ship ship, IReadOnlyList<Stop> stops, FillOrder fillOrder = FillOrder.BowFirst)
    {
        EnsureUnique(stops);

        Stop[] route = [.. stops.OrderBy(s => s.Order)];
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        EnsureCratesFitShip(ship, route);
        EnsureFitsShip(ship, route);

        var line = CrateLine.Of(route);
        if (line.Length == 0)
        {
            return new LoadPlan(ship.Id, [], [], fillOrder);
        }

        var segments = Partition(racks, line);

        // Crates of one item lie next to each other, so each item becomes one placement per rack.
        List<Placement> placements = [.. segments.SelectMany(segment => line.Crates[segment.Start..segment.End]
            .GroupBy(c => (c.Stop, c.Item))
            .Select(g => Place(line.Stops[g.Key.Stop], g.Key.Item, segment.Rack, g.Sum(c => c.Scu))))];

        if (CrateLayout.TryLayOut(ship, placements, fillOrder) is { } layout)
        {
            return WithHints(new LoadPlan(ship.Id, layout.Crates, [], fillOrder), ship, OrderUncheckedNotes(layout.OrderUnchecked));
        }

        var (rackOf, failed) = FirstFitDecreasing(racks, [], [.. line.Crates.Select(c => ((CrateSize)c.Scu, line.Stops[c.Stop].Destination))], fillOrder);
        if (failed is { } crate)
        {
            throw NoGap(ship, line, rackOf, crate);
        }

        // Placements in route order, so the route stays the order of first appearance. Per rack the crates are the ones
        // the probe took largest first, and the layout lays them largest first again: they fit.
        List<Placement> shaped = [.. Enumerable.Range(0, line.Length)
            .GroupBy(i => (line.Crates[i].Stop, line.Crates[i].Item, Rack: rackOf[i]))
            .OrderBy(g => g.Key.Stop).ThenBy(g => g.Key.Item).ThenBy(g => g.Key.Rack)
            .Select(g => Place(line.Stops[g.Key.Stop], g.Key.Item, racks[g.Key.Rack], g.Sum(i => line.Crates[i].Scu)))];
        var fallback = CrateLayout.LayOutShip(ship, shaped, fillOrder);
        return WithHints(new LoadPlan(ship.Id, fallback.Crates, [], fillOrder), ship,
            [.. racks.Where(r => fallback.Crates.Any(c => c.RackId == r.Id)).Select(r => new PlanWarning(r.Id, FallbackNote)),
                .. OrderUncheckedNotes(fallback.OrderUnchecked)]);
    }

    /// <summary>
    /// Puts one crate at a cell of a rack, as the player sees it at the cargo lift (D24): <see cref="MoveCrates"/> with one move.
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    /// <exception cref="PlanException">See <see cref="MoveCrates"/>.</exception>
    public static LoadPlan MoveCrate(LoadPlan plan, Ship ship, Crate crate, string targetRackId, GridPosition targetPosition) =>
        MoveCrates(plan, ship, [new CrateMove(crate, targetRackId, targetPosition)]);

    /// <summary>
    /// Puts crates at cells of racks, all at once (D25): every crate leaves its cells, then all take their new ones, so
    /// crates of one move may carry each other. Each must fit its grid, find its cells free and rest on the plate or
    /// other crates; no crate may lose its support. Nothing slides down: what doesn't hold is refused with the reason
    /// of <see cref="MoveProblem"/>, the player sees what they do. Crates keep their index, so placements keep their
    /// order; hints are derived anew. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, the ship has no such rack, or a crate isn't part of the plan, is delivered or
    /// moves twice.
    /// </exception>
    /// <exception cref="PlanException">A crate doesn't fit where it goes, or another crate would lose its support.</exception>
    public static LoadPlan MoveCrates(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves)
    {
        var crates = Moved(plan, ship, moves);
        if (moves.Count == 0)
        {
            return plan;
        }

        return Problem(plan, ship, moves, crates) is { } problem
            ? throw new PlanException(problem)
            : WithHints(plan with { Crates = crates }, ship);
    }

    /// <summary>
    /// Why <see cref="MoveCrates"/> would refuse these moves, or null if it wouldn't: the first new problem of
    /// <see cref="CrateLayout.NewProblem"/> in the racks the crates leave or enter, in rack order. One check for the
    /// overlay's preview while dragging and for the drop, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    public static string? MoveProblem(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves) =>
        Problem(plan, ship, moves, Moved(plan, ship, moves));

    /// <summary>
    /// Moves every crate of a stop still on board to another rack and lays them out there around the crates it holds,
    /// split parts included (D24); the overlay's drag of a legend line (D25). Delivered cargo is off the ship and stays,
    /// so a delivered stop moves nothing; a stop whose crates on board all lie in the target rack already stays as it is,
    /// it isn't laid out anew. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, has no stop <paramref name="destination"/>, or the ship has no such rack.
    /// </exception>
    /// <exception cref="PlanException">The crates don't fit the free space of the target rack.</exception>
    public static LoadPlan MoveStop(LoadPlan plan, Ship ship, string destination, string targetRackId)
    {
        plan.EnsureBelongsTo(ship);
        var target = RackOf(ship, targetRackId, nameof(targetRackId));
        EnsureStop(plan, destination);

        return Relayout(plan, ship, target, c => c.Destination == destination && !c.Delivered, destination);
    }

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

        var changed = plan with { Crates = [.. plan.Crates.Select(c => c.Destination == destination ? c with { Delivered = delivered } : c)] };
        if (!delivered)
        {
            HashSet<string> racks = [.. plan.Crates.Where(c => c.Destination == destination).Select(c => c.RackId)];
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
    /// The hints the plan would have with every stop on board, without checking cells: the legend keeps their space, so
    /// ticking never resizes the ship view (D16). Ticking back may be refused (<see cref="SetDelivered"/>); this never is.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static IReadOnlyList<PlanWarning> HintsOnBoard(LoadPlan plan, Ship ship)
    {
        plan.EnsureBelongsTo(ship);
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        return [.. Hints(racks, (plan with { Crates = [.. plan.Crates.Select(c => c with { Delivered = false })] }).Placements), .. Notes(plan, racks, [])];
    }

    /// <summary>
    /// Plans missions from scratch, as the editor's Plan does (D19): their stops merged per destination in
    /// <paramref name="route"/> order go through <see cref="Plan(Ship, IReadOnlyList{Stop}, FillOrder)"/>, then every crate
    /// gets a mission that holds its kind. Equal crates of two missions can't be told apart in the game (D38), so which of
    /// them goes to which is no choice. Every mission is placed afterwards; ids, sources and the watermark stay.
    /// </summary>
    /// <param name="route">Every destination of the missions once, in the order the player entered them.</param>
    /// <exception cref="ArgumentException">The route doesn't name every destination once, or see the other overload.</exception>
    /// <exception cref="PlanException">See the other overload.</exception>
    public static SavedPlan Plan(
        Ship ship, IReadOnlyList<Mission> missions, IReadOnlyList<string> route, FillOrder fillOrder = FillOrder.BowFirst, long watermark = 0)
    {
        Mission[] placed = [.. missions.Select(m => m with { Unplaced = false })];
        var items = placed.SelectMany(m => m.Stops)
            .GroupBy(s => s.Destination)
            .ToDictionary(g => g.Key, g => g.SelectMany(s => s.Items).ToArray());
        if (route.Count != items.Count || route.Distinct().Count() != route.Count || !route.All(items.ContainsKey))
        {
            throw new ArgumentException("The route has to name every destination of the missions once.", nameof(route));
        }

        var plan = Plan(ship, [.. route.Select((destination, index) => new MissionStop(destination, index + 1, items[destination]).ToStop())], fillOrder);
        return new SavedPlan(placed, plan with { Crates = Assigned(plan.Crates, placed) }, watermark);
    }

    /// <summary>
    /// Adds a mission to the plan without moving a crate (D41): positions and ticks stay. Each of its stops goes, whole,
    /// into the first rack that takes it: racks with cargo on board for the same destination (one handover, D42), then
    /// the first rack with nothing on board, then the first with room; otherwise crate by crate in rack order. Laid out
    /// by <see cref="CrateLayout.Place"/>, so delivered cells are free and the stacking rule is tried. Crates for a
    /// destination on board go right behind its crates and keep its place in <see cref="LoadPlan.UnloadOrder"/>; others go
    /// to the end, a new visit. If a crate finds no place, the mission comes in without crates (<see cref="Mission.Unplaced"/>):
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

        return TryPlace(saved.Plan, ship, mission) is ({ } crates, _, var orderUnchecked)
            ? new SavedPlan([.. saved.Missions, mission], WithHints(saved.Plan with { Crates = crates }, ship, OrderUncheckedNotes(orderUnchecked)), saved.Watermark)
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

        var free = ship.TotalScu - saved.Plan.Crates.Where(c => !c.Delivered).Sum(c => (int)c.CrateSize);
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
            if (TryPlace(current.Plan, ship, waiting) is ({ } crates, _, var orderUnchecked))
            {
                current = new SavedPlan(
                    [.. current.Missions.Select(m => m.Id == waiting.Id ? m with { Unplaced = false } : m)],
                    WithHints(current.Plan with { Crates = crates }, ship, OrderUncheckedNotes(orderUnchecked)),
                    current.Watermark);
            }
        }

        return current;
    }

    /// <summary>
    /// The plan on another ship (D59): its missions planned anew in the order the editor shows them
    /// (<see cref="SavedPlan.Stops"/>), as the editor's Plan would; if that fails, mission by mission into the empty ship
    /// in mission order (<see cref="RetryUnplaced"/>), the rest waits unplaced (D41). Every stop ticked as a whole gets its
    /// tick back. Missions, sources, watermark and fill order stay; positions and moves go, split crates keep their sizes
    /// (they are in the missions, D27). The only method that takes a plan of another ship.
    /// </summary>
    public static SavedPlan Replan(SavedPlan saved, Ship ship)
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
        Crate[] crates = [.. saved.Plan.Crates.Select(c => c.Destination == from ? c with { Destination = to } : c)];
        return new SavedPlan(missions, WithHints(saved.Plan with { Crates = crates }, ship), saved.Watermark);
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
    /// or, if a crate has no place, the size of the first that hasn't. Stop by stop first; if a crate finds no place there,
    /// the whole mission largest first (<see cref="LargestFirst"/>, D54), and its failure is the one named. With the crates,
    /// the racks whose search for the route order gave up (<see cref="CrateLayout.Lay"/>).
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> OrderUnchecked) TryPlace(LoadPlan plan, Ship ship, Mission mission)
    {
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        MissionStop[] stops = [.. mission.Stops.OrderBy(s => s.Order)];

        // The order after placing: destinations on board keep their place, the mission's others follow in contract order.
        HashSet<string> onBoard = [.. plan.Crates.Where(c => !c.Delivered).Select(c => c.Destination)];
        string[] route = [.. plan.UnloadOrder.Where(onBoard.Contains), .. stops.Select(s => s.Destination).Where(d => !onBoard.Contains(d))];

        return StopByStop(plan, mission, racks, stops, route) is { Crates: not null } placed
            ? placed
            : LargestFirst(plan, mission, racks, stops, route);
    }

    /// <summary>
    /// The rule of <see cref="PlaceInto"/> (D41): each stop whole into the first rack that takes it, else crate by crate,
    /// largest first, into the first rack with room.
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> OrderUnchecked) StopByStop(
        LoadPlan plan, Mission mission, Rack[] racks, MissionStop[] stops, string[] route)
    {
        List<Crate> crates = [.. plan.Crates];
        List<Rack> orderUnchecked = [];
        foreach (var stop in stops)
        {
            (CrateSize Size, string Commodity)[] kinds = [.. stop.Items.SelectMany(i => i.CrateList.Select(size => (size, i.Commodity)))];
            Rack[] candidates =
            [
                .. racks.Where(r => crates.Any(c => c.RackId == r.Id && !c.Delivered && c.Destination == stop.Destination))
                    .Concat(racks.Where(r => !crates.Any(c => c.RackId == r.Id && !c.Delivered)).Take(1))
                    .Concat(racks)
                    .Distinct(),
            ];

            List<Crate> fresh = [];
            if (candidates.Select(r => Laid(r, stop.Destination, kinds, [])).FirstOrDefault(laid => laid is not null) is { } whole)
            {
                Take(whole);
            }
            else
            {
                // OrderByDescending is stable: crates of one size keep item order.
                foreach (var kind in kinds.OrderByDescending(k => k.Size))
                {
                    if (racks.Select(r => Laid(r, stop.Destination, [kind], fresh)).FirstOrDefault(laid => laid is not null) is not { } one)
                    {
                        return (null, kind.Size, []);
                    }

                    Take(one);
                }
            }

            Insert(crates, stop.Destination, fresh);

            void Take((Crate[] Crates, Rack Rack, bool OrderUnchecked)? laid)
            {
                fresh.AddRange(laid!.Value.Crates);
                if (laid.Value.OrderUnchecked && !orderUnchecked.Contains(laid.Value.Rack))
                {
                    orderUnchecked.Add(laid.Value.Rack);
                }
            }
        }

        return (crates, null, orderUnchecked);

        // The kinds laid out in one rack around what lies there and the crates placed for this stop so far; null if they don't fit.
        (Crate[] Crates, Rack Rack, bool OrderUnchecked)? Laid(
            Rack rack, string destination, (CrateSize Size, string Commodity)[] laying, IReadOnlyList<Crate> placedForStop)
        {
            Crate[] occupied = [.. crates.Concat(placedForStop).Where(c => c.RackId == rack.Id)];
            return CrateLayout.Lay(rack, occupied, [.. laying.Select(k => (k.Size, destination))], route, plan.FillOrder) is { } laid
                ? ([.. laying.Zip(laid.Positions).Select(p => new Crate(destination, p.First.Commodity, p.First.Size, rack.Id, p.Second, Mission: mission.Id))],
                    rack, laid.OrderUnchecked)
                : null;
        }
    }

    /// <summary>
    /// The fallback of <see cref="TryPlace"/> (D54): every crate of the mission, largest first, into the first rack that
    /// takes it around the cargo on board, which stays (<see cref="FirstFitDecreasing"/>). Then each rack is laid out with
    /// all its new crates at once, and the crates go into the list stop by stop as <see cref="StopByStop"/> puts them.
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> OrderUnchecked) LargestFirst(
        LoadPlan plan, Mission mission, Rack[] racks, MissionStop[] stops, string[] route)
    {
        (int Stop, CrateSize Size, string Commodity)[] all =
            [.. stops.SelectMany((stop, s) => stop.Items.SelectMany(item => item.CrateList.Select(size => (s, size, item.Commodity))))];
        var (rackOf, failed) = FirstFitDecreasing(racks, plan.Crates, [.. all.Select(k => (k.Size, stops[k.Stop].Destination))], plan.FillOrder);
        if (failed is { } crate)
        {
            return (null, all[crate].Size, []);
        }

        var laid = new Crate[all.Length];
        List<Rack> orderUnchecked = [];
        for (var r = 0; r < racks.Length; r++)
        {
            int[] inRack = [.. Enumerable.Range(0, all.Length).Where(i => rackOf[i] == r)];
            if (inRack.Length == 0)
            {
                continue;
            }

            // The probe took these cells largest first around the same cargo, as Lay does, so they fit.
            var laying = CrateLayout.Lay(
                racks[r], [.. plan.Crates.Where(c => c.RackId == racks[r].Id)], [.. inRack.Select(i => (all[i].Size, stops[all[i].Stop].Destination))], route, plan.FillOrder)!;
            foreach (var (i, position) in inRack.Zip(laying.Positions))
            {
                laid[i] = new Crate(stops[all[i].Stop].Destination, all[i].Commodity, all[i].Size, racks[r].Id, position, Mission: mission.Id);
            }

            if (laying.OrderUnchecked)
            {
                orderUnchecked.Add(racks[r]);
            }
        }

        List<Crate> crates = [.. plan.Crates];
        for (var s = 0; s < stops.Length; s++)
        {
            Insert(crates, stops[s].Destination, [.. Enumerable.Range(0, all.Length).Where(i => all[i].Stop == s).Select(i => laid[i])]);
        }

        return (crates, null, orderUnchecked);
    }

    /// <summary>
    /// The shape-aware split over the racks (D54): each crate, largest first (ties in input order), into the first rack
    /// whose free cells still take it by the layout rule (<see cref="CrateLayout.RackSpace"/>), racks tried as
    /// <see cref="PlaceInto"/> tries them (D41): those holding cargo for its destination, the first with nothing on board,
    /// then all in rack order. <paramref name="onBoard"/> keep their cells. Every crate probes every rack at most once, so
    /// there is nothing to search. The rack index per crate; if a crate finds none, its index as <c>Failed</c>, and it and
    /// the crates not yet tried stay at -1.
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

    /// <summary>Puts a stop's new crates behind the destination's crates if it is on board (one handover), else at the end (a new visit).</summary>
    private static void Insert(List<Crate> crates, string destination, IReadOnlyList<Crate> fresh)
    {
        var at = crates.Any(c => c.Destination == destination && !c.Delivered)
            ? crates.FindLastIndex(c => c.Destination == destination) + 1
            : crates.Count;
        crates.InsertRange(at, fresh);
    }

    /// <summary>
    /// The plan's refusal when <see cref="FirstFitDecreasing"/> finds no rack for crate <paramref name="failed"/> of the line
    /// (D54), in the hauler's words: the destination, how many crates of that item are left over, and the SCU free in the
    /// ship, which are there, only not in that shape.
    /// </summary>
    private static PlanException NoGap(Ship ship, CrateLine line, int[] rackOf, int failed)
    {
        var crate = line.Crates[failed];
        var stop = line.Stops[crate.Stop];
        var left = Enumerable.Range(0, line.Length).Count(i => line.Crates[i].Stop == crate.Stop && line.Crates[i].Item == crate.Item && rackOf[i] < 0);
        var free = ship.TotalScu - line.ScuBefore[line.Length];
        return new PlanException(Invariant(
            $"{stop.Destination} doesn't fit: no room for {left} × {crate.Scu} SCU crate{(left == 1 ? "" : "s")} of {stop.Items[crate.Item].Commodity} – {free} SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks."));
    }

    /// <summary>Every crate with a mission whose items hold its kind, in mission order (see the missions overload of Plan).</summary>
    private static IReadOnlyList<Crate> Assigned(IReadOnlyList<Crate> crates, IReadOnlyList<Mission> missions)
    {
        var owners = missions
            .SelectMany(m => m.Stops.Select(s => s.ToStop()).SelectMany(s => s.Items.SelectMany(i =>
                Enumerable.Repeat((Key: (s.Destination, i.Commodity, i.CrateSize), m.Id), i.Scu / (int)i.CrateSize))))
            .GroupBy(o => o.Key)
            .ToDictionary(g => g.Key, g => new Queue<int>(g.Select(o => o.Id)));
        return [.. crates.Select(c => c with { Mission = owners[(c.Destination, c.Commodity, c.CrateSize)].Dequeue() })];
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
    /// overlap, stick out, don't fit the grid or have nothing below them, and crates lying on cargo of an earlier stop.
    /// The planner and its moves never make the first kind; only a plan file
    /// edited by hand gets there (D24). The order comes where a rack has no layout in route order or the player put a
    /// crate there (D24 addendum). Delivered crates are off the ship and not checked; one that carried another leaves it
    /// floating. Hints for the player (shared racks, split stops) are in <see cref="LoadPlan.Warnings"/>.
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

        var route = plan.UnloadOrder;
        return [.. ship.Racks.OrderBy(r => r.Index)
            .SelectMany(rack => CrateLayout.Problems(rack, plan.CratesForRack(rack.Id), route).Select(problem => new PlanWarning(rack.Id, problem)))];
    }

    /// <summary>
    /// Takes the crates <paramref name="moving"/> picks out of the plan and lays them out in <paramref name="target"/>
    /// around the crates that stay there. Moved crates keep their index in the crate list, so placements keep their order.
    /// Nothing to move, or everything already in the target rack: the plan as it is.
    /// </summary>
    private static LoadPlan Relayout(LoadPlan plan, Ship ship, Rack target, Func<Crate, bool> moving, string what)
    {
        int[] moved = [.. Enumerable.Range(0, plan.Crates.Count).Where(i => moving(plan.Crates[i]))];
        if (moved.All(i => plan.Crates[i].RackId == target.Id))
        {
            return plan;
        }

        Crate[] staying = [.. plan.Crates.Where((c, i) => c.RackId == target.Id && !moved.Contains(i))];
        var laid = CrateLayout.Lay(
                target, staying, [.. moved.Select(i => (plan.Crates[i].CrateSize, plan.Crates[i].Destination))], plan.UnloadOrder, plan.FillOrder)
            ?? throw new PlanException(Invariant(
                $"{what} doesn't fit rack {target.Number}: {staying.Sum(c => (int)c.CrateSize)}/{target.CapacityScu} SCU taken."));

        List<Crate> crates = [.. plan.Crates];
        foreach (var (index, position) in moved.Zip(laid.Positions))
        {
            crates[index] = crates[index] with { RackId = target.Id, Position = position };
        }

        return WithHints(plan with { Crates = crates }, ship, laid.OrderUnchecked ? OrderUncheckedNotes([target]) : []);
    }

    /// <summary>
    /// The plan's crates with every move applied at the crate's index; checks the caller's part: ship, racks, crates of
    /// the plan, none delivered or moved twice.
    /// </summary>
    private static List<Crate> Moved(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves)
    {
        plan.EnsureBelongsTo(ship);
        List<Crate> crates = [.. plan.Crates];
        HashSet<int> taken = [];
        foreach (var (crate, rackId, position) in moves)
        {
            RackOf(ship, rackId, nameof(moves));
            var index = IndexOnBoard(plan, crate, taken, "moves", nameof(moves));
            crates[index] = crate with { RackId = rackId, Position = position };
        }

        return crates;
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

    /// <summary>The first new problem in a rack a move leaves or enters, in rack order; null if there is none.</summary>
    private static string? Problem(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves, List<Crate> crates)
    {
        HashSet<string> touched = [.. moves.SelectMany(m => (string[])[m.Crate.RackId, m.RackId])];
        return ship.Racks.OrderBy(r => r.Index)
            .Where(r => touched.Contains(r.Id))
            .Select(r => CrateLayout.NewProblem(r, plan.CratesForRack(r.Id), [.. crates.Where(c => c.RackId == r.Id)]))
            .FirstOrDefault(problem => problem is not null);
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
        // The parts keep the crate's mission (D42).
        Crate[] pieces = [.. parts.Zip(positions)
            .OrderByDescending(p => p.First)
            .Select(p => new Crate(crate.Destination, crate.Commodity, p.First, crate.RackId, p.Second, Mission: crate.Mission))];
        List<Crate> crates = [.. plan.Crates];
        crates.RemoveAt(index);
        crates.InsertRange(index, pieces);

        // The planner made the crate from an item of its mission that holds it (SavedPlan checks that), so there is one.
        var mission = saved.MissionOf(crate.Mission);
        var item = ItemsHolding(mission, crate.Destination, crate.Commodity, [crate.CrateSize]).First();
        return new Edit(
            new SavedPlan(Rebooked(saved.Missions, mission, item, [crate.CrateSize], parts), WithHints(plan with { Crates = crates }, ship), saved.Watermark),
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

        // One item has to hold them all: its SCU is contract data and must not move to another item.
        var mission = saved.MissionOf(first.Mission);
        CrateSize[] sizes = [.. crates.Select(c => c.CrateSize)];
        (int Stop, int Item)[] holding = [.. ItemsHolding(mission, first.Destination, first.Commodity, sizes)];
        if (holding.Length == 0)
        {
            return Edit.Refused("The crates belong to different contract items.");
        }

        // The kiosk never delivers a crate above the max crate size (D23).
        (int Stop, int Item)[] allowed = [.. holding.Where(at => mission.Stops[at.Stop].Items[at.Item].MaxCrateSize >= merged)];
        if (allowed.Length == 0)
        {
            var max = (int)mission.Stops[holding[0].Stop].Items[holding[0].Item].MaxCrateSize;
            return Edit.Refused(Invariant($"{(int)merged} SCU crates are larger than the max crate size of {max} SCU for {first.Commodity}."));
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
        list[indices.Min()] = new Crate(first.Destination, first.Commodity, merged, first.RackId, corner, Mission: first.Mission);
        foreach (var index in indices.Order().Skip(1).Reverse())
        {
            list.RemoveAt(index);
        }

        return new Edit(
            new SavedPlan(Rebooked(saved.Missions, mission, allowed[0], sizes, [merged]), WithHints(plan with { Crates = list }, ship), saved.Watermark),
            null);
    }

    /// <summary>
    /// Stop and item index within <paramref name="mission"/> of every contract item for <paramref name="destination"/> and
    /// <paramref name="commodity"/> whose crates include all of <paramref name="crates"/>, in mission order.
    /// </summary>
    private static IEnumerable<(int Stop, int Item)> ItemsHolding(
        Mission mission, string destination, string commodity, IReadOnlyList<CrateSize> crates) =>
        from s in Enumerable.Range(0, mission.Stops.Count)
        where mission.Stops[s].Destination == destination
        from i in Enumerable.Range(0, mission.Stops[s].Items.Count)
        let item = mission.Stops[s].Items[i]
        where item.Commodity == commodity && Includes(item.CrateList, crates)
        select (s, i);

    /// <summary>True if <paramref name="list"/> holds every crate of <paramref name="wanted"/>, each as often as it is wanted.</summary>
    private static bool Includes(IReadOnlyList<CrateSize> list, IReadOnlyList<CrateSize> wanted)
    {
        List<CrateSize> rest = [.. list];
        return wanted.All(rest.Remove);
    }

    /// <summary>
    /// The missions with item <paramref name="at"/> of <paramref name="mission"/> holding <paramref name="added"/> instead of
    /// <paramref name="removed"/>.
    /// </summary>
    private static IReadOnlyList<Mission> Rebooked(
        IReadOnlyList<Mission> missions, Mission mission, (int Stop, int Item) at, IReadOnlyList<CrateSize> removed, IReadOnlyList<CrateSize> added)
    {
        var stop = mission.Stops[at.Stop];
        var item = stop.Items[at.Item];
        List<CrateSize> crates = [.. item.CrateList];
        foreach (var size in removed)
        {
            crates.Remove(size);
        }

        MissionItem[] items = [.. stop.Items];
        items[at.Item] = item.WithCrateList([.. crates, .. added]);
        MissionStop[] stops = [.. mission.Stops];
        stops[at.Stop] = stop with { Items = items };
        Mission rebooked = new(mission.Id, stops, mission.Contract, mission.Unplaced);
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
    private static LoadPlan WithHints(LoadPlan plan, Ship ship, IEnumerable<PlanWarning>? added = null)
    {
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        return plan with { Warnings = [.. Hints(racks, plan.Placements), .. Notes(plan, racks, added ?? [])] };
    }

    /// <summary>
    /// The planner's notes, in rack order, each once: those <paramref name="plan"/> carries and <paramref name="added"/>. A
    /// note says what the crates can't: that the plan came from the shape-aware fallback (<see cref="FallbackNote"/>), or
    /// that the search for the route order gave up in a rack (<see cref="CrateLayout.Lay"/>). So it is carried, not
    /// derived, until the next Plan starts without it (D54).
    /// </summary>
    private static IEnumerable<PlanWarning> Notes(LoadPlan plan, Rack[] racks, IEnumerable<PlanWarning> added)
    {
        HashSet<PlanWarning> present = [.. added, .. plan.Warnings];
        return racks.SelectMany(r => (PlanWarning[])[new(r.Id, FallbackNote), new(r.Id, OrderUncheckedNote(r))]).Where(present.Contains);
    }

    /// <summary>In every rack with cargo of a plan from the shape-aware fallback: the racks don't follow the route (D54).</summary>
    private const string FallbackNote = "Racks not in route order – crates placed where they fit";

    private static IEnumerable<PlanWarning> OrderUncheckedNotes(IEnumerable<Rack> racks) =>
        racks.Select(r => new PlanWarning(r.Id, OrderUncheckedNote(r)));

    private static string OrderUncheckedNote(Rack rack) => Invariant(
        $"Rack {rack.Number}: too many crates to rearrange – red crates lie on cargo that unloads earlier. Check the unload order by hand.");

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
    /// What the player has to watch at the cargo lift, derived from the placements alone: every rack holding more
    /// than one destination, and every rack holding part of a destination that spans racks. Delivered cargo is off
    /// the ship and doesn't count (D16). Destinations keep placement order, which is route order for a planned stop.
    /// <paramref name="racks"/> must be in rack order.
    /// </summary>
    private static List<PlanWarning> Hints(Rack[] racks, IReadOnlyList<Placement> allPlacements)
    {
        Placement[] placements = [.. allPlacements.Where(p => !p.Delivered)];
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
    /// Names the first crate in route order whose shape or size no rack takes (<see cref="Rack.CanHold"/>). Checked per
    /// crate, not per mix: for Hull A every mix up to the rack capacity fits (D9).
    /// </summary>
    private static void EnsureCratesFitShip(Ship ship, Stop[] route)
    {
        foreach (var stop in route)
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

    /// <summary>Names the first stop in route order that no longer fits into the ship.</summary>
    private static void EnsureFitsShip(Ship ship, Stop[] route)
    {
        var needed = route.Sum(s => s.TotalScu);
        var loaded = 0;
        foreach (var stop in route)
        {
            loaded += stop.TotalScu;
            if (loaded > ship.TotalScu)
            {
                throw new PlanException(Invariant(
                    $"Ship capacity exceeded: {needed - ship.TotalScu} SCU missing, stop '{stop.Destination}' doesn't fit (stops need {needed} SCU, ship '{ship.Id}' holds {ship.TotalScu} SCU)."));
            }
        }
    }

    private static Placement Place(Stop stop, int itemIndex, Rack rack, int scu)
    {
        var item = stop.Items[itemIndex];
        return new Placement(
            stop.Destination, rack.Id, scu == item.Scu ? item : new CargoItem(item.Commodity, scu, item.CrateSize));
    }

    /// <summary>
    /// Cuts the crate line into one contiguous segment per rack, in rack order. Best cut: fewest split stops,
    /// then fewest split items, then fewest stops in the fullest rack, then the smallest sum of squared stop
    /// counts (spreads sharing evenly), then fewest racks. Ties share in the lowest-numbered racks.
    /// </summary>
    private static Segment[] Partition(Rack[] racks, CrateLine line)
    {
        var (unbounded, _) = Solve(racks, line, line.Stops.Length);
        var rackCount = BestRackCount(unbounded, line.Length);
        if (rackCount < 0)
        {
            throw CratesDontFit(line, unbounded);
        }

        // "Fewest stops in the fullest rack" isn't additive, so it can't be part of the DP score. Instead:
        // the smallest cap on stops per rack that still reaches the fewest split stops and items.
        var target = unbounded[line.Length, rackCount].GetValueOrDefault();
        for (var cap = 1; ; cap++)
        {
            var (best, segmentStart) = Solve(racks, line, cap);
            rackCount = BestRackCount(best, line.Length);
            if (rackCount < 0 || best[line.Length, rackCount] is not { } score
                || (score.Splits, score.ItemCuts) != (target.Splits, target.ItemCuts))
            {
                continue;
            }

            var segments = new Segment[rackCount];
            for (int r = rackCount, end = line.Length; r > 0; r--)
            {
                var start = segmentStart[end, r];
                segments[r - 1] = new Segment(racks[r - 1], start, end);
                end = start;
            }

            return segments;
        }
    }

    /// <summary>
    /// Dynamic programming over (first `end` crates, first r racks), at most <paramref name="maxStopsPerRack"/>
    /// stops per rack. best[end, r]: score of crates [0, end) in racks [0, r), no rack empty; null = impossible.
    /// </summary>
    private static (Score?[,] Best, int[,] SegmentStart) Solve(Rack[] racks, CrateLine line, int maxStopsPerRack)
    {
        var best = new Score?[line.Length + 1, racks.Length + 1];
        var segmentStart = new int[line.Length + 1, racks.Length + 1];
        best[0, 0] = (0, 0, 0);

        for (var r = 1; r <= racks.Length; r++)
        {
            for (var end = r; end <= line.Length; end++)
            {
                var cutsStop = line.CutsStop(end);
                var cutsItem = line.CutsItem(end);

                // Smallest last segment first; ties keep it, so sharing ends up in the lowest-numbered racks.
                for (var start = end - 1; start >= r - 1; start--)
                {
                    var stopsInRack = line.StopsIn(start, end);
                    if (line.ScuBefore[end] - line.ScuBefore[start] > racks[r - 1].CapacityScu || stopsInRack > maxStopsPerRack)
                    {
                        break;
                    }

                    if (best[start, r - 1] is not { } previous)
                    {
                        continue;
                    }

                    // A stop counts as split once, at its first boundary: the one whose segment holds the stop's start.
                    var newSplit = cutsStop && start <= line.StopStart[line.Crates[end].Stop];
                    Score score = (
                        previous.Splits + (newSplit ? 1 : 0),
                        previous.ItemCuts + (cutsItem ? 1 : 0),
                        previous.Squares + (stopsInRack * stopsInRack));
                    if (best[end, r] is not { } current || score.CompareTo(current) < 0)
                    {
                        best[end, r] = score;
                        segmentStart[end, r] = start;
                    }
                }
            }
        }

        return (best, segmentStart);
    }

    /// <summary>Rack count with the best score for the whole line, fewest racks on a tie; -1 = impossible.</summary>
    private static int BestRackCount(Score?[,] best, int length) =>
        Enumerable.Range(0, best.GetLength(1))
            .Where(r => best[length, r] is not null)
            .OrderBy(r => best[length, r])
            .DefaultIfEmpty(-1)
            .First();

    /// <summary>Names the first stop whose crates can't be packed behind the stops before it.</summary>
    private static PlanException CratesDontFit(CrateLine line, Score?[,] best)
    {
        // Feasibility only gets lost while adding crates, so the first unreachable stop end pins the culprit.
        var stop = Enumerable.Range(0, line.Stops.Length)
            .First(s => Enumerable.Range(0, best.GetLength(1)).All(r => best[line.StopEnd(s), r] is null));

        return new PlanException(Invariant(
            $"Crates don't fit: stop '{line.Stops[stop].Destination}' can't be packed in whole crates into the racks behind the previous stops."));
    }

    /// <summary>One crate of item <see cref="Item"/> of stop <see cref="Stop"/> (indices into <see cref="CrateLine.Stops"/>).</summary>
    private readonly record struct LineCrate(int Stop, int Item, int Scu);

    /// <summary>Crates [Start, End) of the line go into <see cref="Rack"/>.</summary>
    private readonly record struct Segment(Rack Rack, int Start, int End);

    /// <summary>
    /// The route as one row of crates: stops in route order, items in input order. Rack boundaries fall
    /// between crates, so racks always hold whole crates and neighbouring parts of the route.
    /// </summary>
    private sealed record CrateLine(Stop[] Stops, LineCrate[] Crates, int[] ScuBefore, int[] StopStart)
    {
        public int Length => Crates.Length;

        public static CrateLine Of(IEnumerable<Stop> route)
        {
            // A stop without items has no crates and takes no rack.
            Stop[] stops = [.. route.Where(s => s.Items.Count > 0)];
            List<LineCrate> crates = [];
            var stopStart = new int[stops.Length];
            for (var s = 0; s < stops.Length; s++)
            {
                stopStart[s] = crates.Count;
                for (var i = 0; i < stops[s].Items.Count; i++)
                {
                    var crateScu = (int)stops[s].Items[i].CrateSize;
                    crates.AddRange(Enumerable.Repeat(new LineCrate(s, i, crateScu), stops[s].Items[i].Scu / crateScu));
                }
            }

            var scuBefore = new int[crates.Count + 1];
            for (var p = 0; p < crates.Count; p++)
            {
                scuBefore[p + 1] = scuBefore[p] + crates[p].Scu;
            }

            return new CrateLine(stops, [.. crates], scuBefore, stopStart);
        }

        public int StopEnd(int stop) => stop + 1 < Stops.Length ? StopStart[stop + 1] : Length;

        /// <summary>Number of stops with crates in [start, end).</summary>
        public int StopsIn(int start, int end) => Crates[end - 1].Stop - Crates[start].Stop + 1;

        /// <summary>A rack boundary before crate p splits a stop.</summary>
        public bool CutsStop(int p) => p > 0 && p < Length && Crates[p - 1].Stop == Crates[p].Stop;

        /// <summary>A rack boundary before crate p splits an item.</summary>
        public bool CutsItem(int p) => CutsStop(p) && Crates[p - 1].Item == Crates[p].Item;
    }
}
