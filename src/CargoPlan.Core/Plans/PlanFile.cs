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
    /// off the ship or not (K2.2) and loaded or not (K6.4), the watermark. An app from before K6.4 refuses it, so it never
    /// plans loaded cargo anew.
    /// </summary>
    public const int SupportedSchemaVersion = 7;

    /// <summary>Crates off the ship or not, none loaded (K2.2); still read, nothing loaded, never written.</summary>
    public const int NotLoadedSchemaVersion = 6;

    /// <summary>Crates turned or not, every crate in a rack (D71); still read, never written.</summary>
    public const int NoOffShipSchemaVersion = 5;

    /// <summary>Crates with items, none turned (D68); still read, every crate unturned, never written.</summary>
    public const int UnrotatedSchemaVersion = 4;

    /// <summary>Crates with missions but without items (D42); still read, items assigned on reading (D68), never written.</summary>
    public const int ItemlessSchemaVersion = 3;

    /// <summary>Placements without positions; still read and laid out on reading, never written.</summary>
    public const int LegacySchemaVersion = 1;

    /// <summary>Crates with positions, no missions (D24); still read, one mission per stop, never written.</summary>
    public const int PreviousSchemaVersion = 2;

    private static readonly int[] ReadableSchemaVersions = [LegacySchemaVersion, PreviousSchemaVersion, ItemlessSchemaVersion, UnrotatedSchemaVersion, NoOffShipSchemaVersion, NotLoadedSchemaVersion, SupportedSchemaVersion];

    /// <summary>
    /// Reads a plan file for <paramref name="ship"/>, schema 1 to 7. The stream is left open. A schema 2 to 7 plan for
    /// another ship loads with its own ship id and without the grid checks; the caller plans it anew (D59). So does one
    /// with a crate outside today's rack, read under <see cref="CapReason.RacksChanged"/> (K3.2).
    /// </summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="PlanFormatException">
    /// The content is broken, uses racks the ship doesn't have, doesn't match its missions, or is a schema 1 plan for
    /// another ship (laying it out needs its grid) or with more cargo in a rack than its grid holds.
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
