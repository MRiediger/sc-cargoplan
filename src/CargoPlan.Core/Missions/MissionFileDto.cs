namespace CargoPlan.Core.Missions;

/// <summary>
/// File format of a mission JSON (schema version 2, D23). Read via <see cref="ToMissionStops"/>, written via
/// <see cref="FromMissionStops"/>, so import and export share one format. Version 1 is still read
/// (<see cref="MissionFileV1Dto"/>), never written.
/// </summary>
internal sealed record MissionFileDto(int SchemaVersion, IReadOnlyList<MissionStopDto> Stops)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public IReadOnlyList<MissionStop> ToMissionStops() =>
        CheckedRoute([.. Stops.Select((s, index) => s.ToMissionStop(index + 1))]);

    public static MissionFileDto FromMissionStops(IEnumerable<MissionStop> stops) =>
        new(MissionFileReader.CurrentSchemaVersion, [.. stops.OrderBy(s => s.Order).Select(MissionStopDto.FromMissionStop)]);

    /// <summary>The route rules of both schema versions. Array order is the route: first stop = Order 1.</summary>
    /// <exception cref="ArgumentException">No stop, or a destination twice.</exception>
    public static IReadOnlyList<MissionStop> CheckedRoute(IReadOnlyList<MissionStop> stops)
    {
        if (stops.Count == 0)
        {
            throw new ArgumentException("The file contains no stops.", nameof(Stops));
        }

        // One stop per destination, otherwise the cargo lift sees "foreign" crates of the second stop.
        // Case-insensitive, because "Riker" and "riker" are the same place for a human.
        var duplicate = stops
            .GroupBy(s => s.Destination, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Destination '{duplicate.Last().Destination}' is used more than once. Put all its items into one stop.",
                nameof(Stops));
        }

        return stops;
    }
}
