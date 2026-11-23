namespace CargoPlan.Core.Routing;

/// <summary>
/// The observations per template (T13.1, U3), one entry per <see cref="ObservationKey"/> in the order first seen. What
/// a scan adds beyond the templates: the places of the system-wide templates whose pools are empty, the Red Wind rewards.
/// Kept in routes.json (<see cref="RoutesFile"/>); nobody reads it as a list.
/// </summary>
public sealed record Observations(IReadOnlyList<Observation> Entries)
{
    public static Observations Empty { get; } = new([]);

    /// <summary>These observations with <paramref name="scan"/>: its key's entry widened, or a new one at the end.</summary>
    public Observations Record(ObservedScan scan)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Key == scan.Key)
            {
                return new([.. Entries.Take(i), Entries[i].With(scan), .. Entries.Skip(i + 1)]);
            }
        }

        return new([.. Entries, Observation.Of(scan)]);
    }
}
