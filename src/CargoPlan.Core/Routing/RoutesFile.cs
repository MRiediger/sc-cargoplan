using System.Text.Json;
using CargoPlan.Core.Json;

namespace CargoPlan.Core.Routing;

/// <summary>What a routes file held: its observations, folded, the schema 1 entries left out (K1 of T13.3), the route and the runs (T13.6).</summary>
/// <param name="Dropped">One line per entry or run left out, for the log.</param>
/// <param name="Route">The route the player flies; null while none is started.</param>
/// <param name="Runs">The finished routes, oldest first.</param>
public sealed record RoutesRead(Observations Observations, IReadOnlyList<string> Dropped, Route? Route, IReadOnlyList<RouteRun> Runs);

/// <summary>
/// Reads and writes routes.json (docs/routes-format.md, D118): the observations per template (T13.1), the route with its
/// placeholders and the finished runs (T13.6). Streams only, as <see cref="Plans.PlanFile"/>; where the file lives and
/// how it is replaced is the app's RoutesBook.
/// </summary>
public static class RoutesFile
{
    /// <summary>Route and runs (T13.6); schema 2 (T13.3, K1: commodities in the key) and 1 (T13.1) are read.</summary>
    public const int SupportedSchemaVersion = 3;

    /// <summary>
    /// Reads a routes file, schema 1 to 3, with one name per place (<see cref="Observations.Folded"/>). Schema 1 keyed
    /// a variant without its commodities: an entry with one commodity is taken as it is, one with several may hold
    /// scans of different templates and is left out (<see cref="RoutesRead.Dropped"/>). A route and runs need schema 3.
    /// The stream is left open.
    /// </summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="RoutesFormatException">Not JSON, another schema version, or out of the format.</exception>
    public static RoutesRead Read(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            var version = SchemaVersion.Check(document.RootElement, [1, 2, SupportedSchemaVersion]);
            var file = document.RootElement.Deserialize(RoutesJsonContext.Default.RoutesFileDto)
                       ?? throw new JsonException("The file contains null.");
            var (observations, dropped) = file.ToObservations(version);
            var (route, runs, droppedRuns) = file.ToRoute(version);
            return new(observations.Folded(), [.. dropped, .. droppedRuns], route, runs);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new RoutesFormatException(sourceName, ex.Message, ex);
        }
    }

    /// <summary>Writes the observations, the route and the runs as indented JSON, schema 3. The stream is left open.</summary>
    public static void Write(Stream stream, Observations observations, Route? route = null, IReadOnlyList<RouteRun>? runs = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonSerializer.Serialize(stream, RoutesFileDto.From(observations, route, runs ?? []), RoutesJsonContext.Default.RoutesFileDto);
    }
}
