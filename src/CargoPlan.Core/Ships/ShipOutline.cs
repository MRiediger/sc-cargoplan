namespace CargoPlan.Core.Ships;

/// <summary>
/// A ship's silhouette seen from above (D55), traced from its Fleetyards render: in metres, origin in the middle of the
/// top view, x towards the bow, y towards port, closed implicitly. Drawn as a backdrop, never used for rack positions.
/// </summary>
/// <param name="HeightM">The hull's height in metres (Fleetyards); places the outline on the hologram's floor (D83, T8.5). null = unknown.</param>
public sealed record ShipOutline(string Slug, IReadOnlyList<OutlinePoint> Points, double? HeightM = null);

/// <summary>One corner of a <see cref="ShipOutline"/>, in metres.</summary>
public readonly record struct OutlinePoint(double X, double Y);
