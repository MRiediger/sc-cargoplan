namespace CargoPlan.Core.Models;

/// <summary>One crate of a plan and where it should lie: rack and min corner in that rack's grid (D24, D25).</summary>
public sealed record CrateMove(Crate Crate, string RackId, GridPosition Position)
{
    /// <summary>
    /// Moves for <paramref name="crates"/> that put <paramref name="anchor"/> at <paramref name="position"/> of
    /// <paramref name="rackId"/> and keep every other crate's offset to it in ship coordinates (origin + position, D14),
    /// so a selection over two racks lands in the two racks at the same offset (D25). Null if a crate's min corner
    /// lands in no rack; whether the crates fit is <c>LoadPlanner.MoveProblem</c>'s call. Order as in <paramref name="crates"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The ship has no rack of a crate or no rack <paramref name="rackId"/>.</exception>
    public static IReadOnlyList<CrateMove>? Shift(Ship ship, IReadOnlyList<Crate> crates, Crate anchor, string rackId, GridPosition position)
    {
        var from = Corner(ship, anchor.RackId, anchor.Position);
        var to = Corner(ship, rackId, position);
        var (dx, dy, dz) = (to.X - from.X, to.Y - from.Y, to.Z - from.Z);

        List<CrateMove> moves = [];
        foreach (var crate in crates)
        {
            var corner = Corner(ship, crate.RackId, crate.Position);
            ShipPoint target = new(corner.X + dx, corner.Y + dy, corner.Z + dz);
            if (ship.Racks.FirstOrDefault(r => Contains(r, target)) is not { } rack)
            {
                return null;
            }

            moves.Add(new CrateMove(crate, rack.Id, new GridPosition(target.X - rack.Origin.X, target.Y - rack.Origin.Y, target.Z - rack.Origin.Z)));
        }

        return moves;
    }

    /// <summary>The min corner of a cell in ship coordinates.</summary>
    private static ShipPoint Corner(Ship ship, string rackId, GridPosition position)
    {
        var origin = (ship.Racks.FirstOrDefault(r => r.Id == rackId)
            ?? throw new ArgumentException($"Ship '{ship.Id}' has no rack '{rackId}'.", nameof(rackId))).Origin;
        return new ShipPoint(origin.X + position.Length, origin.Y + position.Width, origin.Z + position.Stack);
    }

    /// <summary>True if <paramref name="point"/> is the min corner of a cell of the rack's grid.</summary>
    private static bool Contains(Rack rack, ShipPoint point) =>
        point.X >= rack.Origin.X && point.X < rack.Origin.X + rack.Grid.Length
        && point.Y >= rack.Origin.Y && point.Y < rack.Origin.Y + rack.Grid.Width
        && point.Z >= rack.Origin.Z && point.Z < rack.Origin.Z + rack.Grid.Stack;
}
