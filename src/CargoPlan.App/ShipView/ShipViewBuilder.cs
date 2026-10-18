using System.Windows;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>Turns a plan into a <see cref="ShipScene"/>. Reads Core's results, plans nothing.</summary>
public static class ShipViewBuilder
{
    /// <param name="violations">The result of <c>LoadPlanner.Validate</c> for this plan.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static ShipScene Build(LoadPlan plan, Ship ship, IReadOnlyList<PlanWarning> violations)
    {
        if (plan.ShipId != ship.Id)
        {
            throw new ArgumentException($"The plan is for ship '{plan.ShipId}', not '{ship.Id}'.", nameof(ship));
        }

        var palette = MissionPalette.Assign(plan);
        var violated = violations.Select(v => v.RackId).ToHashSet();

        SceneRack[] racks = [.. DrawOrder.BackToFront(ship.Racks)
            .Select(r => BuildRack(r, plan.PlacementsForRack(r.Id), palette, violated.Contains(r.Id)))];

        // GroupBy keeps the order of first appearance, the same order as the palette.
        LegendEntry[] legend = [.. plan.Placements
            .GroupBy(p => p.Destination)
            .Select(g => new LegendEntry(palette[g.Key], g.Key, g.Sum(p => p.Item.Scu)))];

        var (frame, extent) = Frame(ship.Bounds);

        return new ShipScene(
            racks,
            frame,
            extent,
            legend,
            [.. plan.Warnings.Select(w => w.Message).Distinct()],
            [.. violations.Select(v => v.Message)],
            MissionPalette.Repeats(plan));
    }

    private static SceneRack BuildRack(Rack rack, IReadOnlyList<Placement> placements, IReadOnlyDictionary<string, int> palette, bool isViolated)
    {
        var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);
        double length = max.X - min.X;

        List<SceneStrip> strips = [];
        foreach (var segment in RackSegments.Build(rack, placements))
        {
            // Segments count from the bow end (x max), so the plan order reads left to right on screen.
            var bowSide = max.X - segment.Start * length;
            var aftSide = max.X - (segment.Start + segment.Length) * length;
            var (fill, index, toolTip) = segment.Placement switch
            {
                null => (SegmentFill.Free, (int?)null, (string?)null),
                var p => (p.Delivered ? SegmentFill.Delivered : SegmentFill.Mission, palette[p.Destination], ToolTip(rack, p)),
            };

            strips.Add(new SceneStrip(Top(rack.Bounds, aftSide, bowSide), fill, index, toolTip));
            strips.Add(new SceneStrip(Port(rack.Bounds, aftSide, bowSide), fill, index, toolTip));
            if (segment.Start == 0)
            {
                strips.Add(new SceneStrip(Bow(rack.Bounds), fill, index, toolTip));
            }
        }

        return new SceneRack(
            rack.Number,
            isViolated,
            [Top(rack.Bounds, min.X, max.X), Port(rack.Bounds, min.X, max.X), Bow(rack.Bounds)],
            strips,
            IsoProjection.Project(max.X, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0));
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

    // The three faces the viewer sees from bow, port and above (D15), from x = aft to x = bow.
    private static Point[] Top(ShipBox box, double aft, double bow) =>
    [
        IsoProjection.Project(aft, box.Min.Y, box.Max.Z),
        IsoProjection.Project(bow, box.Min.Y, box.Max.Z),
        IsoProjection.Project(bow, box.Max.Y, box.Max.Z),
        IsoProjection.Project(aft, box.Max.Y, box.Max.Z),
    ];

    private static Point[] Port(ShipBox box, double aft, double bow) =>
    [
        IsoProjection.Project(aft, box.Max.Y, box.Min.Z),
        IsoProjection.Project(bow, box.Max.Y, box.Min.Z),
        IsoProjection.Project(bow, box.Max.Y, box.Max.Z),
        IsoProjection.Project(aft, box.Max.Y, box.Max.Z),
    ];

    private static Point[] Bow(ShipBox box) =>
    [
        IsoProjection.Project(box.Max.X, box.Min.Y, box.Min.Z),
        IsoProjection.Project(box.Max.X, box.Max.Y, box.Min.Z),
        IsoProjection.Project(box.Max.X, box.Max.Y, box.Max.Z),
        IsoProjection.Project(box.Max.X, box.Min.Y, box.Max.Z),
    ];

    /// <summary>The 12 edges of the box: every pair of corners that differ on exactly one axis.</summary>
    private static (SceneLine[] Edges, Rect Extent) Frame(ShipBox box)
    {
        Point[] corners = [.. Enumerable.Range(0, 8).Select(i => IsoProjection.Project(
            (i & 1) == 0 ? box.Min.X : box.Max.X,
            (i & 2) == 0 ? box.Min.Y : box.Max.Y,
            (i & 4) == 0 ? box.Min.Z : box.Max.Z))];

        SceneLine[] edges = [.. from i in Enumerable.Range(0, 8)
                                from axis in (int[])[1, 2, 4]
                                where (i & axis) == 0
                                select new SceneLine(corners[i], corners[i | axis])];

        var extent = corners.Aggregate(new Rect(corners[0], corners[0]), (rect, p) => Rect.Union(rect, p));
        return (edges, extent);
    }
}
