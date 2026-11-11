using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.Core.Plans;

/// <summary>
/// Reads and writes plan files (docs/plan-format.md): missions plus the plan with moves and ticks. Streams only;
/// where the file lives and how it is replaced safely is the caller's business (the overlay's PlanStore).
/// </summary>
public static class PlanFile
{
    /// <summary>
    /// The version the writer writes: missions with ids, crates with missions (D42), items (D68), turned or not (D71),
    /// off the ship or not (K2.2) and loaded or not (K6.4), items added by hand to a ContractTracker mission (K6.5),
    /// captures gone from ContractTracker or detached (D81), the row axis (D88), the grouping and section gap (D89), the watermark. An app from before D88
    /// refuses it, so it never lays a crosswise plan out lengthwise.
    /// </summary>
    public const int SupportedSchemaVersion = 9;

    /// <summary>
    /// Captures gone or not, no row axis: every plan lengthwise (D81); still read so, never written. The oldest version
    /// read: v0.1.0 wrote it, the versions before were this project's own history, never a player's (F4, T10.2b1).
    /// </summary>
    public const int NoFillRowsSchemaVersion = 8;

    private static readonly int[] ReadableSchemaVersions = [NoFillRowsSchemaVersion, SupportedSchemaVersion];

    /// <summary>
    /// Reads a plan file for <paramref name="ship"/>, schema 8 or 9. The stream is left open. A plan for another ship
    /// loads with its own ship id and without the grid checks; the caller plans it anew (D59). So does one with a crate
    /// outside today's rack, read under <see cref="CapReason.RacksChanged"/> (K3.2).
    /// </summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="PlanFormatException">
    /// The content is broken, of another schema version, uses racks the ship doesn't have, or doesn't match its missions.
    /// </exception>
    public static SavedPlan Read(Stream stream, string sourceName, Ship ship)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            SchemaVersion.Check(root, ReadableSchemaVersions);
            return (root.Deserialize(PlanJsonContext.Default.PlanFileDto) ?? throw Null()).ToSavedPlan(ship);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or PlanException)
        {
            throw new PlanFormatException(sourceName, ex.Message, ex);
        }
    }

    /// <summary>
    /// The plan's ship id alone, before choosing the ship to read it for (T7.2: without a ship in settings.json the start
    /// takes the plan's). Null if the file has none or isn't JSON; <see cref="Read"/> names that problem. The stream is
    /// left open.
    /// </summary>
    public static string? ReadShipId(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var document = JsonDocument.Parse(stream);
            return document.RootElement is { ValueKind: JsonValueKind.Object } root
                && root.TryGetProperty("shipId", out var shipId) && shipId.ValueKind == JsonValueKind.String
                ? shipId.GetString()
                : null;
        }
        catch (JsonException)
        {
            // Not this method's to report: the real read follows and says what is wrong with the file.
            return null;
        }
    }

    private static JsonException Null() => new("The file contains null.");

    /// <summary>Writes the plan as indented JSON. The stream is left open.</summary>
    public static void Write(Stream stream, SavedPlan saved)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonSerializer.Serialize(stream, PlanFileDto.FromSavedPlan(saved), PlanJsonContext.Default.PlanFileDto);
    }
}
