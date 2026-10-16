using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>
/// File format of a mission JSON (schema version 1). Read via <see cref="ToStops"/>,
/// written via <see cref="FromStops"/>, so import and export share one format.
/// </summary>
internal sealed record MissionFileDto(int SchemaVersion, IReadOnlyList<StopDto> Stops)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public IReadOnlyList<Stop> ToStops()
    {
        if (Stops.Count == 0)
        {
            throw new ArgumentException("The file contains no stops.", nameof(Stops));
        }

        // Array order is the route: first stop = Order 1.
        IReadOnlyList<Stop> stops = [.. Stops.Select((s, index) => s.ToStop(index + 1))];

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

    public static MissionFileDto FromStops(IEnumerable<Stop> stops) =>
        new(MissionFileReader.SupportedSchemaVersion, [.. stops.OrderBy(s => s.Order).Select(StopDto.FromStop)]);
}
