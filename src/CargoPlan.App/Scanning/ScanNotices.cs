using System.Globalization;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Routing;

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
        ScanReportKind.Added => Added(report.ScanNo, mission) + RouteText(report.Route),
        ScanReportKind.Updated => string.Join(" · ", [
            Inv($"Updated #{report.ScanNo}"),
            report.QuantitiesChanged ? "quantities changed" : "confirmed",
            .. report.Cleared,
        ]) + RouteText(report.Route),
        // "cargo", not "plan", since the route is the plan (U4, T13.6).
        ScanReportKind.AlreadyInPlan => Inv($"Already in cargo (#{report.ScanNo})") + RouteText(report.Route),
        ScanReportKind.Refused => Inv($"Scan #{report.ScanNo} not applied: {report.Reason}"),
        ScanReportKind.Incomplete => StillLoading,
        ScanReportKind.NoContract or ScanReportKind.NoContractInFrame => NoContract,
        ScanReportKind.GameNotFound => GameNotFound,
        ScanReportKind.CaptureFailed => $"Scan failed: screen capture refused ({report.Reason})",
        ScanReportKind.Unavailable => null,
        ScanReportKind.Busy => Running,
        // E1 of the T13.2 approval: the Routes tab is labelled Plan, so these say "cargo" for the loading plan.
        ScanReportKind.Observed => $"Observed (Plan tab): {(report.Observed is { } key ? Template(key) : "offer")}{FitText(report.Fit)}{RouteText(report.Route)} – cargo unchanged",
        ScanReportKind.NotObserved => $"Not observed (Plan tab): {report.Reason} – cargo unchanged",
        _ => throw new ArgumentOutOfRangeException(nameof(report), report.Kind, null),
    };

    /// <summary>" · fits as bulk", " · too big for MISC Hull B (432 SCU in 16s)": the check before accepting (T13.3b, D123).</summary>
    private static string FitText(RecipeFit? fit) => fit switch
    {
        RecipeFit.FitsAs { Kind: var kind } => $" · fits as {kind.ToString().ToLowerInvariant()}",
        RecipeFit.TooBig { Scu: var scu, Crate: var crate, ShipName: var ship } => Inv($" · too big for {ship} ({scu} SCU in {(int)crate}s)"),
        _ => "",
    };

    /// <summary>" · plan: bulk ✓ 2/10", " · plan ✓", " · not in plan (extra)", " · plan: bulk": the route's part (T13.6, D125).</summary>
    private static string RouteText(RouteNote? route) => route switch
    {
        RouteNote.Filled { Kind: var kind, Count: var count, Total: var total } => Inv($" · plan: {Word(kind)} ✓ {count}/{total}"),
        RouteNote.AlreadyFilled => " · plan ✓",
        RouteNote.Extra => " · not in plan (extra)",
        RouteNote.Open { Kind: var kind } => $" · plan: {Word(kind)}",
        _ => "",
    };

    private static string Word(RecipeKind kind) => kind == RecipeKind.Return ? "back" : kind.ToString().ToLowerInvariant();

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
            .. mission.Contract?.Scan.QuantitiesEstimated == true ? ["rescan after accepting"] : Array.Empty<string>(),
        ]);
    }

    /// <summary>"Covalex Rookie Direct Extra Small · 41,750 aUEC": the title's words as the game writes them, the reward.</summary>
    private static string Template(ObservationKey key) => string.Join(' ', [
        key.Contractor == HaulContractor.RedWind ? "Red Wind" : "Covalex",
        key.Rank.ToString(),
        .. key.Direct == true ? ["Direct"] : Array.Empty<string>(),
        key.Size == HaulSize.Xs ? "Extra Small" : key.Size.ToString(),
        Inv($"· {key.Reward:N0} aUEC"),
    ]);

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
