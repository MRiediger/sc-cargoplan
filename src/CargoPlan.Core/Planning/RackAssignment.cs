using CargoPlan.Core.Models;

namespace CargoPlan.Core.Planning;

/// <summary>
/// Which rack each crate of the stops goes into, and where it lies there, without a route (D64): the order of the stops is
/// for display only and changes nothing here. Compared in this order:
/// (1) fewest destinations in more than one rack, then fewest commodity lines in more than one rack;
/// (2) fewest destinations in the fullest rack, then fewest racks holding more than one, then the smallest sum of squared
/// destinations per rack;
/// (3) of the assignments equal in (1) and (2), the first whose shared racks lay without a crate on another destination's
/// cargo (<see cref="CrateLayout.Lay"/>), else the one with the fewest racks that don't.
/// "First" is the search order, which is the tie-break: destinations by SCU descending, then name (ordinal); lines of a
/// destination the same way by commodity; each into an empty rack before an occupied one, racks in rack order. Racks of
/// one grid and limit are alike: of those empty, only the first is tried. Every rack's crates must lie in its grid
/// (largest first, as <see cref="CrateLayout.Place"/> lays them).
/// </summary>
internal static class RackAssignment
{
    /// <summary>
    /// Nodes of the search before it settles for the best assignment found so far (D64). A count, not a time, so a plan
    /// comes out the same on every machine.
    /// </summary>
    internal const int MaxSearchNodes = 1_000_000;

    /// <summary>One crate: stop and item index into the stops as passed, and its size.</summary>
    internal readonly record struct Piece(int Stop, int Item, CrateSize Size);

    /// <summary>The crates of one rack with their poses; <paramref name="StackingUnchecked"/> if the search for a layout without foreign stacking gave up.</summary>
    /// <param name="LanesRefused">The rack has lanes (D116), but its crates don't lie in them: laid as without, with a note.</param>
    internal sealed record RackLoad(Rack Rack, IReadOnlyList<(Piece Piece, Pose Pose)> Crates, bool StackingUnchecked, bool LanesRefused = false);

    /// <summary>The racks with cargo, in rack order, and the nodes the search took.</summary>
    internal sealed record Result(IReadOnlyList<RackLoad> Racks, long Nodes);

    /// <summary>The assignment, or null if no assignment lays out (or none was found within <see cref="MaxSearchNodes"/>).</summary>
    /// <param name="racks">In rack order.</param>
    /// <param name="sides">
    /// Lanes (D116) for the layout of the assignment found, not for the search: racks and who waits are as without (F5).
    /// </param>
    internal static Result? Assign(Rack[] racks, IReadOnlyList<Stop> stops, FillOrder order, FillRows rows, Sides sides = Sides.None) =>
        new Search(racks, stops, order, rows, sides, MaxSearchNodes, new CrateLayout.StackingBudget()).Run();

    /// <summary>
    /// Nodes one probe of <see cref="AssignBySide"/> may take: whether a side still takes a destination (E1). Out of nodes
    /// counts as "doesn't take it", so the destination goes on to the next side.
    /// </summary>
    internal const int MaxProbeNodes = 20_000;

    /// <summary>The assignment by side, or null to fall back; the nodes of each group's search and of the probes, for the measurements.</summary>
    internal sealed record SideResult(Result? Assignment, IReadOnlyList<long> GroupNodes, long ProbeNodes, int Probes);

    /// <summary>
    /// The assignment side by side (D117): the destinations, in the order of <paramref name="legend"/> (indices into
    /// <paramref name="stops"/>, P2), go to the groups of <see cref="SideGroups.Of"/>, then each group is assigned as
    /// <see cref="Assign"/> does on its racks alone, so the priorities hold within a side. With
    /// <paramref name="overflow"/> a destination fills what the first group still takes and goes on with the rest; without,
    /// it goes whole into the first group that takes it, and one that fits none comes last, as with overflow. Whether a
    /// group takes crates is a search for any assignment (<see cref="MaxProbeNodes"/> each). All searches share
    /// <see cref="MaxSearchNodes"/> and one layout budget, as one <see cref="Assign"/> does (E1). Null if crates find no
    /// group or a group no assignment.
    /// </summary>
    internal static SideResult AssignBySide(
        IReadOnlyList<Rack[]> groups, IReadOnlyList<Stop> stops, IReadOnlyList<int> legend, FillOrder order, FillRows rows, Sides sides, bool overflow) =>
        new BySide(groups, stops, order, rows, sides).Run(legend, overflow);

    private sealed record Group(CrateSize Size, int Count, (int Item, int Count)[] Items);

    private sealed record Line(int Global, string Commodity, int Scu, Group[] Groups);

    private sealed record Dest(int Stop, string Name, int Scu, Line[] Lines, CrateSize[] Sizes);

    /// <summary><see cref="Count"/> crates of group <see cref="Group"/> of line <see cref="Line"/> of destination <see cref="Dest"/>.</summary>
    private readonly record struct Entry(int Dest, int Line, int Group, int Count);

    private enum Mode
    {
        /// <summary>Stop at the first assignment within the caps.</summary>
        Feasible,

        /// <summary>Find the best (shared racks, squares) within the caps.</summary>
        Optimize,

        /// <summary>Visit the assignments equal to the best, in search order, and weigh their stacking.</summary>
        Ties,
    }

    private sealed class Search
    {
        private readonly Rack[] _racks;
        private readonly IReadOnlyList<Stop> _stops;
        private readonly FillOrder _order;
        private readonly FillRows _rows;
        private readonly Dest[] _dests;
        private readonly int[] _scuFrom;
        private readonly int[][] _twinsBefore;
        private readonly bool[,] _takes;

        private readonly int[] _residual;
        private readonly int[] _count;
        private readonly int[,] _destScu;
        private readonly int[] _destRacks;
        private readonly int[,] _lineScu;
        private readonly int[] _lineRacks;
        private readonly List<Entry>[] _content;
        private int _splits;
        private int _cuts;
        private int _shared;
        private int _squares;
        private int _pairs;

        private int _splitCap;
        private int _cutCap;
        private int _maxCap;
        private Mode _mode;
        private (int Shared, int Squares) _best = (int.MaxValue, int.MaxValue);
        private Entry[][]? _bestLeaf;
        private Entry[][]? _chosen;
        private int _chosenForeign = int.MaxValue;
        private long _nodes;
        private bool _exhausted;

        private readonly CrateLayout.StackingBudget _budget;
        private readonly Dictionary<string, bool> _fits = [];
        private readonly Dictionary<string, CrateLayout.Laying> _layings = [];

        private readonly Sides _sides;
        private readonly long _maxNodes;

        public Search(Rack[] racks, IReadOnlyList<Stop> stops, FillOrder order, FillRows rows, Sides sides, long maxNodes, CrateLayout.StackingBudget budget)
        {
            (_racks, _stops, _order, _rows, _sides, _maxNodes, _budget) = (racks, stops, order, rows, sides, maxNodes, budget);
            var global = 0;
            _dests =
            [
                .. stops.Select((stop, index) => (stop, index))
                    .Where(p => p.stop.Items.Count > 0)
                    .OrderByDescending(p => p.stop.TotalScu).ThenBy(p => p.stop.Destination, StringComparer.Ordinal)
                    .Select(p => ToDest(p.stop, p.index, ref global)),
            ];
            _scuFrom = new int[_dests.Length + 1];
            for (var d = _dests.Length - 1; d >= 0; d--)
            {
                _scuFrom[d] = _scuFrom[d + 1] + _dests[d].Scu;
            }

            _twinsBefore =
            [
                .. racks.Select((rack, r) => racks.Take(r)
                    .Select((other, o) => (other, o))
                    .Where(p => p.other.Grid == rack.Grid && p.other.MaxCrateSize == rack.MaxCrateSize && p.other.MinCrateSize == rack.MinCrateSize && p.other.CapacityScu == rack.CapacityScu)
                    // Crosswise a rack stacking along y keeps its rows (D88), so it isn't alike one that doesn't.
                    .Where(p => rows == FillRows.Lengthwise || p.other.StacksSideways == rack.StacksSideways)
                    .Select(p => p.o)
                    .ToArray()),
            ];
            _takes = new bool[_dests.Length, racks.Length];
            for (var d = 0; d < _dests.Length; d++)
            {
                for (var r = 0; r < racks.Length; r++)
                {
                    _takes[d, r] = _dests[d].Sizes.All(racks[r].CanHold);
                }
            }

            _residual = [.. racks.Select(r => r.CapacityScu)];
            _count = new int[racks.Length];
            _destScu = new int[_dests.Length, racks.Length];
            _destRacks = new int[_dests.Length];
            _lineScu = new int[global, racks.Length];
            _lineRacks = new int[global];
            _content = [.. racks.Select(_ => new List<Entry>())];
        }

        /// <summary>A stop's crates as lines per commodity, largest line first, each line's crates by size, largest first.</summary>
        private static Dest ToDest(Stop stop, int index, ref int global)
        {
            var lines = stop.Items.Select((item, i) => (item, i))
                .GroupBy(p => p.item.Commodity, StringComparer.Ordinal)
                .Select(g => (Commodity: g.Key, Scu: g.Sum(p => p.item.Scu), Groups: g
                    .GroupBy(p => p.item.CrateSize)
                    .OrderByDescending(s => s.Key)
                    .Select(s => new Group(s.Key, s.Sum(p => p.item.Scu / (int)s.Key), [.. s.Select(p => (p.i, p.item.Scu / (int)s.Key))]))
                    .ToArray()))
                .OrderByDescending(l => l.Scu).ThenBy(l => l.Commodity, StringComparer.Ordinal)
                .ToArray();
            var first = global;
            global += lines.Length;
            return new Dest(
                index, stop.Destination, stop.TotalScu,
                [.. lines.Select((l, n) => new Line(first + n, l.Commodity, l.Scu, l.Groups))],
                [.. stop.Items.Select(i => i.CrateSize).Distinct()]);
        }

        public Result? Run()
        {
            if (_dests.Length == 0)
            {
                return new Result([], 0);
            }

            // Lexicographic: each criterion as a hard cap, raised from its lower bound until an assignment exists.
            (_cutCap, _maxCap) = (int.MaxValue, _dests.Length);
            if (!Lowest(ref _splitCap, Enumerable.Range(0, _dests.Length).Count(d => !Enumerable.Range(0, _racks.Length).Any(r => Takes(d, r))), _dests.Length)
                || !Lowest(ref _cutCap, ForcedCuts(0), _lineRacks.Length)
                || !Lowest(ref _maxCap, (_dests.Length + _racks.Length - 1) / _racks.Length, _dests.Length))
            {
                return _bestLeaf is null ? null : Built(_bestLeaf);
            }

            _mode = Mode.Optimize;
            Next(0);
            if (!_exhausted)
            {
                _mode = Mode.Ties;
                Next(0);
            }

            // Out of nodes: the best found so far.
            var chosen = _chosen ?? _bestLeaf;
            return chosen is null ? null : Built(chosen);
        }

        /// <summary>The nodes taken so far.</summary>
        public long Nodes => _nodes;

        /// <summary>True if any assignment lays out, every cap open (a probe of <see cref="AssignBySide"/>); false if none does or the nodes run out first.</summary>
        public bool Feasible()
        {
            if (_dests.Length == 0)
            {
                return true;
            }

            (_splitCap, _cutCap, _maxCap, _mode) = (_dests.Length, int.MaxValue, _dests.Length, Mode.Feasible);
            return Next(0) && !_exhausted;
        }

        /// <summary>Raises <paramref name="cap"/> from <paramref name="from"/> until an assignment within the caps exists; false if none does up to <paramref name="to"/>.</summary>
        private bool Lowest(ref int cap, int from, int to)
        {
            _mode = Mode.Feasible;
            for (cap = from; cap <= to; cap++)
            {
                if (Next(0) && !_exhausted)
                {
                    return true;
                }

                if (_exhausted)
                {
                    return false;
                }
            }

            return false;
        }

        // --- The search ---

        /// <summary>Places destination <paramref name="d"/> and the rest; true stops the search.</summary>
        private bool Next(int d)
        {
            if (Tick())
            {
                return true;
            }

            if (d == _dests.Length)
            {
                return Leaf();
            }

            if (Pruned(d))
            {
                return false;
            }

            foreach (var r in Candidates(d, allEmpty: false))
            {
                if (_residual[r] < _dests[d].Scu || !Takes(d, r))
                {
                    continue;
                }

                PutDest(d, r);
                var stop = Valid(r) && Next(d + 1);
                TakeDest(d, r);
                if (stop)
                {
                    return true;
                }
            }

            return _splits < _splitCap && SplitLine(d, 0);
        }

        /// <summary>Places line <paramref name="l"/> of a destination that spans racks, whole or (within the cap) cut.</summary>
        private bool SplitLine(int d, int l)
        {
            var dest = _dests[d];
            if (l == dest.Lines.Length)
            {
                return _destRacks[d] >= 2 && Next(d + 1);
            }

            if (Tick())
            {
                return true;
            }

            var line = dest.Lines[l];
            foreach (var r in Candidates(d, allEmpty: false))
            {
                if (_residual[r] < line.Scu || !line.Groups.All(g => _racks[r].CanHold(g.Size)))
                {
                    continue;
                }

                PutLine(d, l, r);
                var stop = Valid(r) && SplitLine(d, l + 1);
                TakeLine(d, l, r);
                if (stop)
                {
                    return true;
                }
            }

            return _cuts < _cutCap && CutGroup(d, l, 0);
        }

        /// <summary>Spreads group <paramref name="g"/> of a cut line over the racks, then the next group.</summary>
        private bool CutGroup(int d, int l, int g)
        {
            var line = _dests[d].Lines[l];
            if (g == line.Groups.Length)
            {
                return _lineRacks[line.Global] >= 2 && SplitLine(d, l + 1);
            }

            var size = line.Groups[g].Size;
            return Spread(d, l, g, [.. Candidates(d, allEmpty: true).Where(r => _racks[r].CanHold(size))], 0, line.Groups[g].Count);
        }

        /// <summary>
        /// Puts as many of the <paramref name="left"/> crates as fit into the candidate at <paramref name="at"/>, then fewer,
        /// down to none, and the rest into the following candidates. An empty rack is skipped while an alike one before it is
        /// empty: the two are interchangeable.
        /// </summary>
        private bool Spread(int d, int l, int g, int[] candidates, int at, int left)
        {
            if (left == 0)
            {
                return CutGroup(d, l, g + 1);
            }

            if (at == candidates.Length || Tick())
            {
                return _exhausted;
            }

            var r = candidates[at];
            if (!(Empty(r) && !FirstOfKind(r)))
            {
                for (var n = Math.Min(left, _residual[r] / (int)_dests[d].Lines[l].Groups[g].Size); n > 0; n--)
                {
                    Put(d, l, g, r, n);
                    var stop = Valid(r) && Spread(d, l, g, candidates, at + 1, left - n);
                    Take(d, l, g, r, n);
                    if (stop)
                    {
                        return true;
                    }
                }
            }

            return Spread(d, l, g, candidates, at + 1, left);
        }

        /// <summary>
        /// Racks to try for destination <paramref name="d"/>, in search order: those holding it, then empty ones (the first
        /// of each kind, or all of them for <see cref="Spread"/>, which skips alike ones itself), then the occupied rest.
        /// </summary>
        private List<int> Candidates(int d, bool allEmpty)
        {
            List<int> candidates = new(_racks.Length);
            for (var r = 0; r < _racks.Length; r++)
            {
                if (_destScu[d, r] > 0)
                {
                    candidates.Add(r);
                }
            }

            for (var r = 0; r < _racks.Length; r++)
            {
                if (Empty(r) && (allEmpty || FirstOfKind(r)))
                {
                    candidates.Add(r);
                }
            }

            for (var r = 0; r < _racks.Length; r++)
            {
                if (!Empty(r) && _destScu[d, r] == 0)
                {
                    candidates.Add(r);
                }
            }

            return candidates;
        }

        private bool Empty(int r) => _residual[r] == _racks[r].CapacityScu;

        private bool FirstOfKind(int r) => _twinsBefore[r].All(t => !Empty(t));

        private bool Takes(int d, int r) => _takes[d, r] && _racks[r].CapacityScu >= _dests[d].Scu;

        private bool Valid(int r) => _count[r] <= _maxCap && _splits <= _splitCap && _cuts <= _cutCap;

        private bool Tick()
        {
            if (++_nodes > _maxNodes)
            {
                _exhausted = true;
            }

            return _exhausted;
        }

        /// <summary>True if no assignment of the destinations from <paramref name="d"/> on can stay within the caps, or (optimizing) beat the best.</summary>
        private bool Pruned(int d)
        {
            if (_scuFrom[d] > _residual.Sum())
            {
                return true;
            }

            var forcedSplits = 0;
            for (var u = d; u < _dests.Length; u++)
            {
                if (!Enumerable.Range(0, _racks.Length).Any(r => _residual[r] >= _dests[u].Scu && _takes[u, r]))
                {
                    forcedSplits++;
                }
            }

            var left = _dests.Length - d;
            if (_splits + forcedSplits > _splitCap
                || _cuts + ForcedCuts(d) > _cutCap
                || Math.Max(_count.Max(), (_pairs + left + _racks.Length - 1) / _racks.Length) > _maxCap)
            {
                return true;
            }

            if (_mode == Mode.Feasible)
            {
                return false;
            }

            (int, int) bound = (SharedAtLeast(left), SquaresAtLeast(left));
            return _mode == Mode.Optimize ? bound.CompareTo(_best) >= 0 : bound.CompareTo(_best) > 0;
        }

        /// <summary>Lines of the destinations from <paramref name="d"/> on that no rack takes whole any more.</summary>
        private int ForcedCuts(int d)
        {
            var forced = 0;
            for (var u = d; u < _dests.Length; u++)
            {
                foreach (var line in _dests[u].Lines)
                {
                    if (!Enumerable.Range(0, _racks.Length).Any(r => _residual[r] >= line.Scu && line.Groups.All(g => _racks[r].CanHold(g.Size))))
                    {
                        forced++;
                    }
                }
            }

            return forced;
        }

        /// <summary>
        /// Shared racks at least, with <paramref name="left"/> destinations to come: one goes into each empty rack, the rest
        /// fill shared racks up to the cap, then each further shared rack takes cap − 1 more.
        /// </summary>
        private int SharedAtLeast(int left)
        {
            var rest = left - _residual.Where((_, r) => Empty(r)).Count() - _count.Where(c => c >= 2).Sum(c => _maxCap - c);
            if (rest <= 0)
            {
                return _shared;
            }

            return _maxCap <= 1 ? int.MaxValue : _shared + ((rest + _maxCap - 2) / (_maxCap - 1));
        }

        /// <summary>The sum of squares at least, with <paramref name="left"/> destinations to come, each onto the rack with the fewest.</summary>
        private int SquaresAtLeast(int left)
        {
            int[] counts = [.. _count];
            for (var i = 0; i < left; i++)
            {
                counts[Array.IndexOf(counts, counts.Min())]++;
            }

            return counts.Sum(c => c * c);
        }

        private bool Leaf()
        {
            if (!Enumerable.Range(0, _racks.Length).All(Fits))
            {
                return false;
            }

            switch (_mode)
            {
                case Mode.Feasible:
                    _bestLeaf = Snapshot();
                    return true;
                case Mode.Optimize:
                    if ((_shared, _squares).CompareTo(_best) < 0)
                    {
                        (_best, _bestLeaf) = ((_shared, _squares), Snapshot());
                    }

                    return false;
                default:
                    if ((_shared, _squares) != _best)
                    {
                        return false;
                    }

                    var leaf = Snapshot();
                    var foreign = Enumerable.Range(0, _racks.Length).Count(r => leaf[r].Select(e => e.Dest).Distinct().Count() > 1 && !LayingOf(r, leaf[r]).StackingFree);
                    if (foreign < _chosenForeign)
                    {
                        (_chosen, _chosenForeign) = (leaf, foreign);
                    }

                    return foreign == 0;
            }
        }

        private Entry[][] Snapshot() => [.. _content.Select(c => c.ToArray())];

        /// <summary>True if rack <paramref name="r"/>'s crates lie in its grid largest first, as <see cref="CrateLayout.Place"/> lays them.</summary>
        private bool Fits(int r)
        {
            if (_content[r].Count == 0)
            {
                return true;
            }

            CrateSize[] sizes = [.. _content[r].SelectMany(e => Enumerable.Repeat(_dests[e.Dest].Lines[e.Line].Groups[e.Group].Size, e.Count)).OrderDescending()];
            var key = $"{_twinsBefore[r].DefaultIfEmpty(r).Min()}:{string.Join(",", sizes.Select(s => (int)s))}";
            if (!_fits.TryGetValue(key, out var fits))
            {
                var space = CrateLayout.RackSpace.Around(_racks[r], [], _order, _rows);
                fits = sizes.All(s => space.Add(s) is not null);
                _fits[key] = fits;
            }

            return fits;
        }

        // --- Layout of the chosen assignment ---

        /// <summary>A rack's crates in the planner's order: largest first, then destination, then line, as the search ranks them.</summary>
        private (Entry Entry, CrateSize Size)[] Ordered(Entry[] entries) =>
        [
            .. entries.SelectMany(e => Enumerable.Repeat((Entry: e, _dests[e.Dest].Lines[e.Line].Groups[e.Group].Size), e.Count))
                .OrderByDescending(p => p.Size).ThenBy(p => p.Entry.Dest).ThenBy(p => p.Entry.Line),
        ];

        /// <summary>The layout of a rack's crates (<see cref="CrateLayout.Lay"/>), searched once per content, from the plan's one budget.</summary>
        private CrateLayout.Laying LayingOf(int r, Entry[] entries)
        {
            var ordered = Ordered(entries);
            var key = $"{_twinsBefore[r].DefaultIfEmpty(r).Min()}:{string.Join(",", ordered.Select(p => $"{p.Entry.Dest}/{(int)p.Size}"))}";
            if (!_layings.TryGetValue(key, out var laying))
            {
                // The search checked that the crates fit, and Lay lays them the same way first.
                laying = CrateLayout.Lay(_racks[r], [], [.. ordered.Select(p => (p.Size, _dests[p.Entry.Dest].Name))], _order, _rows, _budget.For(_racks[r]))!;
                _budget.Spend(_racks[r], laying);
                _layings[key] = laying;
            }

            return laying;
        }

        private Result Built(Entry[][] leaf)
        {
            // Which item a crate comes from: the items of a group in their order, across racks in rack order.
            Dictionary<(int Dest, int Line, int Group), (int Item, int Used)> cursor = [];
            List<RackLoad> loads = [];
            for (var r = 0; r < _racks.Length; r++)
            {
                if (leaf[r].Length == 0)
                {
                    continue;
                }

                var laying = LayingOf(r, leaf[r]);

                // The search chose without lanes (F5); the chosen racks' crates lie in them (D116), or as chosen with a note.
                var refused = false;
                if (Lanes.Of(_racks[r], _sides) is { } lanes)
                {
                    if (CrateLayout.Lay(_racks[r], [], [.. Ordered(leaf[r]).Select(p => (p.Size, _dests[p.Entry.Dest].Name))], _order, _rows, _budget.For(_racks[r]), lanes: lanes) is { } laned)
                    {
                        _budget.Spend(_racks[r], laned);
                        laying = laned;
                    }
                    else
                    {
                        refused = true;
                    }
                }

                var pieces = Ordered(leaf[r]).Select(p =>
                {
                    var (dest, line, group) = (p.Entry.Dest, p.Entry.Line, p.Entry.Group);
                    var items = _dests[dest].Lines[line].Groups[group].Items;
                    var (item, used) = cursor.GetValueOrDefault((dest, line, group));
                    cursor[(dest, line, group)] = used + 1 == items[item].Count ? (item + 1, 0) : (item, used + 1);
                    return new Piece(_dests[dest].Stop, items[item].Item, p.Size);
                });
                loads.Add(new RackLoad(_racks[r], [.. pieces.Zip(laying.Poses)], laying.GaveUp, refused));
            }

            return new Result(loads, _nodes);
        }

        // --- State ---

        private void PutDest(int d, int r)
        {
            for (var l = 0; l < _dests[d].Lines.Length; l++)
            {
                PutLine(d, l, r);
            }
        }

        private void TakeDest(int d, int r)
        {
            for (var l = _dests[d].Lines.Length - 1; l >= 0; l--)
            {
                TakeLine(d, l, r);
            }
        }

        private void PutLine(int d, int l, int r)
        {
            var groups = _dests[d].Lines[l].Groups;
            for (var g = 0; g < groups.Length; g++)
            {
                Put(d, l, g, r, groups[g].Count);
            }
        }

        private void TakeLine(int d, int l, int r)
        {
            var groups = _dests[d].Lines[l].Groups;
            for (var g = groups.Length - 1; g >= 0; g--)
            {
                Take(d, l, g, r, groups[g].Count);
            }
        }

        private void Put(int d, int l, int g, int r, int n)
        {
            var line = _dests[d].Lines[l];
            var scu = n * (int)line.Groups[g].Size;
            _residual[r] -= scu;
            if (_destScu[d, r] == 0)
            {
                _count[r]++;
                _pairs++;
                _squares += (2 * _count[r]) - 1;
                _shared += _count[r] == 2 ? 1 : 0;
                _splits += ++_destRacks[d] == 2 ? 1 : 0;
            }

            _destScu[d, r] += scu;
            if (_lineScu[line.Global, r] == 0)
            {
                _cuts += ++_lineRacks[line.Global] == 2 ? 1 : 0;
            }

            _lineScu[line.Global, r] += scu;
            _content[r].Add(new Entry(d, l, g, n));
        }

        /// <summary>Undoes the last <see cref="Put"/> with the same arguments.</summary>
        private void Take(int d, int l, int g, int r, int n)
        {
            var line = _dests[d].Lines[l];
            var scu = n * (int)line.Groups[g].Size;
            _content[r].RemoveAt(_content[r].Count - 1);
            _lineScu[line.Global, r] -= scu;
            if (_lineScu[line.Global, r] == 0)
            {
                _cuts -= _lineRacks[line.Global]-- == 2 ? 1 : 0;
            }

            _destScu[d, r] -= scu;
            if (_destScu[d, r] == 0)
            {
                _splits -= _destRacks[d]-- == 2 ? 1 : 0;
                _shared -= _count[r] == 2 ? 1 : 0;
                _squares -= (2 * _count[r]) - 1;
                _pairs--;
                _count[r]--;
            }

            _residual[r] += scu;
        }
    }

    /// <summary>The destinations shared out to the side groups, then each group assigned on its own (<see cref="AssignBySide"/>).</summary>
    private sealed class BySide(IReadOnlyList<Rack[]> groups, IReadOnlyList<Stop> stops, FillOrder order, FillRows rows, Sides sides)
    {
        /// <summary>Crates per item of a stop still to go.</summary>
        private readonly int[][] _left = [.. stops.Select(s => s.Items.Select(i => i.Scu / (int)i.CrateSize).ToArray())];

        /// <summary>Per group the parts of stops in it: the stop and its crates per item.</summary>
        private readonly List<(int Stop, int[] Counts)>[] _parts = [.. groups.Select(_ => new List<(int, int[])>())];

        private readonly int[] _used = new int[groups.Count];
        private long _nodes = MaxSearchNodes;
        private long _probeNodes;
        private int _probes;

        public SideResult Run(IReadOnlyList<int> legend, bool overflow)
        {
            List<int> crossing = [];
            foreach (var s in legend)
            {
                if (overflow && !Spread(s))
                {
                    return Failed([]);
                }

                if (!overflow && !Whole(s))
                {
                    crossing.Add(s);
                }
            }

            if (crossing.Any(s => !Spread(s)))
            {
                return Failed([]);
            }

            var budget = new CrateLayout.StackingBudget();
            List<RackLoad> loads = [];
            List<long> groupNodes = [];
            for (var g = 0; g < groups.Count; g++)
            {
                if (_parts[g].Count == 0)
                {
                    groupNodes.Add(0);
                    continue;
                }

                // The nodes left, shared among the groups still to search; what one leaves goes on to the next (E1).
                var share = _nodes / Enumerable.Range(g, groups.Count - g).Count(h => _parts[h].Count > 0);
                var (local, map) = Local(_parts[g]);
                var search = new Search(groups[g], local, order, rows, sides, share, budget);
                var result = search.Run();
                _nodes -= search.Nodes;
                groupNodes.Add(search.Nodes);
                if (result is null)
                {
                    return Failed(groupNodes);
                }

                loads.AddRange(result.Racks.Select(load => load with
                {
                    Crates = [.. load.Crates.Select(c => (new Piece(map[c.Piece.Stop].Stop, map[c.Piece.Stop].Items[c.Piece.Item], c.Piece.Size), c.Pose))],
                }));
            }

            return new SideResult(new Result([.. loads.OrderBy(l => l.Rack.Index)], MaxSearchNodes - _nodes), groupNodes, _probeNodes, _probes);
        }

        private SideResult Failed(IReadOnlyList<long> groupNodes) => new(null, groupNodes, _probeNodes, _probes);

        /// <summary>Stop <paramref name="s"/> whole into the first group that takes it; false if none does.</summary>
        private bool Whole(int s)
        {
            for (var g = 0; g < groups.Count; g++)
            {
                if (Room(g) >= stops[s].TotalScu && Crates(s).All(c => groups[g].Any(r => r.CanHold(c.Size))) && Lays(g, s, _left[s]))
                {
                    Add(g, s, _left[s]);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stop <paramref name="s"/> over the groups in order, each taking as many of its crates as it can: lines as the
        /// planner ranks them, largest crates first, while the SCU fit; then the last ones out until the group lays out.
        /// False if crates are left.
        /// </summary>
        private bool Spread(int s)
        {
            for (var g = 0; g < groups.Count && _left[s].Any(n => n > 0); g++)
            {
                var free = Room(g);
                List<(int Item, CrateSize Size)> chosen = [];
                foreach (var crate in Crates(s))
                {
                    if ((int)crate.Size <= free && groups[g].Any(r => r.CanHold(crate.Size)))
                    {
                        chosen.Add(crate);
                        free -= (int)crate.Size;
                    }
                }

                while (chosen.Count > 0 && !Lays(g, s, CountsOf(s, chosen)))
                {
                    chosen.RemoveAt(chosen.Count - 1);
                }

                if (chosen.Count > 0)
                {
                    Add(g, s, CountsOf(s, chosen));
                }
            }

            return _left[s].All(n => n == 0);
        }

        private int Room(int g) => groups[g].Sum(r => r.CapacityScu) - _used[g];

        /// <summary>The crates of stop <paramref name="s"/> still to go, as the planner ranks them: lines by SCU, then commodity; largest first.</summary>
        private IEnumerable<(int Item, CrateSize Size)> Crates(int s) =>
            stops[s].Items.Select((item, i) => (item, i))
                .GroupBy(p => p.item.Commodity, StringComparer.Ordinal)
                .OrderByDescending(l => l.Sum(p => _left[s][p.i] * (int)p.item.CrateSize)).ThenBy(l => l.Key, StringComparer.Ordinal)
                .SelectMany(l => l.OrderByDescending(p => p.item.CrateSize).ThenBy(p => p.i).SelectMany(p => Enumerable.Repeat((p.i, p.item.CrateSize), _left[s][p.i])));

        private int[] CountsOf(int s, List<(int Item, CrateSize Size)> crates)
        {
            var counts = new int[stops[s].Items.Count];
            foreach (var (item, _) in crates)
            {
                counts[item]++;
            }

            return counts;
        }

        private void Add(int g, int s, int[] counts)
        {
            _parts[g].Add((s, [.. counts]));
            for (var i = 0; i < counts.Length; i++)
            {
                _used[g] += counts[i] * (int)stops[s].Items[i].CrateSize;
                _left[s][i] -= counts[i];
            }
        }

        /// <summary>True if group <paramref name="g"/> lays out with its parts and these crates of stop <paramref name="s"/>.</summary>
        private bool Lays(int g, int s, int[] counts)
        {
            if (_nodes <= 0)
            {
                return false;
            }

            var (local, _) = Local([.. _parts[g], (s, counts)]);
            var search = new Search(groups[g], local, order, rows, Sides.None, Math.Min(MaxProbeNodes, _nodes), new CrateLayout.StackingBudget());
            var lays = search.Feasible();
            _nodes -= search.Nodes;
            _probeNodes += search.Nodes;
            _probes++;
            return lays;
        }

        /// <summary>The parts as stops of their own, and per stop which stop and items of the plan it holds.</summary>
        private (Stop[] Stops, (int Stop, int[] Items)[] Map) Local(IReadOnlyList<(int Stop, int[] Counts)> parts)
        {
            var local = new Stop[parts.Count];
            var map = new (int, int[])[parts.Count];
            for (var p = 0; p < parts.Count; p++)
            {
                var (s, counts) = parts[p];
                int[] items = [.. Enumerable.Range(0, counts.Length).Where(i => counts[i] > 0)];
                var stop = stops[s];
                local[p] = new Stop(stop.Destination, stop.Order, [.. items.Select(i => new CargoItem(stop.Items[i].Commodity, counts[i] * (int)stop.Items[i].CrateSize, stop.Items[i].CrateSize))]);
                map[p] = (s, items);
            }

            return (local, map);
        }
    }
}
