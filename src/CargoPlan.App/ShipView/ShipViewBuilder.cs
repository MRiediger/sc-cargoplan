using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>Turns a plan into a <see cref="ShipScene"/>. Reads Core's results, plans nothing.</summary>
public static class ShipViewBuilder
{
    /// <param name="violations">The result of <c>LoadPlanner.Validate</c> for this plan.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static ShipScene Build(LoadPlan plan, Ship ship, IReadOnlyList<PlanWarning> violations)
    {
        plan.EnsureBelongsTo(ship);

        var palette = MissionPalette.Assign(plan);
        var violated = violations.Select(v => v.RackId).ToHashSet();

        SceneRack[] racks = [.. ship.Racks
            .Select(r => BuildRack(r, plan.PlacementsForRack(r.Id), palette, violated.Contains(r.Id)))];

        // GroupBy keeps the order of first appearance, the same order as the palette.
        LegendEntry[] legend = [.. plan.Placements
            .GroupBy(p => p.Destination)
            .Select(g => new LegendEntry(palette[g.Key], g.Key, g.Sum(p => p.Item.Scu), g.All(p => p.Delivered)))];

        return new ShipScene(
            racks,
            ship.Bounds,
            legend,
            Messages(plan),
            Messages(Undelivered(plan, ship)),
            [.. violations.Select(v => v.Message)],
            MissionPalette.Repeats(plan));
    }

    private static string[] Messages(LoadPlan plan) => [.. plan.Warnings.Select(w => w.Message).Distinct()];

    /// <summary>The plan with every stop back on board; Core derives its hints (D16).</summary>
    private static LoadPlan Undelivered(LoadPlan plan, Ship ship) => plan.Placements
        .Where(p => p.Delivered)
        .Select(p => p.Destination)
        .Distinct()
        .Aggregate(plan, (current, destination) => LoadPlanner.SetDelivered(current, ship, destination, false));

    private static SceneRack BuildRack(Rack rack, IReadOnlyList<Placement> placements, IReadOnlyDictionary<string, int> palette, bool isViolated)
    {
        var max = rack.Bounds.Max.X;
        double length = max - rack.Bounds.Min.X;

        SceneSegment[] segments = [.. RackSegments.Build(rack, placements).Select(segment =>
        {
            // Segments count from the bow end (x max), so the plan order reads from the bow.
            var (fill, index, toolTip) = segment.Placement switch
            {
                null => (SegmentFill.Free, (int?)null, (string?)null),
                var p => (p.Delivered ? SegmentFill.Delivered : SegmentFill.Mission, palette[p.Destination], ToolTip(rack, p)),
            };
            return new SceneSegment(
                max - (segment.Start + segment.Length) * length, max - segment.Start * length, fill, index, toolTip, segment.Placement?.Destination);
        })];

        return new SceneRack(rack.Number, isViolated, rack.Bounds, segments);
    }

    /// <summary>"Rack 1 · port upper · 16 SCU Titanium (4×4)": crate count × crate size, as in the text renderer.</summary>
    private static string ToolTip(Rack rack, Placement placement)
    {
        var item = placement.Item;
        var crateScu = (int)item.CrateSize;
        var position = $"{rack.Position.Side} {rack.Position.Level}".ToLowerInvariant();
        var text = Invariant($"Rack {rack.Number} · {position} · {item.Scu} SCU {item.Commodity} ({item.Scu / crateScu}×{crateScu})");
        return placement.Delivered ? text + " (delivered)" : text;
    }
}
