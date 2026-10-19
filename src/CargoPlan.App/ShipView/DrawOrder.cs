using System.Diagnostics;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Painter's algorithm for disjoint axis-aligned boxes: boxes further back are drawn first, so the ones in front cover
/// them. Two disjoint boxes always have a separating axis; the one beyond it, seen from the camera, goes first. Those
/// pairs, for boxes that overlap on screen, are sorted topologically (D15, D21). Racks and the boxes in a rack use the
/// same painter; a rack's boxes are drawn with their rack.
/// </summary>
public static class DrawOrder
{
    // Sines and cosines leave noise around 1e-16; rounding keeps equal depths equal, so ties fall to the tie order.
    private const int DepthDecimals = 9;

    // An axis seen exactly edge-on separates nothing on screen; projected outlines closer than this only touch.
    private const double Edgewise = 1e-9;

    /// <summary>The racks back to front; ties in rack order.</summary>
    public static IReadOnlyList<SceneRack> BackToFront(IEnumerable<SceneRack> racks, IsoProjection camera) =>
        BackToFront([.. racks], r => r.Bounds, r => r.Number, camera);

    /// <summary>A rack's boxes back to front; ties in the given order (<see cref="SceneRack.Boxes"/>).</summary>
    public static IReadOnlyList<SceneBox> BoxesBackToFront(IReadOnlyList<SceneBox> boxes, IsoProjection camera)
    {
        Dictionary<SceneBox, int> index = [];
        for (var i = 0; i < boxes.Count; i++)
        {
            index.TryAdd(boxes[i], i);
        }

        return BackToFront(boxes, b => b.Bounds, b => index[b], camera);
    }

    /// <summary>
    /// Negative if <paramref name="a"/> lies behind <paramref name="b"/> on a separating axis, so it is drawn first;
    /// positive if in front; 0 if no axis decides. Several separating axes can disagree only for boxes side by side on
    /// screen, which need no order, so a disagreement decides nothing either.
    /// </summary>
    public static int Compare(ShipBox a, ShipBox b, IsoProjection camera)
    {
        var d = camera.ViewDirection;
        (int AMin, int AMax, int BMin, int BMax, double View)[] axes =
        [
            (a.Min.X, a.Max.X, b.Min.X, b.Max.X, d.X),
            (a.Min.Y, a.Max.Y, b.Min.Y, b.Max.Y, d.Y),
            (a.Min.Z, a.Max.Z, b.Min.Z, b.Max.Z, d.Z),
        ];

        var verdict = 0;
        foreach (var (aMin, aMax, bMin, bMax, view) in axes)
        {
            // +1: b lies beyond a along the axis.
            var side = aMax <= bMin ? 1 : bMax <= aMin ? -1 : 0;
            if (side == 0 || Math.Abs(view) < Edgewise)
            {
                continue;
            }

            // The view direction points into the scene: beyond along it means behind, so a is in front.
            var axisVerdict = side * Math.Sign(view);
            if (verdict != 0 && axisVerdict != verdict)
            {
                return 0;
            }

            verdict = axisVerdict;
        }

        return verdict;
    }

    /// <summary>
    /// Starts from the order by centre depth (exact for equal boxes on one grid, D21), then sorts topologically by
    /// <see cref="Compare"/> for every pair whose projected outlines overlap, always taking the earliest free box of
    /// the start order. A cycle (rare for boxes packed on a grid) keeps the start order and says so in the debug log.
    /// </summary>
    private static IReadOnlyList<T> BackToFront<T>(IReadOnlyList<T> items, Func<T, ShipBox> boxOf, Func<T, int> tie, IsoProjection camera)
    {
        T[] start = [.. items.OrderByDescending(i => CentreDepth(boxOf(i), camera)).ThenBy(tie)];
        var count = start.Length;
        var outlines = start.Select(i => Outline(boxOf(i), camera)).ToArray();

        // Sweep along screen x: only boxes whose x ranges overlap can overlap on screen.
        List<int>[] later = [.. Enumerable.Range(0, count).Select(_ => new List<int>())];
        var waiting = new int[count];
        List<int> open = [];
        foreach (var i in Enumerable.Range(0, count).OrderBy(i => outlines[i].Left))
        {
            open.RemoveAll(j => outlines[j].Right <= outlines[i].Left + Edgewise);
            foreach (var j in open.Where(j => outlines[i].Top < outlines[j].Bottom - Edgewise && outlines[j].Top < outlines[i].Bottom - Edgewise))
            {
                var (first, second) = Compare(boxOf(start[i]), boxOf(start[j]), camera) switch
                {
                    < 0 => (i, j),
                    > 0 => (j, i),
                    _ => (-1, -1),
                };
                if (first >= 0)
                {
                    later[first].Add(second);
                    waiting[second]++;
                }
            }

            open.Add(i);
        }

        // Kahn's algorithm; the queue hands out the earliest index of the start order first.
        PriorityQueue<int, int> free = new(Enumerable.Range(0, count).Where(i => waiting[i] == 0).Select(i => (i, i)));
        List<T> sorted = new(count);
        while (free.TryDequeue(out var next, out _))
        {
            sorted.Add(start[next]);
            foreach (var after in later[next])
            {
                if (--waiting[after] == 0)
                {
                    free.Enqueue(after, after);
                }
            }
        }

        if (sorted.Count < count)
        {
            Debug.WriteLine(Invariant($"DrawOrder: {count - sorted.Count} of {count} boxes cover each other in a cycle at yaw {camera.Yaw}, pitch {camera.Pitch}; drawn by centre depth."));
            return start;
        }

        return sorted;
    }

    private static double CentreDepth(ShipBox box, IsoProjection camera)
    {
        var (min, max) = (box.Min, box.Max);
        var depth = camera.Depth((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
        return Math.Round(depth, DepthDecimals);
    }

    /// <summary>The screen rectangle around the box's projected corners.</summary>
    private static (double Left, double Top, double Right, double Bottom) Outline(ShipBox box, IsoProjection camera)
    {
        var corners = Enumerable.Range(0, 8).Select(i => camera.Project(
            (i & 1) == 0 ? box.Min.X : box.Max.X,
            (i & 2) == 0 ? box.Min.Y : box.Max.Y,
            (i & 4) == 0 ? box.Min.Z : box.Max.Z)).ToArray();
        return (corners.Min(p => p.X), corners.Min(p => p.Y), corners.Max(p => p.X), corners.Max(p => p.Y));
    }
}
