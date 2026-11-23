namespace CargoPlan.Core.Routing;

/// <summary>What one readable scan adds to the observations: its key, places and amounts (the import's ToObservation).</summary>
/// <param name="Area">The level the title names; null if none.</param>
/// <param name="Pickups">Every place of the Collect lines.</param>
/// <param name="Deliveries">One per delivery line, a leg of the template.</param>
/// <param name="Utc">When it was scanned.</param>
public sealed record ObservedScan(
    ObservationKey Key, AreaLevel? Area, IReadOnlyList<string> Pickups, IReadOnlyList<ObservedDelivery> Deliveries, DateTime Utc);

/// <summary>One delivery line: <paramref name="Scu"/> of <paramref name="Commodity"/> to <paramref name="Destination"/>.</summary>
public sealed record ObservedDelivery(string Destination, string Commodity, int Scu);
