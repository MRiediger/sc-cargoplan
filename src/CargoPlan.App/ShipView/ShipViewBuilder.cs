using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>Turns a plan into a <see cref="ShipScene"/>. Reads Core's results, plans nothing.</summary>
public static class ShipViewBuilder
{
    private static readonly Dimensions OneCell = new(1, 1, 1);

    /// <param name="violations">The result of <c>LoadPlanner.Validate</c> for this plan.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static ShipScene Build(LoadPlan plan, Ship ship, IReadOnlyList<PlanWarning> violations)
    {
        plan.EnsureBelongsTo(ship);

        var palette = MissionPalette.Assign(plan);
        var violated = violations.Select(v => v.RackId).ToHashSet();

        SceneRack[] racks = [.. ship.Racks.Select(r => BuildRack(r, plan, palette, violated.Contains(r.Id)))];

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

    /// <summary>
    /// The rack's crates where the plan stores them (D24), then the cells no crate takes. No layout here: the only one is
    /// <see cref="CrateLayout"/> in Core.
    /// </summary>
    private static SceneRack BuildRack(Rack rack, LoadPlan plan, IReadOnlyDictionary<string, int> palette, bool isViolated)
    {
        var crates = plan.CratesForRack(rack.Id);

        // Only a red rack can hold a faulty crate; valid plans skip the check of every pair.
        IReadOnlySet<Crate> faulty = isViolated ? CrateLayout.Faulty(rack, crates) : new HashSet<Crate>();

        SceneBox[] cargo = [.. plan.PlacementsForRack(rack.Id).SelectMany(placement =>
        {
            Crate[] own = [.. crates.Where(c => Belongs(c, placement))];
            var fill = placement.Delivered ? CellFill.Delivered : CellFill.Mission;
            return own.Select((crate, i) => new SceneBox(
                ToShip(rack.Origin, crate.Position, crate.CrateSize.Dimensions),
                fill,
                palette[placement.Destination],
                ToolTip(rack, placement, i + 1, own.Length),
                placement,
                crate,
                faulty.Contains(crate)));
        })];

        HashSet<GridPosition> taken = [.. crates.SelectMany(c => c.Cells)];
        var grid = rack.Grid;
        SceneBox[] free =
        [
            .. from l in Enumerable.Range(0, grid.Length)
               from w in Enumerable.Range(0, grid.Width)
               from s in Enumerable.Range(0, grid.Stack)
               let cell = new GridPosition(l, w, s)
               where !taken.Contains(cell)
               select new SceneBox(ToShip(rack.Origin, cell, OneCell), CellFill.Free, null, null, null, null, IsViolated: false),
        ];

        return new SceneRack(rack.Id, rack.Number, isViolated, rack.Bounds, [.. cargo, .. free]);
    }

    /// <summary>The crate is one of the placement's: the grouping of <c>LoadPlan.Placements</c>, within one rack.</summary>
    private static bool Belongs(Crate crate, Placement placement) =>
        crate.Destination == placement.Destination
        && crate.Commodity == placement.Item.Commodity
        && crate.CrateSize == placement.Item.CrateSize
        && crate.Delivered == placement.Delivered;

    /// <summary>Grid axes are the ship axes (D14): length along x, width along y, stack along z.</summary>
    private static ShipBox ToShip(ShipPoint origin, GridPosition position, Dimensions size)
    {
        ShipPoint min = new(origin.X + position.Length, origin.Y + position.Width, origin.Z + position.Stack);
        return new ShipBox(min, new ShipPoint(min.X + size.Length, min.Y + size.Width, min.Z + size.Stack));
    }

    /// <summary>
    /// "Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)": <paramref name="number"/> counts the crate within
    /// its placement in plan order, <paramref name="count"/> is the placement's number of crates.
    /// </summary>
    private static string ToolTip(Rack rack, Placement placement, int number, int count)
    {
        var item = placement.Item;
        var position = $"{rack.Position.Side} {rack.Position.Level}".ToLowerInvariant();
        var text = Invariant($"{placement.Destination} · Rack {rack.Number} · {position} · {(int)item.CrateSize} SCU {item.Commodity} crate ({number} of {count})");
        return placement.Delivered ? text + " (delivered)" : text;
    }
}
