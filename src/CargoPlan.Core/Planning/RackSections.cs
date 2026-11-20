using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Planning;

/// <summary>
/// The planner's sections (D89): in a rack holding more than one destination, each destination's crates as a block of
/// whole rows, in the legend's order, with the plan's empty rows between them where they fit. Runs after the planner has decided who waits and which crate goes into which rack,
/// so it never changes either: capacity comes first. It moves planned crates only, within their rack; loaded crates are
/// obstacles, delivered and off-ship crates take no room and stay as they are (D24, K2.2). A section is
/// <see cref="CrateLayout.Lay"/> with every cell outside its rows taken: no packer of its own.
/// </summary>
internal static class RackSections
{
    /// <summary>
    /// A rack's rows, in the order sections take them. Crosswise from the end <paramref name="order"/> names; lengthwise
    /// from starboard. A rack stacking along y runs its sections along the length whatever <paramref name="rows"/> says:
    /// its width is two cells at most on the ships that have it and stands upright (D89). With <paramref name="lanes"/>
    /// (D116) the rows of the first lane, then of the second, then of the middle: crosswise a row is a length within one
    /// lane, lengthwise a width, each lane from its outer edge.
    /// </summary>
    internal readonly record struct RowAxis(bool AlongLength, IReadOnlyList<int> Rows, Lanes? Lanes = null, int Length = 0)
    {
        public static RowAxis Of(Rack rack, FillOrder order, FillRows rows, Lanes? lanes = null)
        {
            var alongLength = rows == FillRows.Crosswise || rack.StacksSideways;
            var count = alongLength ? rack.Grid.Length : rack.Grid.Width;
            int[] along = alongLength && order == FillOrder.BowFirst
                ? [.. Enumerable.Range(0, count).Reverse()]
                : [.. Enumerable.Range(0, count)];
            if (lanes is null)
            {
                return new RowAxis(alongLength, along);
            }

            var length = rack.Grid.Length;
            return new RowAxis(alongLength, alongLength
                ? [.. Enumerable.Range(0, lanes.Count).SelectMany(lane => along.Select(l => (lane * length) + l))]
                : [.. Enumerable.Range(0, lanes.Count).SelectMany(lanes.Breadth)], lanes, length);
        }

        public int RowOf(GridPosition cell) => AlongLength ? (Lanes is null ? cell.Length : (Lanes.LaneOf(cell.Width) * Length) + cell.Length) : cell.Width;

        /// <summary>The lane a row lies in; 0 without lanes.</summary>
        public int LaneOfRow(int row) => Lanes is null ? 0 : AlongLength ? row / Length : Lanes.LaneOf(row);

        /// <summary>The places in <see cref="Rows"/> where each lane starts, and the end, in lane order.</summary>
        public int[] LaneStarts()
        {
            var axis = this;
            return [.. Enumerable.Range(0, Rows.Count).Where(i => i == 0 || axis.LaneOfRow(axis.Rows[i]) != axis.LaneOfRow(axis.Rows[i - 1])), Rows.Count];
        }

        /// <summary>
        /// Where the next section starts after one ending before <paramref name="next"/>: <paramref name="gap"/> rows on,
        /// but never into the next lane, and no gap at a lane's start: the gap lies between sections of one side (D116).
        /// </summary>
        public int After(int next, int gap)
        {
            if (Lanes is null)
            {
                return next + gap;
            }

            var end = LaneStarts().First(s => s >= next);
            return end == next ? next : Math.Min(next + gap, end);
        }
    }

    /// <summary>One or, once they share (rule 3), several destinations' crates, by their index in the plan.</summary>
    private sealed record Section(IReadOnlyList<string> Destinations, int[] Crates, int Scu, int Place);

    /// <summary>
    /// The plan with its shared racks laid in sections, and the notes they give. A rack that can't be laid so stays as the
    /// planner laid it. <paramref name="destinations"/> is the legend's order.
    /// </summary>
    public static LoadPlan Lay(LoadPlan plan, Ship ship, IReadOnlyList<string> destinations) => LaidOut(plan, ship, destinations).Plan;

    /// <summary>
    /// <see cref="Lay"/>, and the destinations that fit neither side of their rack whole though the plan has no overflow
    /// (D116): they lie across both sides with a note, for the caller to let their missions wait.
    /// </summary>
    public static (LoadPlan Plan, IReadOnlyList<string> Crossing) LaidOut(LoadPlan plan, Ship ship, IReadOnlyList<string> destinations)
    {
        List<string> crossing = [];
        Crate[] crates = [.. plan.Crates];
        List<string> legend = [.. destinations];
        var budget = new CrateLayout.StackingBudget();
        List<PlanWarning> notes = [];
        HashSet<string> sectioned = [];
        foreach (var rack in ship.Racks.OrderBy(r => r.Index))
        {
            int[] onBoard = [.. Enumerable.Range(0, crates.Length).Where(i => crates[i].RackId == rack.Id && !crates[i].Delivered)];
            int[] movable = [.. onBoard.Where(i => !crates[i].Loaded)];
            if (movable.Length == 0 || onBoard.Select(i => crates[i].Destination).Distinct().Count() < 2)
            {
                continue;
            }

            var arrangement = new Arrangement(rack, plan, crates, [.. onBoard.Where(i => crates[i].Loaded).Select(i => crates[i])], budget);
            var arranged = arrangement
                .Run([.. movable.GroupBy(i => crates[i].Destination)
                    .OrderBy(g => legend.IndexOf(g.Key))
                    .Select((g, place) => new Section([g.Key], [.. g], g.Sum(i => (int)crates[i].CrateSize), place))]);

            notes.AddRange(arranged.Notes);
            crossing.AddRange(arrangement.Crossing);
            if (arranged.Poses is { } poses)
            {
                sectioned.Add(rack.Id);
                foreach (var (index, pose) in poses)
                {
                    crates[index] = crates[index] with { Position = pose.Position, Rotated = pose.Rotated };
                }

                if (arranged.GaveUp)
                {
                    notes.AddRange(LoadPlanner.StackingUncheckedNotes([rack]));
                }
            }
        }

        // A rack laid anew has no use for what the search said about the layout before it.
        return (LoadPlanner.WithHints(plan with { Crates = crates, Warnings = [.. plan.Warnings.Where(w => !sectioned.Contains(w.RackId))] }, ship, notes), crossing);
    }

    /// <summary>
    /// The rows a captured stop may take in a rack it shares (D41, D89), as stages: a stage is tried whole, unturned then
    /// turned, before the next, so a turned crate keeps a gap rather than an unturned one fills it (Martin: grip before
    /// handle). A destination on board has one stage: its rows, then those and the empty rows next to them, one at a time,
    /// towards the fill end first; so it grows into its gap. A new destination goes last, as a Plan would lay it: into the
    /// free rest at the end, <paramref name="gap"/> rows away from the last cargo, then closer; only then into the empty
    /// rows before that, gaps included.
    /// </summary>
    /// <param name="onBoard">The crates on board in this rack.</param>
    /// <param name="lanes">The rack's lanes (D116): rows run side by side, no gap at a lane's start.</param>
    /// <param name="oneSide">Without overflow (D116): only rows of one lane, and a new destination may start at a later lane.</param>
    internal static IEnumerable<IReadOnlyList<IReadOnlyList<int>>> RowsFor(
        Rack rack, IReadOnlyList<Crate> onBoard, string destination, FillOrder order, FillRows rows, int gap, Lanes? lanes = null, bool oneSide = false)
    {
        var axis = RowAxis.Of(rack, order, rows, lanes);
        if (!oneSide || lanes is null)
        {
            return Stages(axis, onBoard, destination, gap);
        }

        // One side: each candidate within one lane; a destination new to the rack may also start where a later lane does.
        var starts = axis.LaneStarts();
        bool OneLane(IReadOnlyList<int> run) => run.Select(axis.LaneOfRow).Distinct().Count() == 1;
        HashSet<int> taken = [.. onBoard.SelectMany(c => c.Cells).Select(axis.RowOf)];
        var last = Enumerable.Range(0, axis.Rows.Count).LastOrDefault(i => taken.Contains(axis.Rows[i]), -1);
        IReadOnlyList<IReadOnlyList<int>>[] later =
        [
            .. starts.Take(starts.Length - 1).Select((start, lane) => (start, end: starts[lane + 1]))
                .Where(l => l.start > last && !onBoard.Any(c => c.Destination == destination))
                .Select(l => (IReadOnlyList<IReadOnlyList<int>>)[.. Enumerable.Range(1, l.end - l.start).Select(count => (IReadOnlyList<int>)[.. axis.Rows.Skip(l.start).Take(count)])]),
        ];
        return Stages(axis, onBoard, destination, gap).Select(stage => (IReadOnlyList<IReadOnlyList<int>>)[.. stage.Where(OneLane)]).Concat(later);
    }

    private static IEnumerable<IReadOnlyList<IReadOnlyList<int>>> Stages(RowAxis axis, IReadOnlyList<Crate> onBoard, string destination, int gap)
    {
        HashSet<int> taken = [.. onBoard.SelectMany(c => c.Cells).Select(axis.RowOf)];
        HashSet<int> own = [.. onBoard.Where(c => c.Destination == destination).SelectMany(c => c.Cells).Select(axis.RowOf)];
        if (own.Count > 0)
        {
            yield return [.. OwnRows(axis, taken, own)];
            yield break;
        }

        var last = Enumerable.Range(0, axis.Rows.Count).LastOrDefault(i => taken.Contains(axis.Rows[i]), -1);
        for (var away = gap; away >= 0; away--)
        {
            var from = last + 1 + away;
            if (from < axis.Rows.Count && (away == 0 || last < 0 || axis.After(last + 1, away) == from))
            {
                yield return [.. Enumerable.Range(1, axis.Rows.Count - from).Select(count => (IReadOnlyList<int>)[.. axis.Rows.Skip(from).Take(count)])];
            }
        }

        List<IReadOnlyList<int>> earlier = [];
        for (var start = 0; start <= last; start++)
        {
            var run = 0;
            while (start + run <= last && !taken.Contains(axis.Rows[start + run]))
            {
                run++;
                earlier.Add([.. axis.Rows.Skip(start).Take(run)]);
            }

            start += run;
        }

        yield return earlier;
    }

    private static IEnumerable<IReadOnlyList<int>> OwnRows(RowAxis axis, HashSet<int> taken, HashSet<int> own)
    {
        int[] places = [.. Enumerable.Range(0, axis.Rows.Count).Where(i => own.Contains(axis.Rows[i]))];
        var (low, high) = (places[0], places[^1]);
        yield return [.. axis.Rows.Skip(low).Take(high - low + 1)];
        while (true)
        {
            if (high + 1 < axis.Rows.Count && !taken.Contains(axis.Rows[high + 1]))
            {
                high++;
            }
            else if (low > 0 && !taken.Contains(axis.Rows[low - 1]))
            {
                low--;
            }
            else
            {
                yield break;
            }

            yield return [.. axis.Rows.Skip(low).Take(high - low + 1)];
        }
    }

    /// <summary>The cells outside <paramref name="free"/>, as crates, for <see cref="CrateLayout.Lay"/> to lay around.</summary>
    internal static IEnumerable<Crate> Outside(Rack rack, RowAxis axis, IReadOnlySet<int> free)
    {
        for (var length = 0; length < rack.Grid.Length; length++)
        {
            for (var width = 0; width < rack.Grid.Width; width++)
            {
                for (var stack = 0; stack < rack.Grid.Stack; stack++)
                {
                    GridPosition cell = new(length, width, stack);
                    if (!free.Contains(axis.RowOf(cell)))
                    {
                        yield return new Crate("", "", CrateSize.One, rack.Id, cell);
                    }
                }
            }
        }
    }

    /// <summary>
    /// True if an empty cell of <paramref name="rack"/> lies between crates of <paramref name="destination"/> (B17): in its
    /// layer, the nearest crate on both sides along the length, or along the width, is that destination's. A section is a
    /// block without one; only cargo that may not move leaves one (<see cref="SectionNotes.Gap"/>).
    /// </summary>
    /// <param name="lanes">The rack's lanes (D116): along the width a block ends at its lane.</param>
    internal static bool Encloses(Rack rack, IEnumerable<Crate> onBoard, string destination, Lanes? lanes = null)
    {
        var grid = rack.Grid;
        var owner = new string?[grid.Length, grid.Width, grid.Stack];
        foreach (var crate in onBoard.Where(c => c.RackId == rack.Id && !c.Delivered))
        {
            foreach (var cell in crate.Cells.Where(c => c.Length < grid.Length && c.Width < grid.Width && c.Stack < grid.Stack))
            {
                owner[cell.Length, cell.Width, cell.Stack] = crate.Destination;
            }
        }

        for (var l = 0; l < grid.Length; l++)
        {
            for (var w = 0; w < grid.Width; w++)
            {
                for (var s = 0; s < grid.Stack; s++)
                {
                    if (owner[l, w, s] is null
                        && ((Nearest(l, w, s, -1, 0) == destination && Nearest(l, w, s, 1, 0) == destination)
                            || (Nearest(l, w, s, 0, -1) == destination && Nearest(l, w, s, 0, 1) == destination)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;

        string? Nearest(int l, int w, int s, int dl, int dw)
        {
            for (var (x, y) = (l + dl, w + dw); x >= 0 && y >= 0 && x < grid.Length && y < grid.Width && (lanes is null || lanes.LaneOf(y) == lanes.LaneOf(w)); (x, y) = (x + dl, y + dw))
            {
                if (owner[x, y, s] is { } found)
                {
                    return found;
                }
            }

            return null;
        }
    }

    /// <summary>One rack's search for sections: every step of the rules in D89, each try from the rack's first row.</summary>
    private sealed class Arrangement(Rack rack, LoadPlan plan, Crate[] crates, IReadOnlyList<Crate> loaded, CrateLayout.StackingBudget budget)
    {
        private readonly Lanes? _lanes = Lanes.Of(rack, plan.Sides);
        private readonly RowAxis _axis = RowAxis.Of(rack, plan.FillOrder, plan.FillRows, Lanes.Of(rack, plan.Sides));
        private readonly HashSet<string> _turned = [];

        /// <summary>
        /// The destinations that fit neither side whole though the plan has no overflow (D116): laid across both sides, or,
        /// where not even that fits, left as the planner laid the rack, for the caller to let their missions wait.
        /// </summary>
        public IReadOnlyList<string> Crossing { get; private set; } = [];

        public (IReadOnlyList<(int Index, Pose Pose)>? Poses, IReadOnlyList<PlanWarning> Notes, bool GaveUp) Run(List<Section> sections)
        {
            List<PlanWarning> notes = [];
            var gap = plan.SectionGap;
            Crossing = [];

            // D71: a crate lies unturned where it can, so turned only what no row of this rack takes otherwise.
            _turned.UnionWith(sections.Where(s => Laid(s, _axis.Rows, loaded, turn: false) is null).SelectMany(s => s.Destinations));
            while (true)
            {
                if (Attempt(sections, gap) is { } done)
                {
                    Crossing = done.Crossing;
                    return (done.Poses, [.. notes, .. done.Crossing.Select(d => SectionNotes.Crosses(rack, d))], done.GaveUp);
                }

                if (TurnOneMore(sections))
                {
                    continue;
                }

                // Gaps go before anyone shares and after turning (Martin: grip before handle); the turns stay. No note: the picture says it.
                if (gap > 0)
                {
                    gap--;
                    continue;
                }

                // All in one section is no section: the rack as the planner laid it, and nobody shares anything then.
                if (sections.Count == 1)
                {
                    return (null, [SectionNotes.NoSections(rack)], false);
                }

                notes.Add(Merge(sections));
                if (sections.Count == 1)
                {
                    return (null, [SectionNotes.NoSections(rack)], false);
                }
            }
        }

        /// <summary>
        /// Every section at its smallest size from the first free row on, <paramref name="gap"/> rows on from the last; null if
        /// one finds no room. A row with loaded cargo behind a section counts as its gap like any other (Martin): no row on
        /// top of it, or a loaded crate would push the sections behind it back.
        /// </summary>
        private (IReadOnlyList<(int Index, Pose Pose)>? Poses, bool GaveUp, IReadOnlyList<string> Crossing)? Attempt(IReadOnlyList<Section> sections, int gap)
        {
            if (_lanes is not null && !plan.SideOverflow)
            {
                return OneSide(sections, gap);
            }

            List<Crate> occupied = [.. loaded];
            List<(int, Pose)> poses = [];
            var gaveUp = false;
            var cursor = 0;
            foreach (var section in sections.OrderBy(s => s.Place))
            {
                if (Smallest(section, [.. _axis.Rows.Skip(cursor)], occupied) is not { } found)
                {
                    return null;
                }

                cursor = _axis.After(cursor + found.Count, gap);
                gaveUp |= found.Laying.GaveUp;
                Place(section, found.Laying, poses, occupied);
            }

            return (poses, gaveUp, []);
        }

        /// <summary>
        /// Without overflow (D116): each section whole into the first lane that takes it after the sections there, in the
        /// legend's order, so a later small one goes back to the first side where it fits. A section no lane takes whole goes
        /// last into the free rows of all lanes, across both sides; if not even that fits, no poses: the rack stays as the
        /// planner laid it, and the crossing destinations say why.
        /// </summary>
        private (IReadOnlyList<(int Index, Pose Pose)>? Poses, bool GaveUp, IReadOnlyList<string> Crossing)? OneSide(IReadOnlyList<Section> sections, int gap)
        {
            var starts = _axis.LaneStarts();
            var cursors = starts[..^1];
            List<Crate> occupied = [.. loaded];
            List<(int, Pose)> poses = [];
            List<Section> across = [];
            var gaveUp = false;
            foreach (var section in sections.OrderBy(s => s.Place))
            {
                var lane = Enumerable.Range(0, cursors.Length)
                    .Select(l => (Lane: l, Found: Smallest(section, [.. _axis.Rows.Take(starts[l + 1]).Skip(cursors[l])], occupied)))
                    .FirstOrDefault(l => l.Found is not null);
                if (lane.Found is not { } found)
                {
                    across.Add(section);
                    continue;
                }

                cursors[lane.Lane] = Math.Min(cursors[lane.Lane] + found.Count + gap, starts[lane.Lane + 1]);
                gaveUp |= found.Laying.GaveUp;
                Place(section, found.Laying, poses, occupied);
            }

            foreach (var section in across)
            {
                int[] free = [.. Enumerable.Range(0, cursors.Length).SelectMany(l => _axis.Rows.Take(starts[l + 1]).Skip(cursors[l]))];
                if (Smallest(section, free, occupied) is not { } found)
                {
                    return (null, gaveUp, [.. across.SelectMany(s => s.Destinations)]);
                }

                HashSet<int> used = [.. free.Take(found.Count)];
                for (var l = 0; l < cursors.Length; l++)
                {
                    while (cursors[l] < starts[l + 1] && used.Contains(_axis.Rows[cursors[l]]))
                    {
                        cursors[l]++;
                    }
                }

                gaveUp |= found.Laying.GaveUp;
                Place(section, found.Laying, poses, occupied);
            }

            return (poses, gaveUp, [.. across.SelectMany(s => s.Destinations)]);
        }

        private void Place(Section section, CrateLayout.Laying laying, List<(int, Pose)> poses, List<Crate> occupied)
        {
            poses.AddRange(section.Crates.Zip(laying.Poses));
            occupied.AddRange(section.Crates.Zip(laying.Poses).Select(p => crates[p.First] with { Position = p.Second.Position, Rotated = p.Second.Rotated }));
        }

        /// <summary>The fewest of <paramref name="rows"/>, from their start, that take the section: without a crate on other cargo if any count does, else the first that fits.</summary>
        private (int Count, CrateLayout.Laying Laying)? Smallest(Section section, IReadOnlyList<int> rows, IReadOnlyList<Crate> occupied)
        {
            var turn = section.Destinations.Any(_turned.Contains);
            (int, CrateLayout.Laying)? first = null;
            for (var count = 1; count <= rows.Count; count++)
            {
                if (Laid(section, [.. rows.Take(count)], occupied, turn) is { } laying)
                {
                    if (laying.StackingFree)
                    {
                        return (count, laying);
                    }

                    first ??= (count, laying);
                }
            }

            return first;
        }

        private CrateLayout.Laying? Laid(Section section, IReadOnlyList<int> rows, IReadOnlyList<Crate> occupied, bool turn)
        {
            var laying = CrateLayout.Lay(
                rack, [.. occupied, .. Outside(rack, _axis, rows.ToHashSet())],
                [.. section.Crates.Select(i => (crates[i].CrateSize, crates[i].Destination))], plan.FillOrder, plan.FillRows, budget.For(rack), turn, snug: true, _lanes);
            if (laying is not null)
            {
                budget.Spend(rack, laying);
            }

            return laying;
        }

        /// <summary>
        /// Turns the crates of the smallest section whose rows that makes fewer (Martin: a turned crate costs a handle, a
        /// shared section what K9 is for); false if none is left.
        /// </summary>
        private bool TurnOneMore(IReadOnlyList<Section> sections)
        {
            foreach (var section in Smallest(sections).Where(s => !s.Destinations.Any(_turned.Contains)))
            {
                var unturned = RowsAlone(section, turn: false);
                var turned = RowsAlone(section, turn: true);
                if (turned < unturned)
                {
                    _turned.UnionWith(section.Destinations);
                    return true;
                }
            }

            return false;
        }

        private int RowsAlone(Section section, bool turn)
        {
            for (var count = 1; count <= _axis.Rows.Count; count++)
            {
                if (Laid(section, [.. _axis.Rows.Take(count)], loaded, turn) is not null)
                {
                    return count;
                }
            }

            return int.MaxValue;
        }

        /// <summary>The two smallest sections become one, at the place of the earlier: the legend's order stays (rule 3).</summary>
        private PlanWarning Merge(List<Section> sections)
        {
            Section[] ranked = [.. Smallest(sections)];
            var (smallest, next) = (ranked[0], ranked[1]);
            sections.Remove(smallest);
            sections.Remove(next);
            var (earlier, later) = smallest.Place < next.Place ? (smallest, next) : (next, smallest);
            sections.Add(new Section([.. earlier.Destinations, .. later.Destinations], [.. earlier.Crates, .. later.Crates], earlier.Scu + later.Scu, earlier.Place));
            return SectionNotes.Shares(rack, smallest.Destinations[0], next.Destinations[0]);
        }

        // Smallest first; on a tie the later one, so the first destinations keep their sections longest.
        private static IEnumerable<Section> Smallest(IEnumerable<Section> sections) =>
            sections.OrderBy(s => s.Scu).ThenByDescending(s => s.Place);
    }
}

/// <summary>
/// What sections tell the player (D89). What the crates can't say, so carried with the plan like the search's note
/// (<see cref="LoadPlanner.WithHints"/>) until the next Plan; a note goes once a destination it names has left the ship or
/// a crate of it was moved. The words are the only record, so reading them is this class's too.
/// </summary>
public static class SectionNotes
{
    private const string SharesText = " shares a section with ";
    private const string OutsideText = " placed outside sections (capacity)";
    private const string NoFreeText = ": no free section – Re-plan";
    private const string NoSectionsText = "sections don't fit – laid as with None";
    private const string GapText = " has a gap – loaded crates";
    private const string NarrowText = "lanes need a rack at least 4 wide";
    private const string SidewaysText = "no lanes on a rack that stacks sideways";
    private const string LanesRefusedText = "lanes don't fit – laid as without lanes";
    private const string LanesFullText = "lanes full – a mission waits (side overflow off)";
    private const string CrossesText = " crosses sides – side overflow off";
    private const string SidesFullText = "sides full – a mission waits (side overflow off)";

    internal static PlanWarning Shares(Rack rack, string destination, string other) => new(rack.Id, Prefix(rack) + destination + SharesText + other);

    internal static PlanWarning Outside(Rack rack, string destination) => new(rack.Id, Prefix(rack) + destination + OutsideText);

    internal static PlanWarning NoFreeSection(Rack rack, string destination) => new(rack.Id, Prefix(rack) + destination + NoFreeText);

    /// <summary>A section a capture couldn't close around crates that may not move (B17): the gap stays, nothing loaded moves.</summary>
    internal static PlanWarning Gap(Rack rack, string destination) => new(rack.Id, Prefix(rack) + destination + GapText);

    /// <summary>
    /// <paramref name="destination"/> and every destination that shares its section in <paramref name="rack"/>, as the
    /// notes of rule 3 say (B17): pairs of the first destinations of the two sections merged, so the whole chain counts.
    /// </summary>
    internal static IReadOnlySet<string> Sharing(IEnumerable<PlanWarning> warnings, Rack rack, string destination)
    {
        (string, string)[] pairs = [.. warnings.Where(w => w.RackId == rack.Id && w.Message.Contains(SharesText, StringComparison.Ordinal))
            .Select(Names).OfType<IReadOnlyList<string>>().Where(n => n.Count == 2).Select(n => (n[0], n[1]))];
        HashSet<string> section = [destination];
        while (pairs.FirstOrDefault(p => section.Contains(p.Item1) != section.Contains(p.Item2)) is ({ } a, { } b))
        {
            section.Add(a);
            section.Add(b);
        }

        return section;
    }

    /// <summary>A rack too narrow for lanes (D116): fewer than <see cref="Lanes.MinWidth"/> cells across. Derived, not carried.</summary>
    internal static PlanWarning Narrow(Rack rack) => new(rack.Id, Prefix(rack) + NarrowText);

    /// <summary>A rack stacking along ±y (D116): its width stands upright, no side to it. Derived, not carried.</summary>
    internal static PlanWarning Sideways(Rack rack) => new(rack.Id, Prefix(rack) + SidewaysText);

    /// <summary>A rack whose cargo doesn't lie in lanes, laid as without them (D116).</summary>
    internal static PlanWarning LanesRefused(Rack rack) => new(rack.Id, Prefix(rack) + LanesRefusedText);

    /// <summary>A rack where a mission waits because its destination fits neither side whole (D116, no overflow).</summary>
    internal static PlanWarning LanesFull(Rack rack) => new(rack.Id, Prefix(rack) + LanesFullText);

    /// <summary>A rack a mission left because its destination fits neither side of the ship whole (D117, no overflow).</summary>
    internal static PlanWarning SidesFull(Rack rack) => new(rack.Id, Prefix(rack) + SidesFullText);

    /// <summary>A destination across both sides though the plan has no overflow, where it can't wait (D116).</summary>
    internal static PlanWarning Crosses(Rack rack, string destination) => new(rack.Id, Prefix(rack) + destination + CrossesText);

    /// <summary>True for the notes derived from the plan's sides each time (<see cref="LoadPlanner.WithHints"/>), never carried.</summary>
    internal static bool IsDerived(PlanWarning warning) =>
        warning.Message.EndsWith(": " + NarrowText, StringComparison.Ordinal) || warning.Message.EndsWith(": " + SidewaysText, StringComparison.Ordinal);

    /// <summary>A rack that got no sections (D89, Martin: whoever turned them on should read why they aren't there).</summary>
    internal static PlanWarning NoSections(Rack rack) => new(rack.Id, Prefix(rack) + NoSectionsText);

    /// <summary>True if <paramref name="warning"/> is one of these notes, for the overlay's line.</summary>
    public static bool IsNote(PlanWarning warning) => Names(warning) is not null;

    /// <summary>The destinations a note names, none for a rack's own; null if it is none of these notes.</summary>
    internal static IReadOnlyList<string>? Names(PlanWarning warning)
    {
        var start = warning.Message.IndexOf(": ", StringComparison.Ordinal);
        if (start < 0 || !warning.Message.StartsWith("Rack ", StringComparison.Ordinal))
        {
            return null;
        }

        var text = warning.Message[(start + 2)..];
        if (text is NoSectionsText or NarrowText or SidewaysText or LanesRefusedText or LanesFullText or SidesFullText)
        {
            return [];
        }

        if (text.EndsWith(CrossesText, StringComparison.Ordinal))
        {
            return [text[..^CrossesText.Length]];
        }

        if (text.EndsWith(OutsideText, StringComparison.Ordinal))
        {
            return [text[..^OutsideText.Length]];
        }

        if (text.EndsWith(NoFreeText, StringComparison.Ordinal))
        {
            return [text[..^NoFreeText.Length]];
        }

        if (text.EndsWith(GapText, StringComparison.Ordinal))
        {
            return [text[..^GapText.Length]];
        }

        return text.Split(SharesText) is [var destination, var other] ? [destination, other] : null;
    }

    private static string Prefix(Rack rack) => Invariant($"Rack {rack.Number}: ");
}
