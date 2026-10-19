using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>Turns a plan into a <see cref="ShipScene"/>. Reads Core's results, plans nothing.</summary>
public static class ShipViewBuilder
{
    /// <param name="violations">The result of <c>LoadPlanner.Validate</c> for this plan.</param>
    /// <param name="fillOrder">Where the cells of a rack start (D21); no default, the view model decides it in one place.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static ShipScene Build(LoadPlan plan, Ship ship, IReadOnlyList<PlanWarning> violations, FillOrder fillOrder)
    {
        plan.EnsureBelongsTo(ship);

        var palette = MissionPalette.Assign(plan);
        var violated = violations.Select(v => v.RackId).ToHashSet();

        SceneRack[] racks = [.. ship.Racks
            .Select(r => BuildRack(r, plan.PlacementsForRack(r.Id), palette, violated.Contains(r.Id), fillOrder))];

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

    private static SceneRack BuildRack(
        Rack rack, IReadOnlyList<Placement> placements, IReadOnlyDictionary<string, int> palette, bool isViolated, FillOrder fillOrder)
    {
        var origin = rack.Origin;

        // One box per block; GroupBy keeps the order of first appearance, the fill order of each box's first cell.
        SceneBox[] boxes = [.. RackCells.Build(rack, placements, fillOrder, isViolated).GroupBy(c => c.Block).Select(block =>
        {
            var cell = block.First();
            var (fill, index, toolTip) = cell.Placement switch
            {
                null => (CellFill.Free, (int?)null, (string?)null),
                var p => (p.Delivered ? CellFill.Delivered : CellFill.Mission, palette[p.Destination], ToolTip(rack, placements, p, cell.Crate)),
            };

            return new SceneBox(ToShip(origin, block.Key), fill, index, toolTip, cell.Placement, cell.Crate);
        })];

        return new SceneRack(rack.Id, rack.Number, isViolated, rack.Bounds, boxes);
    }

    /// <summary>Grid axes are the ship axes (D14): length along x, width along y, stack along z.</summary>
    private static ShipBox ToShip(ShipPoint origin, GridBox box)
    {
        ShipPoint min = new(origin.X + box.Length, origin.Y + box.Width, origin.Z + box.Stack);
        return new ShipBox(min, new ShipPoint(min.X + box.Size.Length, min.Y + box.Size.Width, min.Z + box.Size.Stack));
    }

    /// <summary>
    /// "Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)"; equal placements in the rack count together, like
    /// their crate numbers. Without a crate (an overloaded rack) the placement: "Rack 1 · port upper · 16 SCU Titanium (4×4)",
    /// crate count × crate size, as in the text renderer.
    /// </summary>
    private static string ToolTip(Rack rack, IReadOnlyList<Placement> placements, Placement placement, CrateId? crate)
    {
        var item = placement.Item;
        var crateScu = (int)item.CrateSize;
        var position = $"{rack.Position.Side} {rack.Position.Level}".ToLowerInvariant();
        var text = crate is null
            ? Invariant($"Rack {rack.Number} · {position} · {item.Scu} SCU {item.Commodity} ({item.Scu / crateScu}×{crateScu})")
            : Invariant($"{placement.Destination} · Rack {rack.Number} · {position} · {crateScu} SCU {item.Commodity} crate ({crate.Number} of {placements.Where(p => p == placement).Sum(p => p.Item.Scu) / crateScu})");
        return placement.Delivered ? text + " (delivered)" : text;
    }

}
