using System.Text.Json.Serialization;
using CargoPlan.Core.Json;
using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>
/// File format of a mission JSON (schema version 4, D88: schema 3 with an optional row axis). Read via
/// <see cref="ToMissionFile"/>, written via <see cref="FromMissionStops"/>, so import and export share one format.
/// Version 3 is the same without <see cref="FillRows"/>, version 2 also without <see cref="FillOrder"/> (D72); version 1
/// (<see cref="MissionFileV1Dto"/>) is still read, none of them is written.
/// </summary>
/// <remarks>
/// <see cref="FillOrder"/> and <see cref="FillRows"/> are optional, so they have a default and stand last; <c>JsonPropertyOrder</c> on
/// <see cref="Stops"/> writes them before the stops, where they are read at a glance. Source:
/// https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties#configure-the-order-of-serialized-properties
/// </remarks>
internal sealed record MissionFileDto(
    int SchemaVersion,
    [property: JsonPropertyOrder(1)] IReadOnlyList<MissionStopDto> Stops,
    string? FillOrder = null,
    string? FillRows = null)
{
    /// <exception cref="ArgumentException">A value violates the domain rules, or a file names a fill order or row axis older than its schema.</exception>
    public MissionFile ToMissionFile()
    {
        const int withFillOrder = MissionFileReader.NoFillOrderSchemaVersion + 1;
        if (FillOrder is not null && SchemaVersion < withFillOrder)
        {
            throw new ArgumentException(
                Invariant($"fillOrder is new in schema {withFillOrder} (the file says {SchemaVersion})."), nameof(FillOrder));
        }

        const int withFillRows = MissionFileReader.NoFillRowsSchemaVersion + 1;
        if (FillRows is not null && SchemaVersion < withFillRows)
        {
            throw new ArgumentException(
                Invariant($"fillRows is new in schema {withFillRows} (the file says {SchemaVersion})."), nameof(FillRows));
        }

        return new(ToMissionStops(), FillOrder is null ? null : FillOrderJson.Parse(FillOrder), FillRows is null ? null : FillRowsJson.Parse(FillRows));
    }

    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    /// <exception cref="ArgumentException">A stop or item is broken, or an item says manual, which only plan files do (K6.5).</exception>
    public IReadOnlyList<MissionStop> ToMissionStops()
    {
        if (Stops.SelectMany(s => s.Items.Select(i => (s.Destination, i))).FirstOrDefault(x => x.i.Manual is not null) is ({ } destination, { } manual))
        {
            throw new ArgumentException($"Stop '{destination}', {manual.Commodity}: 'manual' belongs to a plan file, not to a mission file.", nameof(Stops));
        }

        return CheckedRoute([.. Stops.Select((s, index) => s.ToMissionStop(index + 1))]);
    }

    /// <summary>Always with the fill order and row axis: an export says how its rows were meant to be loaded (D72, D88).</summary>
    public static MissionFileDto FromMissionStops(IEnumerable<MissionStop> stops, Models.FillOrder fillOrder, Models.FillRows fillRows) =>
        new(
            MissionFileReader.CurrentSchemaVersion,
            [.. stops.OrderBy(s => s.Order).Select(s => MissionStopDto.FromMissionStop(s, withManual: false))],
            FillOrderJson.Text(fillOrder),
            FillRowsJson.Text(fillRows));

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
