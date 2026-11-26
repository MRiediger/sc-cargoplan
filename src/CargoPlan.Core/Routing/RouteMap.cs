namespace CargoPlan.Core.Routing;

/// <summary>One cell of the port matrix an edge of the map stands for, with its places.</summary>
public sealed record MapEdgePart(string Pickup, string Dropoff, PortCell Cell);

/// <summary>A node's contracts as its tool tip names them: out, in, and the known aUEC out.</summary>
public sealed record MapNodeFigures(int Out, int In, long? OutReward);

/// <summary>
/// One directed edge of the map (T13.7, D128): the port matrix's cell between two stations, or the sum of the cells
/// of a surface's places (K1). Its figures are the cells', added up; nothing is counted here a second way (A1).
/// </summary>
/// <param name="Parts">The cells, in place order; one between two stations.</param>
/// <param name="Gm">Between the nodes' positions (a surface's is its planet's).</param>
/// <param name="TwoWay">The way back is an edge too: both are drawn a little apart.</param>
public sealed record MapEdge(string From, string To, IReadOnlyList<MapEdgePart> Parts, double Gm, bool TwoWay)
{
    public int Count => Parts.Sum(p => p.Cell.Count);

    /// <summary>The known rewards of its cells; null when they know none, never estimated.</summary>
    public long? RewardSum => Parts.Any(p => p.Cell.RewardSum is not null) ? Parts.Sum(p => p.Cell.RewardSum ?? 0) : null;

    public int UnknownRewards => Parts.Sum(p => p.Cell.UnknownRewards);

    public int MaxScu => Parts.Max(p => p.Cell.MaxScu);

    /// <summary>A scan saw one of its templates serve one of its pairs (design §8.3): drawn solid with a mark.</summary>
    public bool Observed => Parts.Any(p => p.Cell.Observed);

    /// <summary>Its colour: the highest rank of its templates.</summary>
    public HaulRank TopRank => Parts.SelectMany(p => p.Cell.Templates).Max(t => t.Template.Rank);

    /// <summary>1 to 3 by <see cref="RewardSum"/> (<see cref="RouteMap.StrongerFrom"/>, <see cref="RouteMap.StrongestFrom"/>); 1 without one.</summary>
    public int Strength => RewardSum switch
    {
        >= RouteMap.StrongestFrom => 3,
        >= RouteMap.StrongerFrom => 2,
        _ => 1,
    };
}

/// <summary>
/// The map's graph (T13.7, D128) of one port matrix: its cells as directed edges between the map's nodes
/// (<see cref="MapNodes"/>), the nodes that have one, the focus, and the lines of the active route and the draft. Pure; the app lays it out
/// (<see cref="MapLayout"/>) and draws it.
/// </summary>
public sealed class RouteMap
{
    /// <summary>
    /// An edge from this many aUEC on is drawn in the second strength: about the lower third of the pin's Stanton stellar
    /// edges (63,500 in ships-2026.11.2, D128).
    /// </summary>
    public const long StrongerFrom = 60_000;

    /// <summary>An edge from this many aUEC on is drawn in the third strength: about the upper third (344,750).</summary>
    public const long StrongestFrom = 350_000;

    private readonly Dictionary<(string, string), MapEdge> _edges;
    private readonly HashSet<(string, string)>? _lit;
    private readonly Dictionary<string, MapNodeFigures> _figures;
    private readonly Dictionary<(string, string), MapExtraEdge> _extras;

    private RouteMap(
        IReadOnlyList<MapNode> nodes, Dictionary<(string, string), MapEdge> edges, string? focus, HashSet<(string, string)>? lit,
        MapPlanLines active, MapPlanLines draft, Dictionary<(string, string), MapExtraEdge> extras, int insideOneNode)
    {
        Nodes = nodes;
        _edges = edges;
        Edges = [.. edges.Values];
        Focus = focus;
        _lit = lit;
        Active = active;
        Draft = draft;
        _extras = extras;
        Extras = [.. extras.Values];
        InsideOneNode = insideOneNode;
        _figures = nodes.ToDictionary(
            n => n.Key,
            n =>
            {
                MapEdge[] outward = [.. Edges.Where(e => e.From == n.Key)];
                long? reward = outward.Any(e => e.RewardSum is not null) ? outward.Sum(e => e.RewardSum ?? 0) : null;
                return new MapNodeFigures(outward.Sum(e => e.Count), Edges.Where(e => e.To == n.Key).Sum(e => e.Count), reward);
            },
            StringComparer.Ordinal);
    }

    /// <summary>The nodes with an edge or a leg of the route, in place order (hubs first).</summary>
    public IReadOnlyList<MapNode> Nodes { get; }

    public IReadOnlyList<MapEdge> Edges { get; }

    /// <summary>The start the player clicked, for the session; null without one or when the filter hides it.</summary>
    public string? Focus { get; }

    /// <summary>
    /// The active route's lines (gate finding 7a/7b): its contracts' edges, drawn in the route's colour, its legs as the
    /// order flown and its stations' numbers; none without a route of this system.
    /// </summary>
    public MapPlanLines Active { get; }

    /// <summary>The custom route being clicked (T13.7b), its lines as <see cref="Active"/>'s; none without one.</summary>
    public MapPlanLines Draft { get; }

    /// <summary>The active route's extras between nodes no contract of the route joins, in place order.</summary>
    public IReadOnlyList<MapExtraEdge> Extras { get; }

    /// <summary>Contracts whose pickup and drop-off fall into one node (one planet's surface): not drawn, counted.</summary>
    public int InsideOneNode { get; }

    public MapEdge? Edge(string from, string to) => _edges.GetValueOrDefault((from, to));

    /// <summary>The extras from <paramref name="from"/> to <paramref name="to"/>; null for none.</summary>
    public MapExtraEdge? Extra(string from, string to) => _extras.GetValueOrDefault((from, to));

    public MapNodeFigures Figures(string node) => _figures[node];

    /// <summary>
    /// With a focus: the edge leaves the focus or a station of the draft, leads back to the focus from a place they go to,
    /// or is a contract's of the draft. Without one, with a draft: the same from the draft's start (T13.7c, D130), and the
    /// active route's contracts and extras. Without either: the edge is a contract's or an extra's of the active route
    /// (gate finding 7a/7b: the rest at 25 %), else every edge is full.
    /// </summary>
    public bool IsLit(MapEdge edge) => _lit is null || _lit.Contains((edge.From, edge.To));

    /// <param name="nodeOf">Every place of the system to its node (<see cref="MapNodes.For"/>); a place without is left out.</param>
    /// <param name="focus">A node's key; ignored when no edge of the filter touches it.</param>
    /// <param name="route">The active route (T13.6); only one of <paramref name="system"/> is drawn.</param>
    /// <param name="draft">The custom route being clicked (T13.7b); with a focus its stations light the edges from them too
    /// (design §8.4.1).</param>
    /// <param name="extras">The active route's extras (<see cref="MapExtra.Of"/>); drawn only with the route.</param>
    public static RouteMap Build(
        PortMatrix matrix, IReadOnlyDictionary<string, MapNode> nodeOf, StarSystem system, string? focus = null, Route? route = null,
        MapPlan? draft = null, IReadOnlyList<MapExtra>? extras = null)
    {
        Dictionary<(string, string), List<MapEdgePart>> parts = [];
        var inside = 0;
        foreach (var pickup in matrix.Pickups)
        {
            foreach (var dropoff in matrix.Dropoffs)
            {
                if (matrix.Cell(pickup, dropoff) is not { } cell || nodeOf.GetValueOrDefault(pickup) is not { } from || nodeOf.GetValueOrDefault(dropoff) is not { } to)
                {
                    continue;
                }

                if (from == to)
                {
                    inside += cell.Count;
                    continue;
                }

                if (!parts.TryGetValue((from.Key, to.Key), out var list))
                {
                    parts[(from.Key, to.Key)] = list = [];
                }

                list.Add(new MapEdgePart(pickup, dropoff, cell));
            }
        }

        var byKey = nodeOf.Values.DistinctBy(n => n.Key).ToDictionary(n => n.Key, StringComparer.Ordinal);
        var edges = parts.ToDictionary(
            p => p.Key,
            p => new MapEdge(
                p.Key.Item1, p.Key.Item2, [.. p.Value.OrderBy(x => x.Pickup, PlaceNames.Order).ThenBy(x => x.Dropoff, PlaceNames.Order)],
                DistanceGm(byKey[p.Key.Item1], byKey[p.Key.Item2]), parts.ContainsKey((p.Key.Item2, p.Key.Item1))));

        var drawn = route is { } r && r.System == system ? r : null;
        var active = drawn is null ? MapPlanLines.None : MapPlanLines.Of(MapPlan.Of(drawn), nodeOf);
        var drafted = draft is null ? MapPlanLines.None : MapPlanLines.Of(draft, nodeOf);
        HashSet<(string, string)> planned = [.. active.Edges];
        var extraEdges = drawn is null ? [] : (extras ?? [])
            .Select(x => (From: nodeOf.GetValueOrDefault(x.Pickup)?.Key, To: nodeOf.GetValueOrDefault(x.Dropoff)?.Key, Extra: x))
            .Where(x => x.From is not null && x.To is not null && x.From != x.To && !planned.Contains((x.From, x.To)))
            .GroupBy(x => (x.From!, x.To!))
            .OrderBy(g => g.Key.Item1, PlaceNames.Order).ThenBy(g => g.Key.Item2, PlaceNames.Order)
            .ToDictionary(g => g.Key, g => new MapExtraEdge(g.Key.Item1, g.Key.Item2, [.. g.Select(x => x.Extra).Distinct()]));

        var shown = edges.Keys.Concat(active.Legs).Concat(active.Edges).Concat(drafted.Legs).Concat(drafted.Edges).Concat(extraEdges.Keys)
            .SelectMany(k => new[] { k.Item1, k.Item2 }).ToHashSet(StringComparer.Ordinal);
        MapNode[] nodes = [.. byKey.Values.Where(v => shown.Contains(v.Key)).OrderBy(v => v.Kind == MapNodeKind.Hub ? 0 : 1).ThenBy(v => v.Key, PlaceNames.Order)];

        var start = focus is not null && edges.Keys.Any(k => k.Item1 == focus || k.Item2 == focus) ? focus : null;
        var draftStart = draft is not null && drafted.Legs.Count > 0 ? nodeOf.GetValueOrDefault(draft.Start)?.Key : null;
        HashSet<(string, string)>? lit = (start, draftStart) switch
        {
            ({ } s, _) => [.. Lit(edges.Keys, s, [.. drafted.Numbers.Keys]), .. drafted.Edges],

            // A draft without a focus acts as one on its start (T13.7c, D130, Martin's R4; replaces condition 3 there).
            (null, { } d) => [.. Lit(edges.Keys, d, [.. drafted.Numbers.Keys]), .. drafted.Edges, .. planned, .. extraEdges.Keys],
            _ => planned.Count + extraEdges.Count > 0 ? [.. planned, .. extraEdges.Keys] : null,
        };
        return new RouteMap(nodes, edges, start, lit, active, drafted, extraEdges, inside);
    }

    // From the start and the draft's stations, and back to the start from where they go (Martin's "Rückkanten").
    private static HashSet<(string, string)> Lit(IEnumerable<(string From, string To)> edges, string start, IReadOnlyCollection<string> along)
    {
        (string From, string To)[] all = [.. edges];
        HashSet<string> from = [start, .. along];
        var reached = all.Where(e => from.Contains(e.From)).Select(e => e.To).ToHashSet(StringComparer.Ordinal);
        return [.. all.Where(e => from.Contains(e.From) || (e.To == start && reached.Contains(e.From)))];
    }

    private static double DistanceGm(MapNode a, MapNode b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2)) / 1e9;
}

/// <summary>The map's lines in pixels (T13.7): where an edge is drawn, and which a pointer hits. Pure, for drawing and hit test alike.</summary>
public static class MapGeometry
{
    /// <summary>A pointer this close to an edge hits it (startprompt 2.6).</summary>
    public const double HitPx = 6;

    /// <summary>A two-way pair's edges lie this far to the right of their way, so both show.</summary>
    public const double TwoWayOffsetPx = 3;

    /// <summary>The edge from <paramref name="from"/> to <paramref name="to"/>, from rim to rim; a two-way one to the right of its way (y down).</summary>
    public static (MapPoint A, MapPoint B) Line(MapPoint from, MapPoint to, double fromRadius, double toRadius, bool twoWay)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= fromRadius + toRadius)
        {
            return (from, to);
        }

        var (ux, uy) = (dx / length, dy / length);
        // The right of the way with y down: (-uy, ux).
        var (ox, oy) = twoWay ? (-uy * TwoWayOffsetPx, ux * TwoWayOffsetPx) : (0, 0);
        return (new(from.X + (ux * fromRadius) + ox, from.Y + (uy * fromRadius) + oy), new(to.X - (ux * toRadius) + ox, to.Y - (uy * toRadius) + oy));
    }

    /// <summary>The index of the segment nearest to <paramref name="point"/> within <paramref name="maxPx"/>; null if none.</summary>
    public static int? Nearest(MapPoint point, IReadOnlyList<(MapPoint A, MapPoint B)> segments, double maxPx)
    {
        int? best = null;
        var bestDistance = maxPx;
        for (var i = 0; i < segments.Count; i++)
        {
            var distance = Distance(point, segments[i].A, segments[i].B);
            if (distance <= bestDistance)
            {
                (best, bestDistance) = (i, distance);
            }
        }

        return best;
    }

    /// <summary>From a point to a segment: to its nearest point, an end beyond the ends.</summary>
    public static double Distance(MapPoint p, MapPoint a, MapPoint b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var squared = (dx * dx) + (dy * dy);
        var t = squared == 0 ? 0 : Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / squared, 0, 1);
        var (x, y) = (a.X + (t * dx), a.Y + (t * dy));
        return Math.Sqrt(Math.Pow(p.X - x, 2) + Math.Pow(p.Y - y, 2));
    }
}
