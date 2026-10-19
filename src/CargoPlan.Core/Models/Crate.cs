namespace CargoPlan.Core.Models;

/// <summary>
/// One crate on board: what it is, for whom, and where it lies in which rack (D24). The unit of a plan; placements are
/// derived from crates (<see cref="LoadPlan.Placements"/>). In a valid plan no two crates share a cell, so the position
/// tells two crates of the same kind apart.
/// </summary>
public sealed record Crate(string Destination, string Commodity, CrateSize CrateSize, string RackId, GridPosition Position, bool Delivered = false)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public CrateSize CrateSize { get; } =
        Enum.IsDefined(CrateSize) ? CrateSize : throw new ArgumentOutOfRangeException(nameof(CrateSize), CrateSize, "Unknown crate size.");

    /// <summary>The cells the crate fills, in its only orientation (D9).</summary>
    public IEnumerable<GridPosition> Cells
    {
        get
        {
            var size = CrateSize.Dimensions;
            return from l in Enumerable.Range(Position.Length, size.Length)
                   from w in Enumerable.Range(Position.Width, size.Width)
                   from s in Enumerable.Range(Position.Stack, size.Stack)
                   select new GridPosition(l, w, s);
        }
    }

    /// <summary>The cells of its lowest layer, the ones that rest on the plate or on other crates.</summary>
    public IEnumerable<GridPosition> Bottom => Cells.Where(c => c.Stack == Position.Stack);
}
