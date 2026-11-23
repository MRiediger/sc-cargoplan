namespace CargoPlan.Core.Routing;

/// <summary>The smallest and largest SCU seen on one delivery line of <paramref name="Commodity"/>, like a leg's band.</summary>
public sealed record ScuBand(string Commodity, int Min, int Max);
