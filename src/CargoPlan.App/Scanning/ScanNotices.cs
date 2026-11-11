using System.Globalization;
using CargoPlan.Core.Missions;

namespace CargoPlan.App.Scanning;

/// <summary>
/// The notice of each scan result (T10.3, D104), for the overlay's last line, where a move or a re-plan says what it did:
/// an undo or the next step clears it. Pure, so every text is tested without the app.
/// </summary>
internal static class ScanNotices
{
    public const string Running = "Scan running";

    public const string StillLoading = "Contract text still loading – scan again";

    public const string NoContract = "No contract in frame";

    public const string GameNotFound = "Star Citizen window not found";

    /// <summary>The text for <paramref name="report"/>; null for <see cref="ScanReportKind.Unavailable"/>, whose reason the scanner shows in its own line.</summary>
    /// <param name="mission">The scanned mission as the plan holds it now, for <see cref="ScanReportKind.Added"/>; null if it is gone (an undo since).</param>
    public static string? Text(ScanReport report, Mission? mission) => report.Kind switch
    {
        ScanReportKind.Added => Added(report.ScanNo, mission),
        ScanReportKind.Updated => string.Join(" · ", [
            Inv($"Updated #{report.ScanNo}"),
            report.QuantitiesChanged ? "quantities changed" : "confirmed",
            .. report.Cleared,
        ]),
        ScanReportKind.AlreadyInPlan => Inv($"Already in plan (#{report.ScanNo})"),
        ScanReportKind.Refused => Inv($"Scan #{report.ScanNo} not applied: {report.Reason}"),
        ScanReportKind.Incomplete => StillLoading,
        ScanReportKind.NoContract or ScanReportKind.NoContractInFrame => NoContract,
        ScanReportKind.GameNotFound => GameNotFound,
        ScanReportKind.CaptureFailed => $"Scan failed: screen capture refused ({report.Reason})",
        ScanReportKind.Unavailable => null,
        ScanReportKind.Busy => Running,
        _ => throw new ArgumentOutOfRangeException(nameof(report), report.Kind, null),
    };

    /// <summary>A failure the scan didn't expect (D82): the notice; the whole exception goes to cargoplan.log.</summary>
    public static string Failed(Exception error) => $"Scan failed: {error.Message}";

    /// <summary>
    /// "Scanned #3 · From Everus Harbor · 13 SCU Stims to High Course": one destination by name, several as a count (Martin);
    /// the commodity only when there is one; an offer asks for the rescan that settles the amounts (D102).
    /// </summary>
    private static string Added(int? no, Mission? mission)
    {
        var head = Inv($"Scanned #{no}");
        if (mission is null)
        {
            return head;
        }

        var items = mission.Stops.SelectMany(s => s.Items).ToList();
        var pickups = items.SelectMany(i => i.Pickup ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var commodities = items.Select(i => i.Commodity).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var amount = Inv($"{items.Sum(i => i.Scu)} SCU{(commodities.Count == 1 ? $" {commodities[0]}" : "")}");
        var to = mission.Stops.Count == 1 ? mission.Stops[0].Destination : Inv($"{mission.Stops.Count} destinations");
        return string.Join(" · ", [
            head,
            .. pickups.Count > 0 ? [$"From {string.Join(" / ", pickups)}"] : Array.Empty<string>(),
            $"{amount} to {to}",
            .. mission.Contract?.Scan?.QuantitiesEstimated == true ? ["rescan after accepting"] : Array.Empty<string>(),
        ]);
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
