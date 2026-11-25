using System.Diagnostics;
using CargoPlan.App.Scanning;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Routing;
using CargoPlan.Import.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static System.FormattableString;

namespace CargoPlan.App.Routing;

/// <summary>
/// One placeholder in the Cargo tab's block "Plan" (T13.6): filled ones in normal type, open ones grey. Gate 6a: role,
/// title, reward and places (or the way back's parts) in the line, which wraps; commodities and bands in the tool tip.
/// </summary>
/// <param name="Text">"✓ #3 · Bulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · HUR-L1, HUR-L2"; a way back without places.</param>
/// <param name="Accept">" · accept at HUR-L1" for a way back, shown in the notice colour; null otherwise.</param>
/// <param name="Collect">" · collect at HUR-L4, HUR-L3" for a way back with several pickups; null otherwise.</param>
public sealed record PlaceholderRowView(string Text, string? Accept, string? Collect, bool Filled, string ToolTip);

/// <summary>
/// The route in the Cargo tab (T13.6, U9, D125): the block "Plan" above the legend, a line per placeholder; an accepted
/// scan fills one. The route lives in routes.json, saved through <c>RoutesBook</c> (one writer); what is filled is read
/// from the overlay's plan, never stored here, so an undo opens a placeholder again. Without a route nothing shows and
/// every scan is as before.
/// </summary>
public sealed partial class ActiveRouteViewModel : ObservableObject
{
    /// <summary>A second "Start plan" (U4: the route is "plan", the loading plan "cargo").</summary>
    public const string ReplaceQuestion = "Replace the plan? Cargo stays as it is.";

    private const string LogContext = "Routes";

    private readonly Action<Route?> _save;

    private readonly IReadOnlyList<HaulTemplate> _templates;

    private readonly Positions _positions;

    private SavedPlan? _saved;

    /// <param name="route">The route routes.json held at the start; null for none.</param>
    /// <param name="save">Writes the route to routes.json (<c>RoutesBook.SetRoute</c>), null when it ends.</param>
    /// <param name="hauling">Templates and positions to match a scan with; without them every accepted contract is an extra.</param>
    public ActiveRouteViewModel(Route? route, Action<Route?> save, HaulingLoad hauling)
    {
        _save = save;
        (_templates, _positions) = hauling is HaulingLoad.Loaded { Data: var data } ? (data.Templates, data.Positions) : ([], Positions.Empty);
        Route = route;
        Rebuild();
    }

    /// <summary>The route the player flies; null while none is started.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoute))]
    public partial Route? Route { get; private set; }

    public bool HasRoute => Route is not null;

    /// <summary>"Plan: Everus ring · 2/10 filled · Σ 594,500 out · +282,500 back"; null without a route.</summary>
    [ObservableProperty]
    public partial string? Header { get; private set; }

    /// <summary>What leaves the hub, in the route's order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PlaceholderRowView> Rows { get; private set; } = [];

    /// <summary>The ways back, under "Back".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBack))]
    public partial IReadOnlyList<PlaceholderRowView> BackRows { get; private set; } = [];

    /// <summary>The line "Back" shows over the ways back, not without any.</summary>
    public bool HasBack => BackRows.Count > 0;

    /// <summary>"+ #4 extra · 160,250 aUEC": accepted contracts no placeholder took, as the plan holds them.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> ExtraRows { get; private set; } = [];

    /// <summary>Asks before a second route replaces the first; the window sets the dialog (ConfirmReplan's pattern), tests a stub.</summary>
    public Func<string, bool> ConfirmReplace { get; set; } = _ => false;

    /// <summary>
    /// Starts <paramref name="route"/>; one already started is replaced only when the player says yes. The cargo stays,
    /// and its accepted contracts fill their placeholders or are extras (<see cref="Route.Adopt"/>, gate 6a), one log line each.
    /// </summary>
    public bool Start(Route route)
    {
        if (Route is not null && !ConfirmReplace(ReplaceQuestion))
        {
            return false;
        }

        var (started, adopted) = Route.Adopt(route, _saved, _templates, _positions);
        SetRoute(started);
        AppLog.Write(LogContext, Invariant($"plan started: {route.Name}, {route.Placeholders.Count} placeholders, {route.Stations.Count} stations"));
        foreach (var a in adopted)
        {
            AppLog.Write(LogContext, a.PlaceholderId is { } id ? Invariant($"start: #{a.ScanNo} → placeholder {id}") : Invariant($"start: #{a.ScanNo} extra"));
        }

        return true;
    }

    /// <summary>The overlay's plan changed (a scan, a tick, an undo): what is filled follows.</summary>
    public void Show(SavedPlan? saved)
    {
        _saved = saved;
        Rebuild();
    }

    /// <summary>
    /// What the scan of <paramref name="scanned"/> does to the route (U3, D125), after the plan took it: an accepted
    /// contract fills the first open placeholder of its template or is an extra; an offer of an open placeholder's
    /// template is told (E(a)). Null without a route or with nothing to say.
    /// </summary>
    /// <param name="scan">The scan's identity as read (the capture's): an offer is an estimate even after the accepted contract.</param>
    /// <param name="saved">The plan after the scan.</param>
    public RouteNote? Fill(ScannedContract scanned, ScanIdentity scan, ScanChange change, SavedPlan? saved)
    {
        if (Route is not { } route)
        {
            return null;
        }

        _saved = saved;
        if (scan.QuantitiesEstimated)
        {
            return scanned.View == ContractView.Offer ? OfferNote(scanned) : null;
        }

        var watch = Stopwatch.StartNew();
        var keys = Keys(saved);
        var (after, outcome) = Route.Fill(route, Match(scanned), scan, change, keys);
        if (!ReferenceEquals(after, route))
        {
            SetRoute(after);
        }

        RouteNote? note = outcome switch
        {
            FillOutcome.Filled { Placeholder: var p } => new RouteNote.Filled(p.Kind, after.FilledCount(keys), after.Placeholders.Count),
            FillOutcome.AlreadyFilled => new RouteNote.AlreadyFilled(),
            FillOutcome.Extra => new RouteNote.Extra(),
            _ => null,
        };
        AppLog.Write(LogContext, Invariant($"fill #{scan.No}: {Describe(outcome)} fillMs={watch.ElapsedMilliseconds}"));
        return note;
    }

    /// <summary>" · plan: bulk" for an offer of an open placeholder's template (E(a) of T13.6); null otherwise.</summary>
    public RouteNote? OfferNote(ScannedContract scanned) =>
        Route?.OpenFor(Match(scanned), Keys(_saved)) is { } open ? new RouteNote.Open(open.Kind) : null;

    /// <summary>Ends the route: the block goes, routes.json loses it, the cargo stays (U9.1). No run is kept.</summary>
    [RelayCommand]
    private void End()
    {
        if (Route is { } route)
        {
            AppLog.Write(LogContext, $"plan ended: {route.Name}");
            SetRoute(null);
        }
    }

    private void SetRoute(Route? route)
    {
        Route = route;
        _save(route);
        Rebuild();
    }

    // The scan's template (D122), as RoutesViewModel.FitOf matches it; null where the scan is no observation.
    private MatchResult? Match(ScannedContract scanned) =>
        scanned.ToObservation(DateTime.UtcNow) is { } observed ? TemplateMatch.Find(Observation.Of(observed), _templates, _positions) : null;

    private static HashSet<string> Keys(SavedPlan? saved) =>
        saved is null ? [] : [.. saved.Missions.Select(m => m.Contract?.Scan.Key).OfType<string>()];

    private void Rebuild()
    {
        if (Route is not { } route)
        {
            (Header, Rows, BackRows, ExtraRows) = (null, [], [], []);
            return;
        }

        var keys = Keys(_saved);
        var missions = _saved?.Missions.Where(m => m.Contract is not null).ToDictionary(m => m.Contract!.Scan.Key) ?? [];
        Header = Invariant($"Plan: {route.Name} · {route.FilledCount(keys)}/{route.Placeholders.Count} filled · Σ {route.OutboundReward:N0} out · +{route.BackReward:N0} back");
        Rows = [.. route.Outbound.Select(p => Row(p, keys, missions))];
        BackRows = [.. route.Back.Select(p => Row(p, keys, missions))];
        ExtraRows = [.. route.Extras.Where(missions.ContainsKey).Select(k => missions[k].Contract!).Select(c =>
            c.Reward is { } reward ? Invariant($"+ #{c.Scan.No} extra · {reward:N0} aUEC") : Invariant($"+ #{c.Scan.No} extra"))];
    }

    private static PlaceholderRowView Row(Placeholder p, IReadOnlySet<string> keys, Dictionary<string, Mission> missions)
    {
        // Gate 6a (K2): a way back names where to accept and collect instead of its place, the hub.
        string[] parts =
        [
            RoleText.KindWords[p.Kind], p.Title, RoleText.Reward(p.Reward, null),
            .. p.AcceptAt is null ? [string.Join(", ", p.Destinations.Select(PlaceNames.Short))] : Array.Empty<string>(),
        ];
        var line = string.Join(" · ", parts);
        var accept = p.AcceptAt is { } at ? $" · accept at {PlaceNames.Short(at)}" : null;
        var collect = p.CollectAt is { Count: > 0 } more ? $" · collect at {string.Join(", ", more.Select(PlaceNames.Short))}" : null;
        var filled = Route.IsFilled(p, keys);
        var tip = string.Join("\n", [
            p.Title,
            .. p.Legs.Select(l => Invariant($"{l.Commodity} {l.MinScu}–{l.MaxScu} SCU in {(int)l.Container}s")),
            Invariant($"{p.MinScu}–{p.MaxScu} SCU in {(int)p.Container}s in all"),
            $"to {string.Join(", ", p.Destinations)}",
        ]);
        return new(filled ? Invariant($"✓ #{missions[p.FilledBy!].Contract!.Scan.No} · {line}") : line, accept, collect, filled, tip);
    }

    private static string Describe(FillOutcome outcome) => outcome switch
    {
        FillOutcome.Filled { Placeholder: var p } => Invariant($"placeholder {p.Id} ({RoleText.KindWords[p.Kind]}, {p.Title})"),
        FillOutcome.AlreadyFilled { Placeholder: { } p } => Invariant($"already placeholder {p.Id}"),
        FillOutcome.AlreadyFilled => "already an extra",
        FillOutcome.Extra => "extra",
        _ => "not accepted",
    };
}
