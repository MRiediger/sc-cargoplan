namespace CargoPlan.Core.Models;

/// <summary>
/// One crate on board: what it is, for whom, and where it lies in which rack (D24). The unit of a plan; placements are
/// derived from crates (<see cref="LoadPlan.Placements"/>). In a valid plan no two crates share a cell, so the position
/// tells two crates of the same kind apart.
/// </summary>
/// <param name="Mission">Id of the mission the crate belongs to (D42); 0 in a plan without missions (planner, CLI).</param>
/// <param name="Item">
/// Index of its contract item in the mission's <c>Items</c> (D68), so two items of one kind stay two (another pickup);
/// 0 in a plan without missions. Like the mission, checked against the missions by <c>SavedPlan</c>.
/// </param>
/// <param name="Rotated">Turned 90° about the rack's stack axis (D71): length and width swap. Only an oblong crate turns.</param>
public sealed record Crate(
    string Destination, string Commodity, CrateSize CrateSize, string RackId, GridPosition Position, bool Delivered = false, int Mission = 0, int Item = 0,
    bool Rotated = false)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public CrateSize CrateSize { get; } =
        Enum.IsDefined(CrateSize) ? CrateSize : throw new ArgumentOutOfRangeException(nameof(CrateSize), CrateSize, "Unknown crate size.");

    // Checked in the initializer (constructor) and in init (`with`): a square crate has one way to lie, not two spellings.
    public bool Rotated { get; init => field = Turnable(CrateSize, value); } = Turnable(CrateSize, Rotated);

    /// <summary>The shape in the grid as it lies (<see cref="CrateSizeExtensions"/>, D71).</summary>
    public Dimensions Shape => CrateSize.Shape(Rotated);

    /// <summary>The cells the crate fills as it lies.</summary>
    public IEnumerable<GridPosition> Cells
    {
        get
        {
            var size = Shape;
            return from l in Enumerable.Range(Position.Length, size.Length)
                   from w in Enumerable.Range(Position.Width, size.Width)
                   from s in Enumerable.Range(Position.Stack, size.Stack)
                   select new GridPosition(l, w, s);
        }
    }

    /// <summary>The cells of its lowest layer, the ones that rest on the plate or on other crates.</summary>
    public IEnumerable<GridPosition> Bottom => Cells.Where(c => c.Stack == Position.Stack);

    private static bool Turnable(CrateSize size, bool rotated) =>
        !rotated || size.Turns ? rotated : throw new ArgumentException(FormattableString.Invariant($"A {(int)size} SCU crate is square and doesn't turn."), nameof(Rotated));
}
