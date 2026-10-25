using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Planning;

/// <summary>
/// Finds room for moved cargo instead of refusing it (D65), stage by stage, each tried only if the one before finds no
/// room: (1) the cell aimed at, fallen along the stack axis; (2) the nearest free, supported place in the target rack;
/// (3) the target rack laid out anew; (3b) other destinations' cargo displaced from the target rack, one level deep;
/// (4) other racks, the dragged cargo's own rack last. Refused only if no rack takes it. Crates left floating where the
/// cargo was lifted settle along the stack axis. Deterministic: no clock, one layout budget per move (D58).
/// </summary>
internal static class MoveResolution
{
    /// <summary>A crate to move (its index in the plan), the rack it is meant for, and the cell for its min corner; null if the gesture names no cell.</summary>
    internal sealed record Intent(int Index, Rack Rack, GridPosition? Position);

    /// <summary>
    /// A crate drag (D25): the crates as one block through stages 1–3b; if that finds no room, crate by crate (largest
    /// first, then by fill rank of the cell aimed at, then plan order) through stages 1–4. A selection is a whole: if one
    /// crate finds no room, nothing moves (D65).
    /// </summary>
    public static MoveOutcome Crates(LoadPlan plan, Ship ship, IReadOnlyList<Intent> intents, MoveStage upTo)
    {
        if (intents.Count == 0)
        {
            return new MoveOutcome(plan, MoveStage.Unchanged, null, []);
        }

        var start = Board.Lifted(plan, ship, [.. intents.Select(i => i.Index)]);
        if (start is null)
        {
            return Refused(plan, intents.Select(i => i.Index));
        }

        Intent[][] groups = [.. intents.GroupBy(i => i.Rack.Index).OrderBy(g => g.Key).Select(g => g.ToArray())];
        if (intents.All(i => i.Position is not null) && Each(start, groups, Gravity) is { } fallen)
        {
            return fallen.Outcome(plan, ship, MoveStage.Gravity, []);
        }

        if (Each(start, groups, FreeSpot) is { } free)
        {
            return free.Outcome(plan, ship, MoveStage.FreeSpot, []);
        }

        if (upTo < MoveStage.Relayout)
        {
            return new MoveOutcome(null, MoveStage.Pending, null, []);
        }

        if (Each(start, groups, (board, group) => Relayout(board, group[0].Rack, Indices(group))) is { } relaid)
        {
            return relaid.Outcome(plan, ship, MoveStage.Relayout, []);
        }

        List<string> notices = [];
        if (Each(start, groups, (board, group) => Relayout(board, group[0].Rack, Indices(group)) ?? Displace(board, group[0].Rack, Indices(group), notices)) is { } displaced)
        {
            return displaced.Outcome(plan, ship, MoveStage.Displace, notices);
        }

        return OneByOne(plan, ship, start, intents);
    }

    /// <summary>Crate by crate through stages 1–4 on the board as the crates before left it; refused, naming the crates without room, if one finds none.</summary>
    private static MoveOutcome OneByOne(LoadPlan plan, Ship ship, Board start, IReadOnlyList<Intent> intents)
    {
        var board = start.Copy();
        var stage = MoveStage.Gravity;
        List<string> notices = [];
        List<int> failed = [];
        Dictionary<Rack, List<string>> elsewhere = [];
        var single = intents.Count == 1;

        foreach (var intent in intents
            .OrderByDescending(i => plan.Crates[i.Index].CrateSize)
            .ThenBy(i => i.Position is { } p ? Rank(board, i.Rack, p) : int.MaxValue)
            .ThenBy(i => i.Index))
        {
            Intent[] one = [intent];
            int[] index = [intent.Index];
            Board? placed = null;
            var at = stage;

            // One crate alone went through stages 1–3b as the block already.
            if (!single)
            {
                List<string> displacing = [];
                (placed, at) = intent.Position is not null && Gravity(board.Copy(), one) is { } fallen ? (fallen, MoveStage.Gravity)
                    : FreeSpot(board.Copy(), one) is { } free ? (free, MoveStage.FreeSpot)
                    : Relayout(board.Copy(), intent.Rack, index) is { } relaid ? (relaid, MoveStage.Relayout)
                    : Displace(board.Copy(), intent.Rack, index, displacing) is { } displaced ? (displaced, MoveStage.Displace)
                    : ((Board?)null, stage);
                notices.AddRange(displacing);
            }

            if (placed is null && Elsewhere(board.Copy(), intent) is { } other)
            {
                (placed, at) = (other, MoveStage.Elsewhere);
                if (!elsewhere.TryGetValue(intent.Rack, out var racks))
                {
                    elsewhere[intent.Rack] = racks = [];
                }

                racks.Add(other.Slots[intent.Index]!.RackId);
            }

            if (placed is null)
            {
                failed.Add(intent.Index);
                continue;
            }

            board = placed;
            stage = at > stage ? at : stage;
        }

        if (failed.Count > 0)
        {
            return Refused(plan, failed);
        }

        notices.AddRange(elsewhere.OrderBy(e => e.Key.Index)
            .Select(e => Invariant($"No room in rack {e.Key.Number} – placed in {RackList(ship, e.Value)}")));
        return board.Outcome(plan, ship, stage, notices);
    }

    /// <summary>The board with every group placed by <paramref name="place"/>, in rack order; null if one group finds no room.</summary>
    private static Board? Each(Board start, Intent[][] groups, Func<Board, Intent[], Board?> place)
    {
        Board? board = start.Copy();
        foreach (var group in groups)
        {
            board = place(board, group);
            if (board is null)
            {
                return null;
            }
        }

        return board;
    }

    private static int[] Indices(Intent[] group) => [.. group.Select(i => i.Index)];

    // --- Stage 1: gravity ---

    /// <summary>
    /// The group at the cells aimed at, as one block, fallen along the stack axis until it touches cargo or the plate;
    /// null if those cells aren't free or the block doesn't rest there with every crate.
    /// </summary>
    private static Board? Gravity(Board board, Intent[] group)
    {
        var rack = group[0].Rack;
        var taken = board.Taken(rack);
        (CrateSize Size, GridPosition Position)[] crates = [.. group.Select(i => (board.Original(i.Index).CrateSize, i.Position!))];
        if (!Free(rack, taken, crates, 0))
        {
            return null;
        }

        var fall = 0;
        while (Free(rack, taken, crates, fall + 1))
        {
            fall++;
        }

        (CrateSize Size, GridPosition Position)[] landed = [.. crates.Select(c => (c.Size, Below(c.Position, fall)))];
        return Rests(taken, landed) ? board.Put(rack, group.Zip(landed, (i, l) => (i.Index, l.Position))) : null;
    }

    /// <summary>True if every crate fits the rack and the grid with its cells <paramref name="fall"/> cells lower, on cells nothing else takes.</summary>
    private static bool Free(Rack rack, bool[,,] taken, IReadOnlyList<(CrateSize Size, GridPosition Position)> crates, int fall) =>
        crates.All(c => c.Position.Stack >= fall && rack.CanHold(c.Size) && Cells(Below(c.Position, fall), c.Size).All(cell => Inside(rack, cell) && !taken[cell.Length, cell.Width, cell.Stack]));

    private static GridPosition Below(GridPosition position, int fall) => new(position.Length, position.Width, position.Stack - fall);

    /// <summary>The support rule (D24) for crates placed together: every bottom cell above the plate on taken cargo or on one of them.</summary>
    private static bool Rests(bool[,,] taken, IReadOnlyList<(CrateSize Size, GridPosition Position)> crates)
    {
        HashSet<GridPosition> own = [.. crates.SelectMany(c => Cells(c.Position, c.Size))];
        return crates.All(c => c.Position.Stack == 0 || Cells(c.Position, c.Size).Where(cell => cell.Stack == c.Position.Stack).All(cell =>
            taken[cell.Length, cell.Width, cell.Stack - 1] || own.Contains(new GridPosition(cell.Length, cell.Width, cell.Stack - 1))));
    }

    // --- Stage 2: the nearest free place ---

    /// <summary>
    /// The group, as one block, at the nearest free, supported place of the target rack: fewest grid steps from the cells
    /// aimed at, then nearest to the destination's own cargo there, then first in fill order. Without cells aimed at, laid
    /// around the cargo there. Nothing else moves; null if there is no such place.
    /// </summary>
    private static Board? FreeSpot(Board board, Intent[] group)
    {
        var rack = group[0].Rack;
        if (group.Any(i => i.Position is null))
        {
            return LayAround(board, rack, Indices(group));
        }

        var taken = board.Taken(rack);
        var grid = rack.Grid;
        var crates = group.Select(i => (board.Original(i.Index).CrateSize, Position: i.Position!, board.Original(i.Index).Destination)).ToArray();
        if (crates.Any(c => !rack.CanHold(c.CrateSize)))
        {
            return null;
        }

        var rank = board.Ranks(rack);
        GridPosition[][] own = [.. crates.Select(c => board.OnBoard(rack).Where(o => o.Destination == c.Destination).SelectMany(o => o.Cells).ToArray())];
        (int Steps, int Own, int First, int Last) best = (int.MaxValue, 0, 0, 0);
        (CrateSize, GridPosition)[]? bestPlace = null;
        for (var dl = -crates.Max(c => c.Position.Length); dl < grid.Length; dl++)
        {
            for (var dw = -crates.Max(c => c.Position.Width); dw < grid.Width; dw++)
            {
                for (var ds = -crates.Max(c => c.Position.Stack); ds < grid.Stack; ds++)
                {
                    var steps = Math.Abs(dl) + Math.Abs(dw) + Math.Abs(ds);
                    if (steps > best.Steps || crates.Any(c => c.Position.Length + dl < 0 || c.Position.Width + dw < 0 || c.Position.Stack + ds < 0))
                    {
                        continue;
                    }

                    (CrateSize Size, GridPosition Position)[] place =
                        [.. crates.Select(c => (c.CrateSize, new GridPosition(c.Position.Length + dl, c.Position.Width + dw, c.Position.Stack + ds)))];
                    if (!Free(rack, taken, place, 0) || !Rests(taken, place))
                    {
                        continue;
                    }

                    var cells = place.SelectMany(p => Cells(p.Position, p.Size)).ToArray();
                    var score = (steps, OwnDistance(own, place),
                        cells.Min(c => rank[c.Length, c.Width, c.Stack]), cells.Max(c => rank[c.Length, c.Width, c.Stack]));
                    if (score.CompareTo(best) < 0)
                    {
                        (best, bestPlace) = (score, place);
                    }
                }
            }
        }

        return bestPlace is null ? null : board.Put(rack, group.Zip(bestPlace, (i, p) => (i.Index, p.Item2)));
    }

    /// <summary>For each crate the grid steps from its cells to the nearest cell of its destination's cargo in the rack (0 without any), summed.</summary>
    private static int OwnDistance(GridPosition[][] own, (CrateSize Size, GridPosition Position)[] place)
    {
        var total = 0;
        for (var i = 0; i < place.Length; i++)
        {
            if (own[i].Length > 0)
            {
                total += Cells(place[i].Position, place[i].Size)
                    .Min(cell => own[i].Min(o => Math.Abs(o.Length - cell.Length) + Math.Abs(o.Width - cell.Width) + Math.Abs(o.Stack - cell.Stack)));
            }
        }

        return total;
    }

    /// <summary>The crates laid into the free cells around the rack's cargo (<see cref="CrateLayout.Lay"/>); null if they don't fit.</summary>
    private static Board? LayAround(Board board, Rack rack, IReadOnlyList<int> crates)
    {
        var laid = board.Lay(rack, board.OnBoard(rack), crates);
        return laid is null ? null : board.Put(rack, crates.Zip(laid.Positions), laid.GaveUp);
    }

    // --- Stage 3: the target rack laid out anew ---

    /// <summary>The rack's cargo on board and the crates laid out together, in plan order (<see cref="CrateLayout.Lay"/>); null if they don't fit.</summary>
    private static Board? Relayout(Board board, Rack rack, IReadOnlyList<int> crates)
    {
        int[] all = [.. board.IndicesIn(rack).Concat(crates).Order()];
        var laid = board.Lay(rack, [], all);
        return laid is null ? null : board.Put(rack, all.Zip(laid.Positions), laid.GaveUp);
    }

    // --- Stage 3b: displacing other cargo ---

    /// <summary>
    /// Room in the rack by taking other destinations' crates out: destinations with the fewest SCU there first (name on a
    /// tie), largest crates first; the shortest such list after which the rack lays out with the crates and every crate
    /// taken out finds a place in another rack around the cargo there (<see cref="PlaceOut"/>). One level: nothing taken
    /// out displaces anything. Adds "Moved 2 crates for X to rack 1 to make room" per destination; null without such a list.
    /// </summary>
    private static Board? Displace(Board board, Rack rack, IReadOnlyList<int> crates, List<string> notices)
    {
        HashSet<string> moving = [.. crates.Select(i => board.Original(i).Destination)];
        int[] others =
        [
            .. board.IndicesIn(rack)
                .Where(i => !moving.Contains(board.Slots[i]!.Destination))
                .GroupBy(i => board.Slots[i]!.Destination)
                .OrderBy(g => g.Sum(i => (int)board.Slots[i]!.CrateSize))
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .SelectMany(g => g.OrderByDescending(i => board.Slots[i]!.CrateSize).ThenBy(i => i)),
        ];

        // A list that leaves fewer SCU free than the crates need can't lay out: no layout to try for it.
        var missing = board.OnBoard(rack).Sum(c => (int)c.CrateSize) + crates.Sum(i => (int)board.Original(i).CrateSize) - rack.CapacityScu;
        for (var count = 1; count <= others.Length; count++)
        {
            int[] displaced = others[..count];
            if (displaced.Sum(i => (int)board.Slots[i]!.CrateSize) < missing)
            {
                continue;
            }

            var trial = board.Copy();
            trial.Lift(displaced);
            if (Relayout(trial, rack, crates) is not { } relaid || PlaceOut(relaid, rack, displaced) is not { } placed)
            {
                continue;
            }

            notices.AddRange(displaced.GroupBy(i => board.Slots[i]!.Destination).Select(g => Invariant(
                $"Moved {CountOf(g.Count())} for {g.Key} to {RackList(placed.Ship, g.Select(i => placed.Slots[i]!.RackId))} to make room")));
            return placed;
        }

        return null;
    }

    /// <summary>
    /// The crates taken out of <paramref name="from"/> into other racks around the cargo there, as <c>PlaceInto</c> places
    /// a stop (D41, D64): per destination first whole into one rack, else crate by crate, racks tried in the order of
    /// <see cref="Candidates"/>; null if one finds no place.
    /// </summary>
    private static Board? PlaceOut(Board board, Rack from, int[] crates)
    {
        foreach (var destination in crates.GroupBy(i => board.Original(i).Destination))
        {
            Rack[] candidates = Candidates(board, destination.Key, [from], []);
            int[] all = [.. destination];
            if (Best(board, candidates, all) is { } whole)
            {
                board = whole;
                continue;
            }

            foreach (var crate in all.OrderByDescending(i => board.Original(i).CrateSize).ThenBy(i => i))
            {
                if (Best(board, candidates, [crate]) is not { } one)
                {
                    return null;
                }

                board = one;
            }
        }

        return board;
    }

    // --- Stage 4: other racks ---

    /// <summary>The crate whole into another rack around the cargo there: racks as <see cref="Candidates"/> orders them, its own rack last.</summary>
    private static Board? Elsewhere(Board board, Intent intent)
    {
        var own = board.Rack(board.Original(intent.Index).RackId);
        Rack[] last = own == intent.Rack ? [] : [own];
        return Best(board, Candidates(board, board.Original(intent.Index).Destination, [intent.Rack, own], last), [intent.Index]);
    }

    /// <summary>
    /// Racks to try for a destination's crates, the D41 order: racks with its cargo on board, the first rack with nothing
    /// on board, then rack order; without <paramref name="excluded"/>, and <paramref name="last"/> at the end.
    /// </summary>
    private static Rack[] Candidates(Board board, string destination, Rack[] excluded, Rack[] last)
    {
        Rack[] open = [.. board.Racks.Where(r => !excluded.Contains(r))];
        return
        [
            .. open.Where(r => board.OnBoard(r).Any(c => c.Destination == destination))
                .Concat(open.Where(r => !board.OnBoard(r).Any()).Take(1))
                .Concat(open)
                .Distinct(),
            .. last,
        ];
    }

    /// <summary>Of the racks in order, the first where the crates lie on no other destination's cargo, else the first that takes them (<c>LoadPlanner.Best</c>, D64).</summary>
    private static Board? Best(Board board, Rack[] racks, int[] crates)
    {
        (Board Board, CrateLayout.Laying Laying)? first = null;
        foreach (var rack in racks)
        {
            if (board.Lay(rack, board.OnBoard(rack), crates) is not { } laid)
            {
                continue;
            }

            var placed = board.Copy().Put(rack, crates.Zip(laid.Positions), laid.GaveUp);
            if (laid.StackingFree)
            {
                return placed;
            }

            first ??= (placed, laid);
        }

        return first?.Board;
    }

    // --- Refusal and texts ---

    /// <summary>"no room for 4 SCU crate", or "no room for 3 crates (10 SCU)", for the crates that found none.</summary>
    private static MoveOutcome Refused(LoadPlan plan, IEnumerable<int> crates)
    {
        CrateSize[] sizes = [.. crates.Select(i => plan.Crates[i].CrateSize)];
        var text = sizes.Length == 1
            ? Invariant($"no room for {(int)sizes[0]} SCU crate")
            : Invariant($"no room for {sizes.Length} crates ({sizes.Sum(s => (int)s)} SCU)");
        return new MoveOutcome(null, MoveStage.Refused, text, []);
    }

    private static string CountOf(int count) => Invariant($"{count} crate{(count == 1 ? "" : "s")}");

    /// <summary>"rack 2" or "racks 1+3", in rack order, each once.</summary>
    private static string RackList(Ship ship, IEnumerable<string> rackIds)
    {
        HashSet<string> ids = [.. rackIds];
        int[] numbers = [.. ship.Racks.Where(r => ids.Contains(r.Id)).OrderBy(r => r.Index).Select(r => r.Number)];
        return Invariant($"rack{(numbers.Length == 1 ? "" : "s")} {string.Join("+", numbers)}");
    }

    private static int Rank(Board board, Rack rack, GridPosition position) =>
        Inside(rack, position) ? board.Ranks(rack)[position.Length, position.Width, position.Stack] : int.MaxValue;

    private static IEnumerable<GridPosition> Cells(GridPosition position, CrateSize size)
    {
        var dimensions = size.Dimensions;
        return from l in Enumerable.Range(position.Length, dimensions.Length)
               from w in Enumerable.Range(position.Width, dimensions.Width)
               from s in Enumerable.Range(position.Stack, dimensions.Stack)
               select new GridPosition(l, w, s);
    }

    private static bool Inside(Rack rack, GridPosition cell) =>
        cell.Length < rack.Grid.Length && cell.Width < rack.Grid.Width && cell.Stack < rack.Grid.Stack;

    /// <summary>
    /// The plan while a move is worked out: every crate at its index, a lifted one as null until it lands. Copied for every
    /// try, so a stage that finds no room leaves nothing behind. The layout budget is shared by all copies (D58).
    /// </summary>
    private sealed class Board
    {
        private readonly LoadPlan _plan;
        private readonly CrateLayout.StackingBudget _budget;
        private readonly Dictionary<Rack, int[,,]> _ranks;
        private readonly HashSet<Rack> _gaveUp;

        private Board(LoadPlan plan, Ship ship, Rack[] racks, Crate?[] slots, CrateLayout.StackingBudget budget, Dictionary<Rack, int[,,]> ranks, HashSet<Rack> gaveUp) =>
            (_plan, Ship, Racks, Slots, _budget, _ranks, _gaveUp) = (plan, ship, racks, slots, budget, ranks, gaveUp);

        public Ship Ship { get; }

        public Rack[] Racks { get; }

        public Crate?[] Slots { get; }

        /// <summary>
        /// The plan with <paramref name="lifted"/> taken off and every crate left floating in their racks settled along the
        /// stack axis: down to the first cargo it touches, if it rests there; else to the nearest free place in its rack,
        /// else its rack laid out anew. Null if that rack can't be laid out.
        /// </summary>
        public static Board? Lifted(LoadPlan plan, Ship ship, IReadOnlyList<int> lifted)
        {
            Board board = new(plan, ship, [.. ship.Racks.OrderBy(r => r.Index)], [.. plan.Crates], new CrateLayout.StackingBudget(), [], []);
            board.Lift(lifted);
            foreach (var rack in board.Racks.Where(r => lifted.Any(i => plan.Crates[i].RackId == r.Id)))
            {
                if (board.Settle(rack) is not { } settled)
                {
                    return null;
                }

                board = settled;
            }

            return board;
        }

        public Board Copy() => new(_plan, Ship, Racks, [.. Slots], _budget, _ranks, [.. _gaveUp]);

        public Crate Original(int index) => _plan.Crates[index];

        public Rack Rack(string id) => Racks.Single(r => r.Id == id);

        public void Lift(IEnumerable<int> crates)
        {
            foreach (var i in crates)
            {
                Slots[i] = null;
            }
        }

        public IEnumerable<int> IndicesIn(Rack rack) =>
            Enumerable.Range(0, Slots.Length).Where(i => Slots[i] is { Delivered: false } c && c.RackId == rack.Id);

        /// <summary>The rack's crates still on board and not lifted; delivered cargo is off the ship (D24 addendum).</summary>
        public Crate[] OnBoard(Rack rack) => [.. IndicesIn(rack).Select(i => Slots[i]!)];

        public bool[,,] Taken(Rack rack)
        {
            var grid = rack.Grid;
            var taken = new bool[grid.Length, grid.Width, grid.Stack];
            foreach (var cell in OnBoard(rack).SelectMany(c => c.Cells).Where(c => Inside(rack, c)))
            {
                taken[cell.Length, cell.Width, cell.Stack] = true;
            }

            return taken;
        }

        public int[,,] Ranks(Rack rack) => _ranks.TryGetValue(rack, out var rank) ? rank : _ranks[rack] = CrateLayout.Ranks(rack, _plan.FillOrder);

        /// <summary><see cref="CrateLayout.Lay"/> of the crates at <paramref name="crates"/> around <paramref name="occupied"/>, on the move's budget.</summary>
        public CrateLayout.Laying? Lay(Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<int> crates)
        {
            var laid = CrateLayout.Lay(rack, occupied, [.. crates.Select(i => (Original(i).CrateSize, Original(i).Destination))], _plan.FillOrder, _budget.For(rack));
            if (laid is not null)
            {
                _budget.Spend(rack, laid);
            }

            return laid;
        }

        /// <summary>The crates at these indices into the rack at these positions; this board, changed.</summary>
        public Board Put(Rack rack, IEnumerable<(int Index, GridPosition Position)> places, bool gaveUp = false)
        {
            foreach (var (index, position) in places)
            {
                Slots[index] = (Slots[index] ?? Original(index)) with { RackId = rack.Id, Position = position };
            }

            if (gaveUp)
            {
                _gaveUp.Add(rack);
            }

            return this;
        }

        /// <summary>The plan of this board with its hints derived anew and a note for every rack whose search gave up (D54).</summary>
        public MoveOutcome Outcome(LoadPlan plan, Ship ship, MoveStage stage, IReadOnlyList<string> notices) =>
            new(LoadPlanner.WithHints(plan with { Crates = [.. Slots.Select(c => c!)] }, ship, LoadPlanner.StackingUncheckedNotes(_gaveUp.OrderBy(r => r.Index))),
                stage, null, notices);

        /// <summary>
        /// The lowest crate of the rack (plan order on a tie) with a bottom cell above nothing (D24); null if none floats.
        /// A crate's own cells never lie below its bottom, so one grid of taken cells answers for every crate.
        /// </summary>
        private int? FirstFloating(Rack rack)
        {
            var taken = Taken(rack);
            return IndicesIn(rack)
                .Where(i => Slots[i]!.Position.Stack > 0 && Slots[i]!.Bottom.Any(c => !Inside(rack, c) || !taken[c.Length, c.Width, c.Stack - 1]))
                .OrderBy(i => Slots[i]!.Position.Stack).ThenBy(i => i)
                .Select(i => (int?)i)
                .FirstOrDefault();
        }

        /// <summary>Every floating crate of the rack settled, lowest first, until none floats; null if one finds no place.</summary>
        private Board? Settle(Rack rack)
        {
            var board = this;
            while (board.FirstFloating(rack) is { } index)
            {
                var floating = board.Slots[index]!;
                board.Slots[index] = null;
                var taken = board.Taken(rack);
                (CrateSize, GridPosition)[] alone = [(floating.CrateSize, floating.Position)];
                var fall = 0;
                while (Free(rack, taken, alone, fall + 1))
                {
                    fall++;
                }

                Intent[] self = [new(index, rack, floating.Position)];
                if (fall > 0 && Rests(taken, [(floating.CrateSize, Below(floating.Position, fall))]))
                {
                    board.Slots[index] = floating with { Position = Below(floating.Position, fall) };
                }
                else if ((FreeSpot(board.Copy(), self) ?? Relayout(board.Copy(), rack, [index])) is { } placed)
                {
                    board = placed;
                }
                else
                {
                    return null;
                }
            }

            return board;
        }
    }
}
