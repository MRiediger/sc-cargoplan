namespace CargoPlan.Core.Models;

/// <summary>One crate of a plan and where it should lie: rack and min corner in that rack's grid (D24, D25).</summary>
public sealed record CrateMove(Crate Crate, string RackId, GridPosition Position)
{
    /// <summary>
    /// Moves for <paramref name="crates"/> that put <paramref name="anchor"/> at <paramref name="position"/> of
    /// <paramref name="rackId"/> and keep every other crate where it was relative to it (D25, D26): crates from one rack
    /// keep their grid offset, so a stack stays a stack even between racks stacking in opposite directions; crates from
    /// several racks keep their offset in ship coordinates, so they land in the racks at the same offset. Null if a crate's
    /// min corner lands in no rack; where the crates end up is <c>LoadPlanner.ResolveMove</c>'s call (D65). Order as in <paramref name="crates"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The ship has no rack of a crate or no rack <paramref name="rackId"/>.</exception>
    public static IReadOnlyList<CrateMove>? Shift(Ship ship, IReadOnlyList<Crate> crates, Crate anchor, string rackId, GridPosition position)
    {
        var source = RackOf(ship, anchor.RackId);
        var target = RackOf(ship, rackId);
        return crates.All(c => c.RackId == source.Id)
            ? InGrid(target, crates, anchor, position)
            : InShip(ship, source, target, crates, anchor, position);
    }

    private static List<CrateMove>? InGrid(Rack target, IReadOnlyList<Crate> crates, Crate anchor, GridPosition position)
    {
        var grid = target.Grid;
        List<CrateMove> moves = [];
        foreach (var crate in crates)
        {
            var (l, w, s) = (
                position.Length + crate.Position.Length - anchor.Position.Length,
                position.Width + crate.Position.Width - anchor.Position.Width,
                position.Stack + crate.Position.Stack - anchor.Position.Stack);
            if (l < 0 || w < 0 || s < 0 || l >= grid.Length || w >= grid.Width || s >= grid.Stack)
            {
                return null;
            }

            moves.Add(new CrateMove(crate, target.Id, new GridPosition(l, w, s)));
        }

        return moves;
    }

    private static List<CrateMove>? InShip(Ship ship, Rack source, Rack target, IReadOnlyList<Crate> crates, Crate anchor, GridPosition position)
    {
        var from = source.CellBox(anchor.Position, anchor.CrateSize).Min;
        var to = target.CellBox(position, anchor.CrateSize).Min;
        var (dx, dy, dz) = (to.X - from.X, to.Y - from.Y, to.Z - from.Z);

        List<CrateMove> moves = [];
        foreach (var crate in crates)
        {
            if (crate == anchor)
            {
                // Named by the caller: no rack earlier in rack order may claim it if it sticks out of the target.
                moves.Add(new CrateMove(crate, target.Id, position));
                continue;
            }

            var box = RackOf(ship, crate.RackId).CellBox(crate.Position, crate.CrateSize);
            ShipBox moved = new(
                new ShipPoint(box.Min.X + dx, box.Min.Y + dy, box.Min.Z + dz),
                new ShipPoint(box.Max.X + dx, box.Max.Y + dy, box.Max.Z + dz));

            // Racks don't overlap, but a crate sticking out could have its min corner in two: rack order decides.
            var landing = ship.Racks.Select(r => (Rack: r, Position: r.ToGrid(moved))).FirstOrDefault(l => l.Position is not null);
            if (landing.Position is null)
            {
                return null;
            }

            moves.Add(new CrateMove(crate, landing.Rack.Id, landing.Position));
        }

        return moves;
    }

    private static Rack RackOf(Ship ship, string rackId) =>
        ship.Racks.FirstOrDefault(r => r.Id == rackId)
        ?? throw new ArgumentException($"Ship '{ship.Id}' has no rack '{rackId}'.", nameof(rackId));
}
