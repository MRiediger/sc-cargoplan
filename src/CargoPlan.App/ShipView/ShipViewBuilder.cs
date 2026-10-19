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

        SceneCell[] cells = [.. RackCells.Build(rack, placements, fillOrder).Select(cell =>
        {
            var (fill, index, toolTip) = cell.Placement switch
            {
                null => (CellFill.Free, (int?)null, (string?)null),
                var p => (p.Delivered ? CellFill.Delivered : CellFill.Mission, palette[p.Destination], ToolTip(rack, p)),
            };

            // Grid axes are the ship axes (D14): length along x, width along y, stack along z.
            ShipPoint min = new(origin.X + cell.Length, origin.Y + cell.Width, origin.Z + cell.Stack);
            return new SceneCell(new ShipBox(min, new ShipPoint(min.X + 1, min.Y + 1, min.Z + 1)), fill, index, toolTip, cell.Placement);
        })];

        return new SceneRack(rack.Id, rack.Number, isViolated, rack.Bounds, cells);
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
