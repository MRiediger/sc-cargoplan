namespace CargoPlan.Core.Ships;

/// <summary>
/// A ship's silhouette seen from above (D55), traced from its Fleetyards render: in metres, origin in the middle of the
/// top view, x towards the bow, y towards port, closed implicitly. Drawn as a backdrop, never used for rack positions.
/// </summary>
public sealed record ShipOutline(string Slug, IReadOnlyList<OutlinePoint> Points);

/// <summary>One corner of a <see cref="ShipOutline"/>, in metres.</summary>
public readonly record struct OutlinePoint(double X, double Y);
