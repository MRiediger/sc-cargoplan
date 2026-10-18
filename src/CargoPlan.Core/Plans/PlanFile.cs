using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Plans;

/// <summary>
/// Reads and writes plan files (docs/plan-format.md): missions plus the plan with moves and ticks. Streams only;
/// where the file lives and how it is replaced safely is the caller's business (the overlay's PlanStore).
/// </summary>
public static class PlanFile
{
    public const int SupportedSchemaVersion = 1;

    /// <summary>Reads a plan file for <paramref name="ship"/>. The stream is left open.</summary>
    /// <param name="sourceName">Shown in error messages, e.g. the file path.</param>
    /// <exception cref="PlanFormatException">
    /// The content is broken, is for another ship, uses racks the ship doesn't have, or doesn't match its missions.
    /// </exception>
    public static SavedPlan Read(Stream stream, string sourceName, Ship ship)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            using var document = JsonDocument.Parse(stream);
            SchemaVersion.Check(document.RootElement, SupportedSchemaVersion);

            var dto = document.RootElement.Deserialize(PlanJsonContext.Default.PlanFileDto)
                ?? throw new JsonException("The file contains null.");

            return dto.ToSavedPlan(ship);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
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
