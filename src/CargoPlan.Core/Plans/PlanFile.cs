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
    /// <summary>The version the writer writes: missions with ids, crates with missions (D42) and items (D68), the watermark.</summary>
    public const int SupportedSchemaVersion = 4;

    /// <summary>Crates with missions but without items (D42); still read, items assigned on reading (D68), never written.</summary>
    public const int ItemlessSchemaVersion = 3;

    /// <summary>Placements without positions; still read and laid out on reading, never written.</summary>
    public const int LegacySchemaVersion = 1;

    /// <summary>Crates with positions, no missions (D24); still read, one mission per stop, never written.</summary>
    public const int PreviousSchemaVersion = 2;

    private static readonly int[] ReadableSchemaVersions = [LegacySchemaVersion, PreviousSchemaVersion, ItemlessSchemaVersion, SupportedSchemaVersion];

    /// <summary>
    /// Reads a plan file for <paramref name="ship"/>, schema 1 to 4. The stream is left open. A schema 2 to 4 plan for
    /// another ship loads with its own ship id and without the grid checks; the caller plans it anew (D59).
    /// </summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="PlanFormatException">
    /// The content is broken, uses racks the ship doesn't have, has a crate outside its rack, doesn't match its
    /// missions, or is a schema 1 plan for another ship (laying it out needs its grid) or with more cargo in a rack than
    /// its grid holds.
    /// </exception>
    public static SavedPlan Read(Stream stream, string sourceName, Ship ship)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            return SchemaVersion.Check(root, ReadableSchemaVersions) switch
            {
                LegacySchemaVersion => (root.Deserialize(PlanJsonContext.Default.PlanFileV1Dto) ?? throw Null()).ToSavedPlan(ship),
                PreviousSchemaVersion => (root.Deserialize(PlanJsonContext.Default.PlanFileV2Dto) ?? throw Null()).ToSavedPlan(ship),
                _ => (root.Deserialize(PlanJsonContext.Default.PlanFileDto) ?? throw Null()).ToSavedPlan(ship),
            };
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
