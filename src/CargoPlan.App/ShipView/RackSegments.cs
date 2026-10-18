using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// A share of a rack's length, measured from the bow end: 0 is the bow, 1 the aft end.
/// <see cref="Placement"/> is null for the free rest.
/// </summary>
public sealed record RackSegment(double Start, double Length, Placement? Placement)
{
    public bool IsFree => Placement is null;
}

/// <summary>
/// Splits a rack's length by SCU, one segment per placement. The planner assigns racks, not cells, so the view
/// shows shares, not crates (D15).
/// </summary>
public static class RackSegments
{
    /// <summary>
    /// Placements in the given order from the bow end, then the free rest. An overloaded rack (only after a manual
    /// move, D10) is scaled to its load instead of its capacity, so the segments always add up to 1.
    /// </summary>
    public static IReadOnlyList<RackSegment> Build(Rack rack, IReadOnlyList<Placement> placements)
    {
        var used = placements.Sum(p => p.Item.Scu);
        double total = Math.Max(rack.CapacityScu, used);

        List<RackSegment> segments = [];
        double start = 0;
        foreach (var placement in placements)
        {
            var length = placement.Item.Scu / total;
            segments.Add(new RackSegment(start, length, placement));
            start += length;
        }

        if (used < rack.CapacityScu)
        {
            segments.Add(new RackSegment(start, (rack.CapacityScu - used) / total, null));
        }

        return segments;
    }
}
