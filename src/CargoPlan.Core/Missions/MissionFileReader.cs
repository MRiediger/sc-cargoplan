using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>
/// Reads mission files (see docs/mission-format.md). The stream overload is the core:
/// CLI reads files, tests read embedded resources, the editor imports files, all through one code path.
/// </summary>
public static class MissionFileReader
{
    public const int SupportedSchemaVersion = 1;

    /// <summary>Reads a mission file from disk.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="MissionFormatException">The file is broken.</exception>
    public static IReadOnlyList<Stop> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Read(stream, path);
    }

    /// <summary>Reads a mission file from a stream. The stream is left open.</summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="MissionFormatException">The content is broken.</exception>
    public static IReadOnlyList<Stop> Read(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            SchemaVersion.Check(document.RootElement, SupportedSchemaVersion);

            var dto = document.RootElement.Deserialize(MissionJsonContext.Default.MissionFileDto)
                ?? throw new JsonException("The file contains null.");

            return dto.ToStops();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new MissionFormatException(sourceName, ex.Message, ex);
        }
    }
}
