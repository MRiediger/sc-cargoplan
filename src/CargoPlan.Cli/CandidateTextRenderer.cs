using System.Globalization;
using System.Text;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.Cli;

/// <summary>A ContractTracker candidate as plain text, for checking the parser against a real database (T5.3).</summary>
internal static class CandidateTextRenderer
{
    /// <param name="withRawText">Append the raw OCR text: what a bug report needs to become a test fixture.</param>
    public static string Render(ContractTrackerCandidate candidate, bool withRawText)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Contracts {string.Join(", ", candidate.ContractIds)}");
        text.AppendLine(candidate.CapturedAt is { } capturedAt
            ? string.Create(CultureInfo.InvariantCulture, $" · captured {capturedAt:yyyy-MM-dd HH:mm:ss}Z")
            : " · capture time unreadable");

        // "?" after an assumed size, as the legend will show it (T5.4).
        var assumed = candidate.CrateSizeAssumed ? "?" : "";
        foreach (var stop in candidate.Stops)
        {
            var items = stop.Items.Select(i => string.Create(CultureInfo.InvariantCulture,
                $"{i.Commodity} {i.Scu} SCU (max {(int)i.MaxCrateSize} SCU{assumed})"));
            text.AppendLine(CultureInfo.InvariantCulture, $"  {stop.Order}. {stop.Destination}: {string.Join(", ", items)}");
        }

        if (candidate.Stops.Count == 0)
        {
            text.AppendLine("  ⚠ no destination read");
        }

        if (candidate.DestinationsIncomplete)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  ⚠ {candidate.Stops.Count} of {candidate.ListedDestinationCount} destinations");
        }

        if (candidate.UsedFallbackRows)
        {
            text.AppendLine("  ⚠ from ContractTracker's destination rows (no delivery line in the text)");
        }

        if (withRawText)
        {
            text.AppendLine("--- raw text ---");
            text.AppendLine(candidate.RawText ?? "(none)");
            text.AppendLine("--- end of raw text ---");
        }

        return text.ToString();
    }
}
