using System.Text.Json;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>Writes mission files (docs/mission-format.md), the counterpart of <see cref="MissionFileReader"/>, for the editor's export.</summary>
public static class MissionFileWriter
{
    /// <summary>
    /// Writes the stops in route order and the fill order as indented JSON, always in the current schema version (D72).
    /// The stream is left open.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The file couldn't be read back (no stops, a stop without items, a destination twice); nothing is written then.
    /// </exception>
    public static void Write(Stream stream, IReadOnlyList<MissionStop> stops, FillOrder fillOrder)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var dto = MissionFileDto.FromMissionStops(stops, fillOrder);

        // The reader's rules decide what may be written: a file this app can't import again is no export.
        dto.ToMissionFile();

        JsonSerializer.Serialize(stream, dto, MissionJsonContext.Default.MissionFileDto);
    }
}
