using System.Text.Json;
using CargoPlan.Core.Json;

namespace CargoPlan.Core.Missions;

/// <summary>
/// Reads mission files (see docs/mission-format.md). The stream overload is the core:
/// CLI reads files, tests read embedded resources, the editor imports files, all through one code path.
/// Returns the missions as entered and the file's fill order, if any (D72); <see cref="MissionStopExtensions.ToStops"/>
/// expands the missions for the planner (D23).
/// </summary>
public static class MissionFileReader
{
    /// <summary>The version the writer writes.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Derived crate mixes, but no fill order (D23); still read, never written.</summary>
    public const int NoFillOrderSchemaVersion = 2;

    /// <summary>One crate size per item; still read, never written.</summary>
    public const int LegacySchemaVersion = 1;

    private static readonly int[] SupportedSchemaVersions = [LegacySchemaVersion, NoFillOrderSchemaVersion, CurrentSchemaVersion];

    /// <summary>
    /// The mission file inside a plan file of schema 1 and 2: those were written before schema 3, and a plan has one fill
    /// order, its own.
    /// </summary>
    internal static readonly int[] EmbeddedSchemaVersions = [LegacySchemaVersion, NoFillOrderSchemaVersion];

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

    /// <summary>The schema versions in <paramref name="versions"/>; also used for the missions inside a plan file.</summary>
    /// <exception cref="JsonException">Not the expected format.</exception>
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    internal static MissionFile Parse(JsonElement root, IReadOnlyList<int> versions) =>
        SchemaVersion.Check(root, versions) == LegacySchemaVersion
            ? new((root.Deserialize(MissionJsonContext.Default.MissionFileV1Dto) ?? throw new JsonException("The file contains null."))
                .ToMissionStops(), null)
            : (root.Deserialize(MissionJsonContext.Default.MissionFileDto) ?? throw new JsonException("The file contains null."))
                .ToMissionFile();
}
