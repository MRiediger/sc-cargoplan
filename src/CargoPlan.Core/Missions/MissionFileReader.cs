using System.Text.Json;
using CargoPlan.Core.Json;

namespace CargoPlan.Core.Missions;

/// <summary>
/// Reads mission files (see docs/mission-format.md). The stream overload is the core:
/// CLI reads files, tests read embedded resources, the editor imports files, all through one code path.
/// Returns the missions as entered and the file's fill order and row axis, if any (D72, D88); <see cref="MissionStopExtensions.ToStops"/>
/// expands the missions for the planner (D23).
/// </summary>
public static class MissionFileReader
{
    /// <summary>The version the writer writes.</summary>
    public const int CurrentSchemaVersion = 4;

    /// <summary>
    /// A fill order, but no row axis (D72); still read, never written. The oldest readable: v0.1.0 wrote it, schema 1 and 2
    /// were development history (T10.4a, review F5).
    /// </summary>
    public const int NoFillRowsSchemaVersion = 3;

    private static readonly int[] SupportedSchemaVersions = [NoFillRowsSchemaVersion, CurrentSchemaVersion];

    /// <summary>Reads a mission file from disk.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="MissionFormatException">The file is broken.</exception>
    public static MissionFile Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Read(stream, path);
    }

    /// <summary>Reads a mission file from a stream. The stream is left open.</summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="MissionFormatException">The content is broken.</exception>
    public static MissionFile Read(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            return Parse(document.RootElement, SupportedSchemaVersions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new MissionFormatException(sourceName, ex.Message, ex);
        }
    }

    /// <summary>The schema versions in <paramref name="versions"/>.</summary>
    /// <exception cref="JsonException">Not the expected format.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    private static MissionFile Parse(JsonElement root, IReadOnlyList<int> versions)
    {
        SchemaVersion.Check(root, versions);
        return (root.Deserialize(MissionJsonContext.Default.MissionFileDto) ?? throw new JsonException("The file contains null.")).ToMissionFile();
    }
}
