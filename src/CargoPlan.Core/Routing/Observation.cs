namespace CargoPlan.Core.Routing;

/// <summary>
/// Everything the scans of one template variant showed (T13.1, U3): where it really picks up and delivers, the SCU per
/// commodity, how often and when. The map and the matrix show it as "observed" (design §8.3).
/// </summary>
/// <param name="Area">The last level a title named; null if none did.</param>
/// <param name="Pickups">Places as the parser normalised them, sorted; joined over the scans.</param>
/// <param name="Destinations">As <paramref name="Pickups"/>.</param>
/// <param name="Scu">One band per commodity, sorted by commodity.</param>
/// <param name="Count">Scans recorded, at least 1; a rescan of the same instance counts too.</param>
public sealed record Observation(
    ObservationKey Key,
    AreaLevel? Area,
    IReadOnlyList<string> Pickups,
    IReadOnlyList<string> Destinations,
    IReadOnlyList<ScuBand> Scu,
    int Count,
    DateTime FirstUtc,
    DateTime LastUtc)
{
    /// <summary>The observation of a first scan.</summary>
    public static Observation Of(ObservedScan scan) => Empty(scan.Key, scan.Utc).With(scan);

    /// <summary>This observation and <paramref name="scan"/> of the same key: sets joined, bands widened, counted.</summary>
    public Observation With(ObservedScan scan) => this with
    {
        Area = scan.Area ?? Area,
        Pickups = Joined(Pickups, scan.Pickups),
        Destinations = Joined(Destinations, scan.Deliveries.Select(d => d.Destination)),
        Scu = Widened(Scu, scan.Deliveries),
        Count = Count + 1,
        LastUtc = scan.Utc > LastUtc ? scan.Utc : LastUtc,
    };

    private static Observation Empty(ObservationKey key, DateTime utc) => new(key, null, [], [], [], 0, utc, utc);

    private static string[] Joined(IEnumerable<string> known, IEnumerable<string> seen) =>
        [.. known.Concat(seen).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static ScuBand[] Widened(IEnumerable<ScuBand> known, IEnumerable<ObservedDelivery> seen) =>
    [
        .. known.Concat(seen.Select(d => new ScuBand(d.Commodity, d.Scu, d.Scu)))
            .GroupBy(b => b.Commodity, StringComparer.Ordinal)
            .Select(g => new ScuBand(g.Key, g.Min(b => b.Min), g.Max(b => b.Max)))
            .OrderBy(b => b.Commodity, StringComparer.Ordinal),
    ];
}
