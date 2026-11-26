namespace CargoPlan.Core.Routing;

/// <summary>A point of the map in device-independent pixels, y down.</summary>
public readonly record struct MapPoint(double X, double Y);

/// <summary>A size in device-independent pixels.</summary>
public readonly record struct MapSize(double Width, double Height);

/// <summary>A rectangle in device-independent pixels, y down.</summary>
public readonly record struct MapRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    /// <summary>True where the two share area; touching edges don't count.</summary>
    public bool Intersects(MapRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public bool Inside(MapSize view) => X >= 0 && Y >= 0 && Right <= view.Width && Bottom <= view.Height;
}

/// <summary>
/// Where a node's label lies, as an offset from the node, so a zoom moves it with the node and keeps its size (K4).
/// </summary>
/// <param name="Always">Hubs and surfaces: shown at every zoom (E of K3).</param>
/// <param name="Free">It found a place clear of every other label and node at zoom 1.</param>
public sealed record MapLabel(double Dx, double Dy, double Width, double Height, bool Always, bool Free)
{
    public MapRect Box(MapPoint node) => new(node.X + Dx, node.Y + Dy, Width, Height);
}

/// <summary>
/// The map's layout (T13.7, D128), pure and deterministic: every node polar about the star, its angle as in the system's
/// coordinates (y up), its distance to scale, so the farthest node fits the view with room for a label: the radii about
/// the star span only 1:4 (10.6–68 Gm), the 1:40 between a hub and its L1 is the spread's work (D128). Nodes closer than <see cref="ClusterPx"/> join a cluster: its first node in place order (hubs first) stays,
/// the others go round it at <see cref="SpreadPx"/> at their real angle from it, at least <see cref="MinSpreadDegrees"/>
/// apart (fewer when more than six). A label takes the first of eight places round its node, outwards first, clear of the
/// labels set before and of every node; hubs and surfaces go first. Zoom and pan only project (K4), nothing here changes.
/// </summary>
public sealed class MapLayout
{
    /// <summary>Room between the farthest node and the view's edge, for its label.</summary>
    public const double MarginPx = 40;

    /// <summary>Nodes closer than this join one cluster.</summary>
    public const double ClusterPx = 22;

    /// <summary>A cluster's members lie this far from its anchor.</summary>
    public const double SpreadPx = 30;

    /// <summary>The least angle between two members of a cluster, up to six members.</summary>
    public const double MinSpreadDegrees = 60;

    /// <summary>From this zoom on every label shows, also those without room at zoom 1 (E of K3).</summary>
    public const double AllLabelsZoom = 1.5;

    private const double LabelGapPx = 3;

    private const double Octant = Math.PI / 4;

    private MapLayout(MapSize view, MapPoint centre, double pixelsPerGm, IReadOnlyDictionary<string, MapPoint> points, IReadOnlyDictionary<string, MapLabel> labels)
    {
        View = view;
        Centre = centre;
        PixelsPerGm = pixelsPerGm;
        Points = points;
        Labels = labels;
    }

    public MapSize View { get; }

    /// <summary>The star: the view's centre.</summary>
    public MapPoint Centre { get; }

    /// <summary>The scale at zoom 1: pixels per Gm from the star. A new size keeps the zoom and scales the pan by its ratio (gate 7b).</summary>
    public double PixelsPerGm { get; }

    /// <summary>Each node's place at zoom 1, by <see cref="MapNode.Key"/>.</summary>
    public IReadOnlyDictionary<string, MapPoint> Points { get; }

    public IReadOnlyDictionary<string, MapLabel> Labels { get; }

    /// <summary>The radius a node is drawn with: a hub larger, a surface between.</summary>
    public static double Radius(MapNodeKind kind) => kind switch
    {
        MapNodeKind.Hub => 6,
        MapNodeKind.Surface => 5,
        _ => 4,
    };

    /// <summary>A node's disc as a square, for the labels to keep clear of.</summary>
    public static MapRect Disc(MapPoint at, MapNodeKind kind)
    {
        var r = Radius(kind) + 1;
        return new(at.X - r, at.Y - r, 2 * r, 2 * r);
    }

    /// <summary>A point of the layout at <paramref name="zoom"/> about the view's centre, then moved by <paramref name="pan"/>.</summary>
    public MapPoint Project(MapPoint point, double zoom, MapPoint pan) =>
        new(Centre.X + (zoom * (point.X - Centre.X)) + pan.X, Centre.Y + (zoom * (point.Y - Centre.Y)) + pan.Y);

    /// <summary>The nodes whose label shows at <paramref name="zoom"/>: hubs and surfaces always, a station with room or from <see cref="AllLabelsZoom"/>.</summary>
    public IReadOnlyList<string> ShownLabels(double zoom) =>
        [.. Labels.Where(l => l.Value.Always || l.Value.Free || zoom >= AllLabelsZoom).Select(l => l.Key)];

    /// <param name="labelSize">A label's size as drawn; the app measures its font, a test estimates.</param>
    public static MapLayout For(IReadOnlyList<MapNode> nodes, MapSize view, Func<string, MapSize> labelSize)
    {
        var centre = new MapPoint(view.Width / 2, view.Height / 2);
        MapNode[] ordered = [.. nodes.DistinctBy(n => n.Key).OrderBy(n => Priority(n.Kind)).ThenBy(n => n.Key, PlaceNames.Order)];
        var maxRho = ordered.Select(Rho).DefaultIfEmpty(0).Max();
        var scale = maxRho > 0 ? Math.Max(0, (Math.Min(view.Width, view.Height) / 2) - MarginPx) / maxRho : 0;
        var polar = ordered.ToDictionary(n => n.Key, n => Polar(n, centre, scale), StringComparer.Ordinal);
        var (points, anchors) = Spread(ordered, polar);
        var labels = Place(ordered, points, anchors, centre, view, labelSize);
        return new MapLayout(view, centre, scale, points, labels);
    }

    // Hubs, surfaces, the rest: the order in which clusters choose their anchor and labels their place.
    private static int Priority(MapNodeKind kind) => kind switch
    {
        MapNodeKind.Hub => 0,
        MapNodeKind.Surface => 1,
        _ => 2,
    };

    // Distance from the star in the system's plane, Gm: true to scale (no compression, see D128).
    private static double Rho(MapNode node) => Math.Sqrt((node.X * node.X) + (node.Y * node.Y)) / 1e9;

    private static MapPoint Polar(MapNode node, MapPoint centre, double scale)
    {
        var theta = Math.Atan2(node.Y, node.X);
        var rho = Rho(node) * scale;
        return new(centre.X + (rho * Math.Cos(theta)), centre.Y - (rho * Math.Sin(theta)));
    }

    // Single linkage below ClusterPx; the anchor is the cluster's first node in order, the others round it. Also each
    // member's anchor point, for its label.
    private static (Dictionary<string, MapPoint> Points, Dictionary<string, MapPoint> Anchors) Spread(MapNode[] ordered, Dictionary<string, MapPoint> polar)
    {
        var cluster = Enumerable.Range(0, ordered.Length).ToArray();
        int Root(int i) => cluster[i] == i ? i : cluster[i] = Root(cluster[i]);
        for (var i = 0; i < ordered.Length; i++)
        {
            for (var j = i + 1; j < ordered.Length; j++)
            {
                if (Distance(polar[ordered[i].Key], polar[ordered[j].Key]) < ClusterPx)
                {
                    // The smaller index is the root: the anchor is the first node in order.
                    var (a, b) = (Root(i), Root(j));
                    cluster[Math.Max(a, b)] = Math.Min(a, b);
                }
            }
        }

        var points = new Dictionary<string, MapPoint>(polar, StringComparer.Ordinal);
        var anchors = new Dictionary<string, MapPoint>(StringComparer.Ordinal);
        foreach (var group in Enumerable.Range(0, ordered.Length).GroupBy(Root).Where(g => g.Count() > 1))
        {
            var anchor = ordered[group.Key];
            var at = polar[anchor.Key];
            var members = group.Where(i => i != group.Key).Select(i => ordered[i])
                .Select(m => (Node: m, Angle: Math.Atan2(m.Y - anchor.Y, m.X - anchor.X)))
                .OrderBy(m => m.Angle).ThenBy(m => m.Node.Key, PlaceNames.Order).ToList();
            var gap = Math.Min(MinSpreadDegrees, 360.0 / members.Count) * Math.PI / 180;
            var previous = double.NegativeInfinity;
            foreach (var (node, real) in members)
            {
                var angle = Math.Max(real, previous + gap);
                points[node.Key] = new(at.X + (SpreadPx * Math.Cos(angle)), at.Y - (SpreadPx * Math.Sin(angle)));
                anchors[node.Key] = at;
                previous = angle;
            }
        }

        return (points, anchors);
    }

    // Pass 1 at zoom 1: hubs and surfaces first, each label the first free place of its eight. Pass 2 for the labels that
    // found none: their place at AllLabelsZoom, where they show, clear of the labels of pass 1 there; the offset holds.
    private static Dictionary<string, MapLabel> Place(
        MapNode[] ordered, Dictionary<string, MapPoint> points, Dictionary<string, MapPoint> anchors, MapPoint centre, MapSize view, Func<string, MapSize> labelSize)
    {
        var sizes = ordered.Select(n => labelSize(n.Label)).ToArray();
        var offsets = new (double Dx, double Dy)[ordered.Length];
        var free = new bool[ordered.Length];
        List<int> placed = [];
        foreach (var zoom in new[] { 1, AllLabelsZoom })
        {
            MapPoint At(int i) => Zoomed(points[ordered[i].Key], centre, zoom);
            MapRect[] discs = [.. ordered.Select((n, i) => Disc(At(i), n.Kind))];
            List<MapRect> taken = [.. placed.Select(i => new MapRect(At(i).X + offsets[i].Dx, At(i).Y + offsets[i].Dy, sizes[i].Width, sizes[i].Height))];
            for (var i = 0; i < ordered.Length; i++)
            {
                if (free[i] || (zoom > 1 && placed.Contains(i)))
                {
                    continue;
                }

                var node = ordered[i];
                var at = At(i);

                // Outwards: away from the star, or for a cluster member away from its anchor.
                var from = anchors.TryGetValue(node.Key, out var anchor) ? Zoomed(anchor, centre, zoom) : centre;
                var first = (int)Math.Round(Math.Atan2(from.Y - at.Y, at.X - from.X) / Octant);
                var candidates = Enumerable.Range(0, 8)
                    .Select(k => first + ((k + 1) / 2 * (k % 2 == 1 ? 1 : -1)))
                    .Select(octant => Candidate(at, octant * Octant, Radius(node.Kind) + LabelGapPx, sizes[i]))
                    .ToList();
                var index = candidates.FindIndex(c => (zoom > 1 || c.Inside(view)) && !taken.Any(c.Intersects) && !discs.Where((_, j) => j != i).Any(c.Intersects));
                var box = candidates[Math.Max(index, 0)];
                offsets[i] = (box.X - at.X, box.Y - at.Y);
                if (index >= 0)
                {
                    taken.Add(box);
                    placed.Add(i);
                    free[i] = zoom == 1;
                }
            }
        }

        return Enumerable.Range(0, ordered.Length).ToDictionary(
            i => ordered[i].Key,
            i => new MapLabel(offsets[i].Dx, offsets[i].Dy, sizes[i].Width, sizes[i].Height, ordered[i].Kind != MapNodeKind.Station, free[i]),
            StringComparer.Ordinal);
    }

    private static MapPoint Zoomed(MapPoint point, MapPoint centre, double zoom) =>
        new(centre.X + (zoom * (point.X - centre.X)), centre.Y + (zoom * (point.Y - centre.Y)));

    // The label beside its node in the direction of angle (y up): left-aligned to the right, right-aligned to the left,
    // centred above and below.
    private static MapRect Candidate(MapPoint at, double angle, double gap, MapSize size)
    {
        var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
        var (qx, qy) = (at.X + (gap * cos), at.Y - (gap * sin));
        var x = cos > 0.3 ? qx : cos < -0.3 ? qx - size.Width : qx - (size.Width / 2);
        var y = sin > 0.3 ? qy - size.Height : sin < -0.3 ? qy : qy - (size.Height / 2);
        return new(x, y, size.Width, size.Height);
    }

    private static double Distance(MapPoint a, MapPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
