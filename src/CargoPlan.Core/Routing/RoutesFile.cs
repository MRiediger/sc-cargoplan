using System.Text.Json;
using CargoPlan.Core.Json;

namespace CargoPlan.Core.Routing;

/// <summary>What a routes file held: its observations, folded, and the schema 1 entries left out (K1 of T13.3).</summary>
/// <param name="Dropped">One line per entry left out, for the log.</param>
public sealed record RoutesRead(Observations Observations, IReadOnlyList<string> Dropped);

/// <summary>
/// Reads and writes routes.json (docs/routes-format.md, D118): the observations per template (T13.1), later the route.
/// Streams only, as <see cref="Plans.PlanFile"/>; where the file lives and how it is replaced is the app's RoutesBook.
/// </summary>
public static class RoutesFile
{
    /// <summary>Observations keyed with their commodities (T13.3, K1); schema 1 (T13.1) is read.</summary>
    public const int SupportedSchemaVersion = 2;

    /// <summary>
    /// Reads a routes file, schema 1 or 2, with one name per place (<see cref="Observations.Folded"/>). Schema 1 keyed
    /// a variant without its commodities: an entry with one commodity is taken as it is, one with several may hold
    /// scans of different templates and is left out (<see cref="RoutesRead.Dropped"/>). The stream is left open.
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
            var version = SchemaVersion.Check(document.RootElement, [1, SupportedSchemaVersion]);
            var file = document.RootElement.Deserialize(RoutesJsonContext.Default.RoutesFileDto)
                       ?? throw new JsonException("The file contains null.");
            var (observations, dropped) = file.ToObservations(version);
            return new(observations.Folded(), dropped);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new RoutesFormatException(sourceName, ex.Message, ex);
        }
    }

    /// <summary>Writes the observations as indented JSON. The stream is left open.</summary>
    public static void Write(Stream stream, Observations observations)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonSerializer.Serialize(stream, RoutesFileDto.From(observations), RoutesJsonContext.Default.RoutesFileDto);
    }
}
