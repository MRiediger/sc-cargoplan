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
                racks[r], [.. inRack.Zip(laying.Poses).Select(p => (new RackAssignment.Piece(all[p.First].Stop, all[p.First].Item, all[p.First].Size), p.Second))], laying.GaveUp));
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
            .Select(c => new Crate(stop.Destination, item.Commodity, c.Piece.Size, load.Rack.Id, c.Pose.Position, Rotated: c.Pose.Rotated))))),
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
            [.. moves.Select(m => new MoveResolution.Intent(IndexOnBoard(plan, m.Crate, taken, "moves", nameof(moves)), RackOf(ship, m.RackId, nameof(moves)), m.Position, m.Rotated))],
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
    /// Where crates go when the player drags a legend line or a mission's header onto a rack (B6, D69): the crates
    /// outside the rack into it without a cell, as a selection without one goes (D65): laid around the cargo there, else
    /// the rack laid out anew, else other destinations' cargo displaced, else crate by crate, other racks last. Atomic:
    /// if one crate finds no room, nothing moves. Crates already in the rack stay where they lie, so a drop onto the rack
    /// holding them all changes nothing. Preview and drop as with
    /// <see cref="ResolveMove(LoadPlan, Ship, IReadOnlyList{CrateMove}, MoveStage)"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, the ship has no such rack, or a crate isn't part of the plan, is delivered or
    /// is named twice.
    /// </exception>
    public static MoveOutcome ResolveMoveToRack(LoadPlan plan, Ship ship, IReadOnlyList<Crate> crates, string rackId, MoveStage upTo = MoveStage.Refused)
    {
        plan.EnsureBelongsTo(ship);
        var target = RackOf(ship, rackId, nameof(rackId));
        HashSet<int> taken = [];
        MoveResolution.Intent[] intents = [.. crates.Select(c => new MoveResolution.Intent(IndexOnBoard(plan, c, taken, "moves", nameof(crates)), target, null))];
        return MoveResolution.Crates(plan, ship, [.. intents.Where(i => plan.Crates[i.Index].RackId != target.Id)], upTo);
    }

    private static LoadPlan Applied(MoveOutcome outcome) =>
        outcome.Plan ?? throw new PlanException(outcome.Refusal ?? "");

    /// <summary>
    /// Marks every crate of a stop as delivered, or back. A stop is unloaded in one visit, split parts included,
    /// so the unit is the stop. The hints are derived anew without delivered cargo. Delivered crates keep their position
    /// but free their cells (D24 addendum), and loaded ones stop being loaded; every crate that rested on them settles
    /// straight down its column (K6.4, <see cref="Settled"/>). Ticked back, the crates are planned again: in their cells
    /// while those are free, else laid into free cells by the rule of <see cref="PlaceInto"/> (K2.2, D41). The original
    /// plan stays unchanged, crate order too.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no stop <paramref name="destination"/>.</exception>
    /// <exception cref="PlanException">Ticking back: no room for a crate of the stop.</exception>
    public static LoadPlan SetDelivered(LoadPlan plan, Ship ship, string destination, bool delivered)
    {
        plan.EnsureBelongsTo(ship);
        EnsureStop(plan, destination);
        return SetDelivered(plan, ship, destination, c => c.Destination == destination, delivered);
    }

    /// <summary>
    /// Marks the crates of one mission for a stop as delivered, or back: the legend's line (B6, D69), for the player who
    /// unloads one contract's crates at a time. Otherwise as the stop's overload.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship or has no crate of <paramref name="mission"/> for <paramref name="destination"/>.
    /// </exception>
    /// <exception cref="PlanException">Ticking back: no room for a crate.</exception>
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
        if (delivered)
        {
            var ticking = plan with { Crates = [.. plan.Crates.Select(c => ticked(c) && !c.OffShip ? c.AsDelivered() : c)] };
            return WithHints(Settled(ticking, ship), ship);
        }

        // Off-ship crates (K2.2) have no cells to go back to: ticked back, they are laid in (BackOnBoard).
        var changed = plan with { Crates = [.. plan.Crates.Select(c => ticked(c) && c.Delivered && !c.OffShip ? c.AsPlanned() : c)] };
        HashSet<string> racks = [.. plan.Crates.Where(c => ticked(c) && !c.OffShip).Select(c => c.RackId)];
        var inPlace = ship.Racks
            .Where(r => racks.Contains(r.Id))
            .All(r => CrateLayout.NewProblem(r, plan.CratesForRack(r.Id), changed.CratesForRack(r.Id)) is null);
        if (!inPlace)
        {
            // Their cells were taken since (settling, a move): back to planned all the same, laid in where there is room (K6.4).
            return WithHints(BackOnBoard(plan, ship, destination, [.. plan.Crates.Where(c => ticked(c) && c.Delivered)]), ship);
        }

        if (plan.Crates.Where(c => ticked(c) && c.OffShip).ToArray() is { Length: > 0 } offShip)
        {
            changed = BackOnBoard(changed, ship, destination, offShip);
        }

        return WithHints(changed, ship);
    }

    /// <summary>
    /// Marks one mission's crates for a stop as loaded, or back to planned (K6.4, D79): the legend's line, once its crates
    /// are on board in the game. Nothing moves. Loaded crates stay where they lie until the player moves them; the
    /// planner lays everything else around them.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    /// <exception cref="PlanException">The delivery has no crates on board: waiting (not placed yet) or delivered.</exception>
    public static LoadPlan SetLoaded(LoadPlan plan, Ship ship, string destination, int mission, bool loaded)
    {
        plan.EnsureBelongsTo(ship);
        bool Of(Crate c) => c.Destination == destination && c.Mission == mission;
        if (!plan.Crates.Any(Of))
        {
            throw new PlanException($"{destination} is not placed yet.");
        }

        if (plan.Crates.Any(c => Of(c) && c.Delivered))
        {
            throw new PlanException($"{destination} is not on board: it is delivered.");
        }

        return plan with { Crates = [.. plan.Crates.Select(c => Of(c) ? (loaded ? c.AsLoaded() : c.AsPlanned()) : c)] };
    }

    /// <summary>
    /// Every crate on board that rests on nothing, dropped straight down its column along the stack axis until it touches
    /// cargo or the plate (K6.4, D79): same rack, same cell across, the lowest first, so a crate that falls makes room for
    /// the one above it. Loaded and planned alike. A crate that touches cargo with part of its bottom stays there, marked
    /// by <see cref="Validate"/>; nothing moves sideways. The plan itself if nothing falls.
    /// </summary>
    internal static LoadPlan Settled(LoadPlan plan, Ship ship)
    {
        Crate[] crates = [.. plan.Crates];
        var fell = false;
        foreach (var rack in ship.Racks)
        {
            int[] onBoard = [.. Enumerable.Range(0, crates.Length).Where(i => !crates[i].Delivered && crates[i].RackId == rack.Id).OrderBy(i => crates[i].Position.Stack)];
            HashSet<GridPosition> taken = [.. onBoard.SelectMany(i => crates[i].Cells)];
            foreach (var i in onBoard)
            {
                var crate = crates[i];
                taken.ExceptWith(crate.Cells);
                var fall = 0;
                while (crate.Position.Stack - fall > 0 && crate.Bottom.All(c => !taken.Contains(new GridPosition(c.Length, c.Width, c.Stack - fall - 1))))
                {
                    fall++;
                }

                if (fall > 0)
                {
                    crate = crate with { Position = new GridPosition(crate.Position.Length, crate.Position.Width, crate.Position.Stack - fall) };
                    crates[i] = crate;
                    fell = true;
                }

                taken.UnionWith(crate.Cells);
            }
        }

        return fell ? plan with { Crates = crates } : plan;
    }

    /// <summary>
    /// How many crates on board lie lower in <paramref name="after"/> than in <paramref name="before"/>, in the same
    /// column of the same rack (<see cref="Settled"/>), loaded and planned; for the notice "2 loaded crates settled".
    /// Crates have no identity, so equal crates in one column pair up by height.
    /// </summary>
    public static (int Loaded, int Planned) SettledCrates(LoadPlan before, LoadPlan after)
    {
        var was = before.Crates.Where(c => !c.Delivered).GroupBy(Column).ToDictionary(g => g.Key, g => g.Select(c => c.Position.Stack).ToList());
        var (loaded, planned) = (0, 0);
        foreach (var group in after.Crates.Where(c => !c.Delivered).GroupBy(Column))
        {
            if (!was.TryGetValue(group.Key, out var stacks))
            {
                continue;
            }

            List<int> lower = [];
            foreach (var stack in group.Select(c => c.Position.Stack))
            {
                if (!stacks.Remove(stack))
                {
                    lower.Add(stack);
                }
            }

            var settled = Math.Min(lower.Count, stacks.Count);
            (loaded, planned) = group.Key.Loaded ? (loaded + settled, planned) : (loaded, planned + settled);
        }

        return (loaded, planned);

        static (string, string, CrateSize, string, int, int, bool, int, bool Loaded) Column(Crate c) =>
            (c.Destination, c.Commodity, c.CrateSize, c.RackId, c.Position.Length, c.Position.Width, c.Rotated, c.Mission, c.Loaded);
    }

    /// <summary>
    /// Off-ship crates ticked back (K2.2): delivered on another ship, but still on board after all. They are laid into free
    /// cells by the rule of <see cref="PlaceInto"/> (D41), nothing else moves, and each keeps its place in the list, so
    /// legend and colours keep their order.
    /// </summary>
    /// <exception cref="PlanException">No room for one of them.</exception>
    private static LoadPlan BackOnBoard(LoadPlan plan, Ship ship, string destination, Crate[] offShip)
    {
        Crate[] templates = [.. offShip.OrderByDescending(c => c.CrateSize).ThenBy(c => c.Commodity, StringComparer.Ordinal)];
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        var (fresh, failed) = LayStop(
            plan.Crates, racks, destination, [.. templates.Select(c => (c.CrateSize, c.Commodity))], 0, plan.FillOrder, new CrateLayout.StackingBudget(), []);
        if (fresh is null)
        {
            throw new PlanException(Invariant($"{destination} can't go back on board: no room for {(int)failed!.Value} SCU crate"));
        }

        // LayStop keeps the order of the kinds, so each laid crate is its template's.
        Dictionary<Crate, Crate> laid = new(ReferenceEqualityComparer.Instance);
        foreach (var (template, crate) in templates.Zip(fresh))
        {
            laid[template] = template.AsPlanned() with { RackId = crate.RackId, Position = crate.Position, Rotated = crate.Rotated };
        }

        return plan with { Crates = [.. plan.Crates.Select(c => laid.GetValueOrDefault(c, c))] };
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

        var cap = CrateCap.Of(ship);
        var plan = Plan(ship, [.. stopOrder.Select((destination, index) => new MissionStop(destination, index + 1, items[destination]).ToStop(cap))], fillOrder);
        return new SavedPlan(placed, plan with { Crates = Assigned(plan.Crates, placed, cap) }, cap, watermark);
    }

    /// <summary>
    /// The editor's Plan with what is on board and what is delivered kept (K6.4, D19 addendum): a delivery (mission ×
    /// destination) of <paramref name="current"/> that is loaded or delivered keeps its crates as they are, where the
    /// edited <paramref name="missions"/> still hold it with the same crates (commodity and size; a split or merge is in
    /// the mission, D27). It is known by its contract's first id and destination, a typed one by its destination alone
    /// (typed rows of one destination are one mission, D42, and the editor numbers them anew). Changed, it is planned
    /// again, and <c>Cleared</c> says so ("Riker: changed in the editor – no longer loaded"). Without anything kept, the
    /// plan from scratch (<see cref="Plan(Ship, IReadOnlyList{Mission}, IReadOnlyList{string}, FillOrder, long)"/>);
    /// with only delivered cargo kept, the rest planned like that and joined (as Re-plan does, D66); with loaded cargo,
    /// the rest laid in around it by the rule of <see cref="PlaceInto"/> (D41): first the rest of missions loaded in part,
    /// then missions by SCU (largest first), first destination and id, so the order of entry doesn't matter. A mission
    /// without room waits (and a delivered delivery of it loses its tick, as on another ship, K2.2).
    /// </summary>
    /// <exception cref="ArgumentException">The order doesn't name every destination once.</exception>
    /// <exception cref="PlanException">
    /// The rest of a mission loaded in part finds no room: the elevator hands out whole missions, so it can't wait while
    /// its loaded crates are on board (K6.4, E1). Or the plan from scratch has none (see the other overload).
    /// </exception>
    public static (SavedPlan Plan, IReadOnlyList<string> Cleared) PlanKeeping(
        SavedPlan current, Ship ship, IReadOnlyList<Mission> missions, IReadOnlyList<string> stopOrder, FillOrder fillOrder = FillOrder.BowFirst, long watermark = 0)
    {
        var cap = CrateCap.Of(ship);
        var states = current.Plan.ShipId == ship.Id ? KeptDeliveries(current) : [];
        List<string> cleared = [];
        List<Crate> kept = [];
        HashSet<(int Mission, string Destination)> keptDeliveries = [];
        foreach (var mission in missions)
        {
            foreach (var stop in mission.Stops)
            {
                var key = DeliveryKey(mission.Contract, stop.Destination);
                if (!states.Remove(key, out var crates))
                {
                    continue;
                }

                if (Kinds(crates.Select(c => (c.Commodity, c.CrateSize))).SequenceEqual(Kinds(stop.Items.SelectMany(i => i.CrateListUnder(cap).Select(size => (i.Commodity, size))))))
                {
                    kept.AddRange(crates.Select(c => c with { Mission = mission.Id }));
                    keptDeliveries.Add((mission.Id, stop.Destination));
                }
                else
                {
                    cleared.Add($"{stop.Destination}: changed in the editor – {(crates[0].Loaded ? "no longer loaded" : "tick cleared")}");
                }
            }
        }

        if (kept.Count == 0)
        {
            return (Plan(ship, missions, stopOrder, fillOrder, watermark), cleared);
        }

        Mission[] placed = [.. missions.Select(m => m with { Unplaced = false })];
        Mission[] rest = [.. placed.Select(m => Without(m, keptDeliveries)).OfType<Mission>()];
        if (!kept.Any(c => c.Loaded))
        {
            // Only delivered cargo kept: it takes no room, the rest is planned from scratch around nothing (D66).
            var planned = Plan(ship, rest, [.. stopOrder.Where(d => rest.Any(m => m.Stops.Any(s => s.Destination == d)))], fillOrder, watermark).Plan;
            return (new SavedPlan(placed, ItemsOf(placed, Joined(planned, [.. kept], [.. stopOrder], ship), cap), cap, watermark), cleared);
        }

        HashSet<int> partly = [.. kept.Where(c => c.Loaded).Select(c => c.Mission)];
        var plan = new LoadPlan(ship.Id, [.. kept], [], fillOrder);
        HashSet<int> waiting = [];
        List<Rack> stackingUnchecked = [];
        foreach (var mission in rest
            .OrderByDescending(m => partly.Contains(m.Id))
            .ThenByDescending(m => m.TotalScu)
            .ThenBy(m => m.Stops.Min(s => s.Destination), StringComparer.Ordinal)
            .ThenBy(m => m.Id))
        {
            if (TryPlace(plan, ship, mission, cap) is ({ } crates, _, var gaveUp))
            {
                plan = plan with { Crates = crates };
                stackingUnchecked.AddRange(gaveUp.Where(r => !stackingUnchecked.Contains(r)));
            }
            else if (partly.Contains(mission.Id))
            {
                throw new PlanException(Invariant(
                    $"{string.Join(", ", mission.Stops.Select(s => s.Destination))}: {mission.TotalScu} SCU not loaded yet find no room around the loaded cargo"));
            }
            else
            {
                waiting.Add(mission.Id);
            }
        }

        // A waiting mission has no crates: what of it was delivered loses its tick (K2.2, D41).
        Crate[] all = [.. plan.Crates.Where(c => !waiting.Contains(c.Mission))];
        cleared.AddRange(placed.Where(m => waiting.Contains(m.Id))
            .SelectMany(m => m.Stops.Where(s => keptDeliveries.Contains((m.Id, s.Destination))))
            .Select(s => $"{s.Destination}: waits – tick cleared"));
        Mission[] result = [.. placed.Select(m => waiting.Contains(m.Id) ? m with { Unplaced = true } : m)];
        Crate[] ordered = [.. stopOrder.SelectMany(d => all.Where(c => c.Destination == d)), .. all.Where(c => !stopOrder.Contains(c.Destination))];
        return (new SavedPlan(result, WithHints(new LoadPlan(ship.Id, SavedPlan.WithItems(result, ordered, _ => false, cap), [], fillOrder), ship, StackingUncheckedNotes(stackingUnchecked)), cap, watermark), cleared);

        static IEnumerable<(string, CrateSize)> Kinds(IEnumerable<(string Commodity, CrateSize Size)> crates) =>
            crates.OrderBy(k => k.Commodity, StringComparer.Ordinal).ThenBy(k => k.Size);
    }

    /// <summary>
    /// The deliveries of <paramref name="saved"/> that are loaded or delivered as a whole, by <see cref="DeliveryKey"/>,
    /// with their crates; of two with one key the first.
    /// </summary>
    private static Dictionary<(long Contract, string Destination), Crate[]> KeptDeliveries(SavedPlan saved)
    {
        var contracts = saved.Missions.ToDictionary(m => m.Id, m => m.Contract);
        Dictionary<(long, string), Crate[]> kept = [];
        foreach (var delivery in saved.Plan.Crates.GroupBy(c => (c.Mission, c.Destination)))
        {
            if (delivery.All(c => c.Loaded) || delivery.All(c => c.Delivered))
            {
                kept.TryAdd(DeliveryKey(contracts.GetValueOrDefault(delivery.Key.Mission), delivery.Key.Destination), [.. delivery]);
            }
        }

        return kept;
    }

    /// <summary>
    /// A delivery across plans: its capture's first ContractTracker id, or 0 for a typed mission, and the destination. The
    /// id of a detached capture is negated, so a new database's contract with that id is another delivery (D81).
    /// </summary>
    private static (long Contract, string Destination) DeliveryKey(ContractSource? contract, string destination) =>
        (contract is null ? 0 : contract.Detached ? -contract.ContractIds.Min() : contract.ContractIds.Min(), destination);

    /// <summary><paramref name="mission"/> without the stops in <paramref name="kept"/>; null if none is left.</summary>
    private static Mission? Without(Mission mission, IReadOnlySet<(int Mission, string Destination)> kept)
    {
        MissionStop[] left = [.. mission.Stops.Where(s => !kept.Contains((mission.Id, s.Destination)))];
        return left.Length == 0 ? null
            : left.Length == mission.Stops.Count ? mission
            : new Mission(mission.Id, [.. left.Select((s, index) => s with { Order = index + 1 })], mission.Contract);
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

        return TryPlace(saved.Plan, ship, mission, saved.CrateCap) is ({ } crates, _, var stackingUnchecked)
            ? new SavedPlan([.. saved.Missions, mission], WithHints(saved.Plan with { Crates = crates }, ship, StackingUncheckedNotes(stackingUnchecked)), saved.CrateCap, saved.Watermark)
            : new SavedPlan([.. saved.Missions, mission with { Unplaced = true }], saved.Plan, saved.CrateCap, saved.Watermark);
    }

    /// <summary>
    /// Why the unplaced mission <paramref name="missionId"/> doesn't fit, for the legend (D41): "doesn't fit (no rack
    /// accepts 4 SCU crates, door limit 2 SCU)" if no rack takes a crate the player set (D70), "doesn't fit (16 SCU, 8
    /// free)" if the ship lacks the room, "doesn't fit (no room for 8 SCU crates)" if the room is there in pieces. Null for a
    /// placed mission, and for one that would fit now (placed at the next <see cref="RetryUnplaced"/>). The same check as
    /// <see cref="PlaceInto"/>, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no such mission.</exception>
    public static string? PlaceProblem(SavedPlan saved, Ship ship, int missionId)
    {
        saved.Plan.EnsureBelongsTo(ship);
        var mission = saved.MissionOf(missionId);
        if (!mission.Unplaced || TryPlace(saved.Plan, ship, mission, saved.CrateCap) is not (null, { } failed, _))
        {
            return null;
        }

        if (!ship.Racks.Any(r => r.CanHold(failed)))
        {
            return Invariant($"doesn't fit ({CrateCap.Of(ship).Refusal(failed)})");
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
        // A contract gone from ContractTracker gets no room of its own (D81): it waits until it is back or deleted.
        foreach (var waiting in saved.Missions.Where(m => m.Unplaced && m.Contract is not { Gone: true }))
        {
            if (TryPlace(current.Plan, ship, waiting, current.CrateCap) is ({ } crates, _, var stackingUnchecked))
            {
                current = new SavedPlan(
                    [.. current.Missions.Select(m => m.Id == waiting.Id ? m with { Unplaced = false } : m)],
                    WithHints(current.Plan with { Crates = crates }, ship, StackingUncheckedNotes(stackingUnchecked)),
                    current.CrateCap,
                    current.Watermark);
            }
        }

        return current;
    }

    /// <summary>
    /// The plan laid out anew: on another ship when the player switches (D59), on its own ship when the player asks for
    /// it (Re-plan, D66). Missions, sources, watermark and fill order stay; positions and moves go, split crates keep their
    /// sizes (they are in the missions, D27). The only method that takes a plan of another ship. A plan whose mixes were
    /// derived under another cap (a file from before D70) is planned anew like one of another ship. Loaded is this ship's
    /// state (K6.4): planned anew for other racks, loaded cargo is planned cargo again; delivered stays delivered (K2.2).
    /// </summary>
    public static SavedPlan Replan(SavedPlan saved, Ship ship) =>
        saved.Plan.ShipId == ship.Id && saved.CrateCap == CrateCap.Of(ship)
            ? ReplanInPlace(saved, ship)
            : ReplanForShip(new SavedPlan(saved.Missions, saved.Plan with { Crates = [.. saved.Plan.Crates.Select(c => c.Loaded ? c.AsPlanned() : c)] }, saved.CrateCap, saved.Watermark), ship);

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

    // Turned in place is moved too (D71).
    private static (string, string, CrateSize, string, GridPosition, bool) Lying(Crate crate) =>
        (crate.Destination, crate.Commodity, crate.CrateSize, crate.RackId, crate.Position, crate.Rotated);

    /// <summary>
    /// The player's Re-plan (D66): the cargo on board laid out as a fresh <see cref="Plan(Ship, IReadOnlyList{Mission}, IReadOnlyList{string}, FillOrder, long)"/>
    /// of the same missions would lay it, waiting missions included, so the result doesn't depend on the order things came
    /// in (D64). Ticked crates are off the ship: they stay where they lay, ticked, and take no room (D24), also when only
    /// part of a stop is ticked (D41). Never loses cargo: if the waiting missions don't fit, the cargo on board is laid
    /// out without them and they wait (<see cref="RetryUnplaced"/>); if even that finds no layout, the plan stays as it is.
    /// The missions stay the same list unless one of them got placed. With loaded cargo (K6.4) it stays where it lies and
    /// the rest is laid in around it (<see cref="PlanKeeping"/>); laid in that way, more can wait than in the layout the
    /// player let grow, so a result with more SCU waiting is not taken, the plan stays as it is (E2).
    /// </summary>
    private static SavedPlan ReplanInPlace(SavedPlan saved, Ship ship)
    {
        if (saved.Plan.Crates.Any(c => c.Loaded))
        {
            try
            {
                var (around, _) = PlanKeeping(saved, ship, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], saved.Plan.FillOrder, saved.Watermark);
                return Waiting(around) > Waiting(saved) ? saved : around;
            }
            catch (PlanException)
            {
                return saved;
            }

            static int Waiting(SavedPlan plan) => plan.Missions.Where(m => m.Unplaced).Sum(m => m.TotalScu);
        }

        var cap = saved.CrateCap;
        Crate[] delivered = [.. saved.Plan.Crates.Where(c => c.Delivered)];
        Mission[] onBoard = [.. saved.Missions.Select(m => m.Unplaced ? m : WithoutDelivered(m, delivered, cap)).OfType<Mission>()];
        if (onBoard.Length == 0)
        {
            return saved;
        }

        string[] route = [.. saved.Stops.Select(s => s.Destination)];
        if (PlannedOrNull(ship, onBoard, route, saved.Plan.FillOrder) is { } all)
        {
            IReadOnlyList<Mission> missions = saved.Missions.Any(m => m.Unplaced) ? [.. saved.Missions.Select(m => m with { Unplaced = false })] : saved.Missions;
            return new SavedPlan(missions, ItemsOf(saved.Missions, Joined(all, delivered, route, ship), cap), cap, saved.Watermark);
        }

        if (PlannedOrNull(ship, [.. onBoard.Where(m => !m.Unplaced)], route, saved.Plan.FillOrder) is { } placed)
        {
            return RetryUnplaced(new SavedPlan(saved.Missions, ItemsOf(saved.Missions, Joined(placed, delivered, route, ship), cap), cap, saved.Watermark), ship);
        }

        // No layout of the cargo on board (a heuristic ship, D54): the player's one stays, the waiting missions get a try.
        return RetryUnplaced(saved, ship);
    }

    /// <summary>
    /// <paramref name="mission"/> with only the crates still on board: its ticked crates taken out of its items' crate
    /// lists (D41: a stop can be ticked in part). Null if everything is ticked.
    /// </summary>
    private static Mission? WithoutDelivered(Mission mission, IReadOnlyList<Crate> delivered, CrateCap cap)
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
                .Select(s => s with { Items = [.. s.Items.Select(i => Remaining(i, gone.GetValueOrDefault((s.Destination, i.Commodity)), cap)).OfType<MissionItem>()] })
                .Where(s => s.Items.Count > 0),
        ];
        return stops.Length == 0 ? null : new Mission(mission.Id, stops, mission.Contract);
    }

    /// <summary>The item without the crates in <paramref name="gone"/>, which it takes out of that list; null if none is left.</summary>
    private static MissionItem? Remaining(MissionItem item, List<CrateSize>? gone, CrateCap cap)
    {
        if (gone is null)
        {
            return item;
        }

        List<CrateSize> left = [];
        foreach (var crate in item.CrateListUnder(cap))
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
    private static LoadPlan ItemsOf(IReadOnlyList<Mission> missions, LoadPlan plan, CrateCap cap) =>
        plan with { Crates = SavedPlan.WithItems(missions, plan.Crates, c => c.Delivered, cap) };

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
    /// The plan on another ship (D59), or on racks that changed (D71): the cargo on board planned anew in the order the
    /// editor shows it (<see cref="SavedPlan.Stops"/>), as the editor's Plan would; if that fails, mission by mission into
    /// the empty ship in mission order (<see cref="RetryUnplaced"/>), the rest waits unplaced (D41). What is delivered stays
    /// delivered (K2.2): a delivery, a mission's crates for one destination (the legend's line, D69), whose crates are all
    /// ticked leaves its mission before planning, so it takes no room, and comes back off the ship
    /// (<see cref="Crate.OffShip"/>) with the crates its items take under the new ship's cap. A mission that waits has no
    /// crates, so a delivery of it loses its tick: the elevator hands out whole missions. Without ticks the editor's Plan.
    /// </summary>
    private static SavedPlan ReplanForShip(SavedPlan saved, Ship ship)
    {
        var fillOrder = saved.Plan.FillOrder;
        var cap = CrateCap.Of(ship);
        if (saved.Missions.Count == 0)
        {
            return new SavedPlan([], new LoadPlan(ship.Id, [], [], fillOrder), cap, saved.Watermark);
        }

        HashSet<(int Mission, string Destination)> delivered =
            [.. saved.Plan.Crates.GroupBy(c => (c.Mission, c.Destination)).Where(g => g.All(c => c.Delivered)).Select(g => g.Key)];
        Mission[] onBoard = [.. saved.Missions.Select(m => Undelivered(m, delivered)).OfType<Mission>()];
        string[] route = [.. saved.Stops.Select(s => s.Destination)];
        var replanned = PlanOrPlace(ship, onBoard, route, fillOrder, saved.Watermark);
        if (delivered.Count == 0)
        {
            return replanned;
        }

        // A mission wholly delivered isn't planned and isn't waiting; the others wait as their cargo on board does.
        var waiting = replanned.Missions.ToDictionary(m => m.Id, m => m.Unplaced);
        Mission[] missions = [.. saved.Missions.Select(m => m with { Unplaced = waiting.GetValueOrDefault(m.Id) })];
        Crate[] offShip =
        [
            .. missions.Where(m => !m.Unplaced).SelectMany(m => m.Items
                .Select((at, index) => (at.Stop, at.Item, Index: index))
                .Where(at => delivered.Contains((m.Id, at.Stop.Destination)))
                .SelectMany(at => at.Item.CrateListUnder(cap).Select(size => Crate.OffShipOf(at.Stop.Destination, at.Item.Commodity, size, m.Id, at.Index)))),
        ];
        return new SavedPlan(missions, ItemsOf(missions, Joined(replanned.Plan, offShip, route, ship), cap), cap, replanned.Watermark);
    }

    /// <summary>The editor's Plan of <paramref name="missions"/> on <paramref name="ship"/>, else mission by mission (D41).</summary>
    private static SavedPlan PlanOrPlace(Ship ship, IReadOnlyList<Mission> missions, string[] route, FillOrder fillOrder, long watermark)
    {
        var cap = CrateCap.Of(ship);
        if (missions.Count == 0)
        {
            return new SavedPlan([], new LoadPlan(ship.Id, [], [], fillOrder), cap, watermark);
        }

        HashSet<string> destinations = [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination)];
        try
        {
            return Plan(ship, missions, [.. route.Where(destinations.Contains)], fillOrder, watermark);
        }
        catch (PlanException)
        {
            // Too much for the ship, or no layout: what fits comes in mission by mission, as captures do (D41).
            SavedPlan empty = new([.. missions.Select(m => m with { Unplaced = true })], new LoadPlan(ship.Id, [], [], fillOrder), cap, watermark);
            return RetryUnplaced(empty, ship);
        }
    }

    /// <summary><paramref name="mission"/> without its delivered stops (K2.2); null if every stop is delivered.</summary>
    private static Mission? Undelivered(Mission mission, IReadOnlySet<(int Mission, string Destination)> delivered)
    {
        if (!mission.Stops.Any(s => delivered.Contains((mission.Id, s.Destination))))
        {
            return mission;
        }

        MissionStop[] left = [.. mission.Stops.Where(s => !delivered.Contains((mission.Id, s.Destination)))];
        return left.Length == 0 ? null : new Mission(mission.Id, [.. left.Select((s, index) => s with { Order = index + 1 })], mission.Contract, mission.Unplaced);
    }

    /// <summary>
    /// The player's Discard plan (K2.3): no missions, no crates, no ticks, and the ContractTracker watermark back at 0, so
    /// the next sync takes every contract in again (Martin's variant a: nothing remembers what was discarded). The fill
    /// order stays, it is the player's way of loading (T4.5); the plan is for <paramref name="ship"/>.
    /// </summary>
    public static SavedPlan Discard(SavedPlan saved, Ship ship) =>
        new([], new LoadPlan(ship.Id, [], [], saved.Plan.FillOrder), CrateCap.Of(ship));

    /// <summary>
    /// Takes a mission out of the plan with its crates (D42): deleted in the editor, dropped in the legend, or gone from
    /// ContractTracker. Ticks stay; a crate that rested on one of its crates settles straight down (K6.4,
    /// <see cref="Settled"/>), nothing else moves. The original stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no such mission.</exception>
    public static SavedPlan RemoveMission(SavedPlan saved, Ship ship, int missionId)
    {
        saved.Plan.EnsureBelongsTo(ship);
        var mission = saved.MissionOf(missionId);
        return new SavedPlan(
            [.. saved.Missions.Where(m => m != mission)],
            WithHints(Settled(saved.Plan with { Crates = [.. saved.Plan.Crates.Where(c => c.Mission != missionId)] }, ship), ship),
            saved.CrateCap,
            saved.Watermark);
    }

    /// <summary>
    /// Takes a mission's deliveries to <paramref name="destinations"/> out of the plan with their items and crates, the rest
    /// of it stays (D81: a contract gone from ContractTracker keeps its loaded deliveries). What rested on the crates settles
    /// as in <see cref="RemoveMission"/>; the crates left name their items anew, counted over the stops left (D68). At least
    /// one delivery stays; the original stays unchanged.
    /// </summary>
    internal static SavedPlan RemoveDeliveries(SavedPlan saved, Ship ship, int missionId, IReadOnlySet<string> destinations)
    {
        var mission = saved.MissionOf(missionId);
        var rest = Without(mission, destinations.Select(d => (missionId, d)).ToHashSet())
            ?? throw new ArgumentException($"Mission {missionId} would keep no delivery.", nameof(destinations));

        // Old item index → new one: Without keeps the stops left in their order (Mission.Items counts in it).
        var items = mission.Items.Select((at, index) => (at.Stop.Destination, index))
            .Where(at => !destinations.Contains(at.Destination))
            .Select((at, index) => (Old: at.index, New: index))
            .ToDictionary(at => at.Old, at => at.New);
        var plan = saved.Plan with
        {
            Crates =
            [
                .. saved.Plan.Crates
                    .Where(c => c.Mission != missionId || !destinations.Contains(c.Destination))
                    .Select(c => c.Mission == missionId ? c with { Item = items[c.Item] } : c),
            ],
        };
        return new SavedPlan(
            [.. saved.Missions.Select(m => m == mission ? rest : m)],
            WithHints(Settled(plan, ship), ship),
            saved.CrateCap,
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
        return new SavedPlan(missions, WithHints(saved.Plan with { Crates = crates }, ship), saved.CrateCap, saved.Watermark);
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
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> StackingUnchecked) TryPlace(LoadPlan plan, Ship ship, Mission mission, CrateCap cap)
    {
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        MissionStop[] stops = [.. mission.Stops.OrderByDescending(s => s.TotalScu).ThenBy(s => s.Destination, StringComparer.Ordinal)];
        var budget = new CrateLayout.StackingBudget();

        var placed = StopByStop(plan, mission, racks, stops, budget, cap) is { Crates: not null } byStop
            ? byStop
            : LargestFirst(plan, mission, racks, stops, budget, cap);

        // The crates of the mission take its items (D68); the crates already on board keep theirs.
        return placed.Crates is { } crates ? placed with { Crates = [.. SavedPlan.WithItems([mission], crates, c => c.Mission != mission.Id, cap)] } : placed;
    }

    /// <summary>
    /// The rule of <see cref="PlaceInto"/> (D41, D64): each stop whole into a rack, tried as the D41 order has them (racks
    /// with the destination on board, the first rack with nothing on board, then rack order): the first where no new crate
    /// sits on other cargo, else the first that takes it. If none takes it, crate by crate, largest first, by the same
    /// choice over all racks.
    /// </summary>
    private static (List<Crate>? Crates, CrateSize? Failed, IReadOnlyList<Rack> StackingUnchecked) StopByStop(
        LoadPlan plan, Mission mission, Rack[] racks, MissionStop[] stops, CrateLayout.StackingBudget budget, CrateCap cap)
    {
        List<Crate> crates = [.. plan.Crates];
        List<Rack> stackingUnchecked = [];
        foreach (var stop in stops)
        {
            (CrateSize Size, string Commodity)[] kinds =
            [
                .. stop.Items.SelectMany(i => i.CrateListUnder(cap).Select(size => (size, i.Commodity)))
                    .OrderByDescending(k => k.size).ThenBy(k => k.Commodity, StringComparer.Ordinal),
            ];
            var (fresh, failed) = LayStop(crates, racks, stop.Destination, kinds, mission.Id, plan.FillOrder, budget, stackingUnchecked);
            if (fresh is null)
            {
                return (null, failed, []);
            }

            Insert(crates, stop.Destination, fresh);
        }

        return (crates, null, stackingUnchecked);
    }

    /// <summary>
    /// One stop's crates of <paramref name="kinds"/> laid in around <paramref name="crates"/> by the rule of
    /// <see cref="StopByStop"/>, in the order of <paramref name="kinds"/>; or, if one has no place, the size of the first
    /// that hasn't. Racks whose stacking search gave up are added to <paramref name="stackingUnchecked"/>.
    /// </summary>
    private static (List<Crate>? Fresh, CrateSize? Failed) LayStop(
        IReadOnlyList<Crate> crates, Rack[] racks, string destination, (CrateSize Size, string Commodity)[] kinds, int missionId, FillOrder fillOrder,
        CrateLayout.StackingBudget budget, List<Rack> stackingUnchecked)
    {
        Rack[] candidates =
        [
            .. racks.Where(r => crates.Any(c => c.RackId == r.Id && !c.Delivered && c.Destination == destination))
                .Concat(racks.Where(r => !crates.Any(c => c.RackId == r.Id && !c.Delivered)).Take(1))
                .Concat(racks)
                .Distinct(),
        ];

        List<Crate> fresh = [];
        List<Rack> gaveUp = [];
        if (Best(candidates.Select(r => Laid(r, kinds, []))) is { } whole)
        {
            Take(whole);
        }
        else
        {
            foreach (var kind in kinds)
            {
                if (Best(racks.Select(r => Laid(r, [kind], fresh))) is not { } one)
                {
                    return (null, kind.Size);
                }

                Take(one);
            }
        }

        stackingUnchecked.AddRange(gaveUp.Where(r => !stackingUnchecked.Contains(r)));
        return (fresh, null);

        void Take((Crate[] Crates, Rack Rack, CrateLayout.Laying Laying) laid)
        {
            fresh.AddRange(laid.Crates);
            if (laid.Laying.GaveUp && !gaveUp.Contains(laid.Rack))
            {
                gaveUp.Add(laid.Rack);
            }
        }

        // The kinds laid out in one rack around what lies there and the crates placed for this stop so far; null if they don't fit.
        (Crate[] Crates, Rack Rack, CrateLayout.Laying Laying)? Laid(Rack rack, (CrateSize Size, string Commodity)[] laying, IReadOnlyList<Crate> placedForStop)
        {
            Crate[] occupied = [.. crates.Concat(placedForStop).Where(c => c.RackId == rack.Id)];
            if (CrateLayout.Lay(rack, occupied, [.. laying.Select(k => (k.Size, destination))], fillOrder, budget.For(rack)) is not { } laid)
            {
                return null;
            }

            budget.Spend(rack, laid);
            return ([.. laying.Zip(laid.Poses).Select(p => new Crate(destination, p.First.Commodity, p.First.Size, rack.Id, p.Second.Position, Mission: missionId, Rotated: p.Second.Rotated))],
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
        LoadPlan plan, Mission mission, Rack[] racks, MissionStop[] stops, CrateLayout.StackingBudget budget, CrateCap cap)
    {
        (int Stop, CrateSize Size, string Commodity)[] all =
            [.. stops.SelectMany((stop, s) => stop.Items.SelectMany(item => item.CrateListUnder(cap).Select(size => (s, size, item.Commodity))))];
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
            foreach (var (i, pose) in inRack.Zip(laying.Poses))
            {
                laid[i] = new Crate(stops[all[i].Stop].Destination, all[i].Commodity, all[i].Size, racks[r].Id, pose.Position, Mission: mission.Id, Rotated: pose.Rotated);
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
    private static IReadOnlyList<Crate> Assigned(IReadOnlyList<Crate> crates, IReadOnlyList<Mission> missions, CrateCap cap)
    {
        var owners = missions
            .SelectMany(m => m.Stops.Select(s => s.ToStop(cap)).SelectMany(s => s.Items.SelectMany(i =>
                Enumerable.Repeat((Key: (s.Destination, i.Commodity, i.CrateSize), m.Id), i.Scu / (int)i.CrateSize))))
            .GroupBy(o => o.Key)
            .ToDictionary(g => g.Key, g => new Queue<int>(g.Select(o => o.Id)));
        Crate[] owned = [.. crates.Select(c => c with { Mission = owners[(c.Destination, c.Commodity, c.CrateSize)].Dequeue() })];
        return SavedPlan.WithItems(missions, owned, _ => false, cap);
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
    /// another leaves it floating; an off-ship crate (K2.2) names no rack. A crate on another destination's cargo is no
    /// problem but a hint (D64): hints for the player (shared racks, split stops, stacking) are in <see cref="LoadPlan.Warnings"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or uses a rack the ship doesn't have.</exception>
    public static IReadOnlyList<PlanWarning> Validate(LoadPlan plan, Ship ship)
    {
        plan.EnsureBelongsTo(ship);
        if (plan.Crates.FirstOrDefault(c => !c.OffShip && !ship.Racks.Any(r => r.Id == c.RackId)) is { } stray)
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

        // The rack's minimum (K3.3): the parts would fit the footprint, the rack still refuses their size.
        if (parts.Where(rack.LimitRefuses).Select(p => (CrateSize?)p).FirstOrDefault() is { } refused)
        {
            return Edit.Refused(Invariant($"Rack {rack.Number}: {CrateCap.Of(rack).RackText(refused)}."));
        }

        if (CrateLayout.PlaceWithin(rack, crate, parts, plan.FillOrder) is not { } poses)
        {
            return Edit.Refused(Invariant($"{sum} SCU don't fit the footprint of the {size} SCU crate ({crate.Shape})."));
        }

        // Largest first, as the missions expand them (MissionItem.ToCargoItems). OrderByDescending is stable.
        // The parts keep the crate's mission (D42) and item (D68), and turn where the footprint needs it (D71).
        Crate[] pieces = [.. parts.Zip(poses)
            .OrderByDescending(p => p.First)
            .Select(p => new Crate(crate.Destination, crate.Commodity, p.First, crate.RackId, p.Second.Position, Mission: crate.Mission, Item: crate.Item, Rotated: p.Second.Rotated, Loaded: crate.Loaded))];
        List<Crate> crates = [.. plan.Crates];
        crates.RemoveAt(index);
        crates.InsertRange(index, pieces);

        // The crate's item holds it (SavedPlan checks that).
        return new Edit(
            new SavedPlan(Rebooked(saved.Missions, saved.MissionOf(crate.Mission), crate.Item, [crate.CrateSize], parts, saved.CrateCap), WithHints(plan with { Crates = crates }, ship), saved.CrateCap, saved.Watermark),
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

        // The rack's door (D56, D70): the block may have the shape, the door still refuses the size.
        if (RackOf(ship, first.RackId, nameof(crates)) is var rack && rack.LimitRefuses(merged))
        {
            return Edit.Refused(Invariant($"Rack {rack.Number}: {CrateCap.Of(rack).RackText(merged)}."));
        }

        // The block decides how the new crate lies, not how the parts lay (D71).
        var (problem, rotated) = CrateLayout.Block(crates, merged);
        if (problem is not null)
        {
            return Edit.Refused(problem);
        }

        // The new crate takes the place of the first of them in the plan; the others go, from the back.
        GridPosition corner = new(crates.Min(c => c.Position.Length), crates.Min(c => c.Position.Width), crates.Min(c => c.Position.Stack));
        List<Crate> list = [.. plan.Crates];
        // Loaded is per delivery (K6.4): the parts of one item share it; a mix of both is loaded only where all were.
        list[indices.Min()] = new Crate(first.Destination, first.Commodity, merged, first.RackId, corner, Mission: first.Mission, Item: first.Item, Rotated: rotated, Loaded: crates.All(c => c.Loaded));
        foreach (var index in indices.Order().Skip(1).Reverse())
        {
            list.RemoveAt(index);
        }

        return new Edit(
            new SavedPlan(Rebooked(saved.Missions, mission, first.Item, [.. crates.Select(c => c.CrateSize)], [merged], saved.CrateCap), WithHints(plan with { Crates = list }, ship), saved.CrateCap, saved.Watermark),
            null);
    }

    /// <summary>
    /// The missions with item <paramref name="item"/> (<see cref="Mission.Items"/>) of <paramref name="mission"/> holding
    /// <paramref name="added"/> instead of <paramref name="removed"/>.
    /// </summary>
    private static IReadOnlyList<Mission> Rebooked(
        IReadOnlyList<Mission> missions, Mission mission, int item, IReadOnlyList<CrateSize> removed, IReadOnlyList<CrateSize> added, CrateCap cap)
    {
        var contractItem = mission.Items[item].Item;
        List<CrateSize> crates = [.. contractItem.CrateListUnder(cap)];
        foreach (var size in removed)
        {
            crates.Remove(size);
        }

        var rebooked = mission.WithItem(item, contractItem.WithCrateList([.. crates, .. added], cap));
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
                    $"{(int)item.CrateSize} SCU crate of {item.Commodity} for {stop.Destination} doesn't fit any rack of {ship.Id} ({CrateCap.Of(ship).ShipText(item.CrateSize)})."));
            }
        }
    }

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
