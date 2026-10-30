using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Where a line enters an axis-aligned box, for the drop target (D25), the rack numbers' cover test (T8.2) and the hit
/// test (T8.3). Pure; the slab method: the line is inside the box where it is inside all three pairs of planes.
/// Source: Kay &amp; Kajiya (1986), Ray tracing complex scenes, SIGGRAPH '86.
/// </summary>
public static class RayBox
{
    // A step this small runs parallel to a pair of planes; a span this short only grazes an edge.
    private const double Graze = 1e-9;

    /// <summary>The distance along <paramref name="ray"/> where it enters the box; null if it misses; negative behind its origin.</summary>
    public static double? Entry(Ray ray, Point3D min, Point3D max) => Span(ray, min, max)?.Enter;

    /// <summary>
    /// The distance along <paramref name="ray"/> to the first point of the box in front of its origin: where it enters,
    /// or 0 if the origin lies inside (a camera in a large rack); null if the box misses or lies behind.
    /// </summary>
    public static double? Ahead(Ray ray, Point3D min, Point3D max) => Span(ray, min, max) switch
    {
        { Enter: >= 0 } span => span.Enter,
        { Leave: > 0 } => 0,
        _ => null,
    };

    /// <summary>Where <paramref name="ray"/> enters and leaves the box, as distances along it; null if it misses.</summary>
    public static (double Enter, double Leave)? Span(Ray ray, Point3D min, Point3D max) => Span(
        (min.X, min.Y, min.Z), (max.X, max.Y, max.Z), (ray.Origin.X, ray.Origin.Y, ray.Origin.Z), (ray.Direction.X, ray.Direction.Y, ray.Direction.Z));

    /// <summary>As t of <paramref name="origin"/> + t·<paramref name="direction"/> (smaller lies nearer the origin); null if it misses.</summary>
    public static double? Entry(
        (double X, double Y, double Z) min, (double X, double Y, double Z) max, (double X, double Y, double Z) origin, (double X, double Y, double Z) direction) =>
        Span(min, max, origin, direction)?.Enter;

    private static (double Enter, double Leave)? Span(
        (double X, double Y, double Z) min, (double X, double Y, double Z) max, (double X, double Y, double Z) origin, (double X, double Y, double Z) direction)
    {
        var (enter, leave) = (double.NegativeInfinity, double.PositiveInfinity);
        foreach (var (low, high, start, step) in (ReadOnlySpan<(double, double, double, double)>)
            [(min.X, max.X, origin.X, direction.X), (min.Y, max.Y, origin.Y, direction.Y), (min.Z, max.Z, origin.Z, direction.Z)])
        {
            if (Math.Abs(step) < Graze)
            {
                // Parallel to this pair of planes: inside between them or nowhere.
                if (start <= low || start >= high)
                {
                    return null;
                }

                continue;
            }

            var (a, b) = ((low - start) / step, (high - start) / step);
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
        }

        return leave - enter > Graze ? (enter, leave) : null;
    }
}
