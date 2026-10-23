using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>
/// The outlines that loaded, by Fleetyards slug, and one line per problem (D55): a broken entry costs only its own
/// outline, a broken file all of them, never the start. A ship without an outline shows the bow wedge.
/// </summary>
public sealed record ShipOutlineCatalog(IReadOnlyDictionary<string, ShipOutline> Outlines, IReadOnlyList<string> Problems)
{
    public static ShipOutlineCatalog Empty { get; } = new(new Dictionary<string, ShipOutline>(), []);

    /// <summary>The outline of <paramref name="ship"/> by its <c>fleetyardsSlug</c> (case-sensitive), or null.</summary>
    public ShipOutline? For(Ship ship) =>
        ship.FleetyardsSlug is { } slug && Outlines.TryGetValue(slug, out var outline) ? outline : null;
}
