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
    /// <summary>The version the writer writes: crates with positions (D24).</summary>
    public const int SupportedSchemaVersion = 2;

    /// <summary>Placements without positions; still read and laid out on reading, never written.</summary>
    public const int LegacySchemaVersion = 1;

    private static readonly int[] ReadableSchemaVersions = [LegacySchemaVersion, SupportedSchemaVersion];

    /// <summary>Reads a plan file for <paramref name="ship"/>, schema 1 or 2. The stream is left open.</summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="PlanFormatException">
    /// The content is broken, is for another ship, uses racks the ship doesn't have, has a crate outside its rack,
    /// doesn't match its missions, or is a schema 1 plan with more cargo in a rack than its grid holds.
    /// </exception>
    public static SavedPlan Read(Stream stream, string sourceName, Ship ship)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            return SchemaVersion.Check(root, ReadableSchemaVersions) == LegacySchemaVersion
                ? (root.Deserialize(PlanJsonContext.Default.PlanFileV1Dto) ?? throw new JsonException("The file contains null.")).ToSavedPlan(ship)
                : (root.Deserialize(PlanJsonContext.Default.PlanFileDto) ?? throw new JsonException("The file contains null.")).ToSavedPlan(ship);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or PlanException)
        {
            throw new PlanFormatException(sourceName, ex.Message, ex);
        }
    }

    /// <summary>Writes the plan as indented JSON. The stream is left open.</summary>
    public static void Write(Stream stream, SavedPlan saved)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonSerializer.Serialize(stream, PlanFileDto.FromSavedPlan(saved), PlanJsonContext.Default.PlanFileDto);
    }
}
