using System.Text.Json;
using CargoPlan.Core.Json;

namespace CargoPlan.Core.Routing;

/// <summary>
/// Reads and writes routes.json (docs/routes-format.md, D118): the observations per template (T13.1), later the route.
/// Streams only, as <see cref="Plans.PlanFile"/>; where the file lives and how it is replaced is the app's RoutesStore.
/// </summary>
public static class RoutesFile
{
    /// <summary>Observations only (T13.1).</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>Reads a routes file. The stream is left open.</summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="RoutesFormatException">Not JSON, another schema version, or out of the format.</exception>
    public static Observations Read(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            SchemaVersion.Check(document.RootElement, SupportedSchemaVersion);
            var file = document.RootElement.Deserialize(RoutesJsonContext.Default.RoutesFileDto)
                       ?? throw new JsonException("The file contains null.");
            return file.ToObservations();
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
