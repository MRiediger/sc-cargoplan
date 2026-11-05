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
    /// its width is two cells at most on the ships that have it and stands upright (D89).
    /// </summary>
    internal readonly record struct RowAxis(bool AlongLength, IReadOnlyList<int> Rows)
    {
        public static RowAxis Of(Rack rack, FillOrder order, FillRows rows)
        {
            var alongLength = rows == FillRows.Crosswise || rack.StacksSideways;
            var count = alongLength ? rack.Grid.Length : rack.Grid.Width;
            return new RowAxis(alongLength, alongLength && order == FillOrder.BowFirst
                ? [.. Enumerable.Range(0, count).Reverse()]
                : [.. Enumerable.Range(0, count)]);
        }

        public int RowOf(GridPosition cell) => AlongLength ? cell.Length : cell.Width;
    }

    /// <summary>One or, once they share (rule 3), several destinations' crates, by their index in the plan.</summary>
    private sealed record Section(IReadOnlyList<string> Destinations, int[] Crates, int Scu, int Place);

    /// <summary>
    /// The plan with its shared racks laid in sections, and the notes they give. A rack that can't be laid so stays as the
    /// planner laid it. <paramref name="destinations"/> is the legend's order.
    /// </summary>
    public static LoadPlan Lay(LoadPlan plan, Ship ship, IReadOnlyList<string> destinations)
    {
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

            var arranged = new Arrangement(rack, plan, crates, [.. onBoard.Where(i => crates[i].Loaded).Select(i => crates[i])], budget)
                .Run([.. movable.GroupBy(i => crates[i].Destination)
                    .OrderBy(g => legend.IndexOf(g.Key))
                    .Select((g, place) => new Section([g.Key], [.. g], g.Sum(i => (int)crates[i].CrateSize), place))]);

            notes.AddRange(arranged.Notes);
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
        return LoadPlanner.WithHints(plan with { Crates = crates, Warnings = [.. plan.Warnings.Where(w => !sectioned.Contains(w.RackId))] }, ship, notes);
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
    internal static IEnumerable<IReadOnlyList<IReadOnlyList<int>>> RowsFor(Rack rack, IReadOnlyList<Crate> onBoard, string destination, FillOrder order, FillRows rows, int gap)
    {
        var axis = RowAxis.Of(rack, order, rows);
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
            if (from < axis.Rows.Count)
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

    /// <summary>One rack's search for sections: every step of the rules in D89, each try from the rack's first row.</summary>
    private sealed class Arrangement(Rack rack, LoadPlan plan, Crate[] crates, IReadOnlyList<Crate> loaded, CrateLayout.StackingBudget budget)
    {
        private readonly RowAxis _axis = RowAxis.Of(rack, plan.FillOrder, plan.FillRows);
        private readonly HashSet<string> _turned = [];

        public (IReadOnlyList<(int Index, Pose Pose)>? Poses, IReadOnlyList<PlanWarning> Notes, bool GaveUp) Run(List<Section> sections)
        {
            List<PlanWarning> notes = [];
            var gap = plan.SectionGap;

            // D71: a crate lies unturned where it can, so turned only what no row of this rack takes otherwise.
            _turned.UnionWith(sections.Where(s => Laid(s, _axis.Rows, loaded, turn: false) is null).SelectMany(s => s.Destinations));
            while (true)
            {
                if (Attempt(sections, gap) is { } done)
                {
                    return (done.Poses, notes, done.GaveUp);
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
        private (IReadOnlyList<(int Index, Pose Pose)> Poses, bool GaveUp)? Attempt(IReadOnlyList<Section> sections, int gap)
        {
            List<Crate> occupied = [.. loaded];
            List<(int, Pose)> poses = [];
            var gaveUp = false;
            var cursor = 0;
            foreach (var section in sections.OrderBy(s => s.Place))
            {
                if (Smallest(section, cursor, occupied) is not { } found)
                {
                    return null;
                }

                cursor += found.Count + gap;
                gaveUp |= found.Laying.GaveUp;
                poses.AddRange(section.Crates.Zip(found.Laying.Poses));
                occupied.AddRange(section.Crates.Zip(found.Laying.Poses).Select(p => crates[p.First] with { Position = p.Second.Position, Rotated = p.Second.Rotated }));
            }

            return (poses, gaveUp);
        }

        /// <summary>The fewest rows from <paramref name="cursor"/> that take the section: without a crate on other cargo if any count does, else the first that fits.</summary>
        private (int Count, CrateLayout.Laying Laying)? Smallest(Section section, int cursor, IReadOnlyList<Crate> occupied)
        {
            var turn = section.Destinations.Any(_turned.Contains);
            (int, CrateLayout.Laying)? first = null;
            for (var count = 1; cursor + count <= _axis.Rows.Count; count++)
            {
                if (Laid(section, [.. _axis.Rows.Skip(cursor).Take(count)], occupied, turn) is { } laying)
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
                [.. section.Crates.Select(i => (crates[i].CrateSize, crates[i].Destination))], plan.FillOrder, plan.FillRows, budget.For(rack), turn);
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

    internal static PlanWarning Shares(Rack rack, string destination, string other) => new(rack.Id, Prefix(rack) + destination + SharesText + other);

    internal static PlanWarning Outside(Rack rack, string destination) => new(rack.Id, Prefix(rack) + destination + OutsideText);

    internal static PlanWarning NoFreeSection(Rack rack, string destination) => new(rack.Id, Prefix(rack) + destination + NoFreeText);

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
        if (text == NoSectionsText)
        {
            return [];
        }

        if (text.EndsWith(OutsideText, StringComparison.Ordinal))
        {
            return [text[..^OutsideText.Length]];
        }

        if (text.EndsWith(NoFreeText, StringComparison.Ordinal))
        {
            return [text[..^NoFreeText.Length]];
        }

        return text.Split(SharesText) is [var destination, var other] ? [destination, other] : null;
    }

    private static string Prefix(Rack rack) => Invariant($"Rack {rack.Number}: ");
}
