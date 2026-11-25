using System.Diagnostics;
using CargoPlan.App.Scanning;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
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

    private readonly Action<RouteRun>? _finished;

    private readonly Func<DateTime> _clock;

    private readonly Action<bool>? _saveBlock;

    private SavedPlan? _saved;

    private Ship? _ship;

    // The stops as last worked out: the ticks since move the bar (Route.Advance).
    private RouteProgress? _progress;

    /// <param name="route">The route routes.json held at the start; null for none.</param>
    /// <param name="save">Writes the route to routes.json (<c>RoutesBook.SetRoute</c>), null when it ends.</param>
    /// <param name="hauling">Templates and positions to match a scan with; without them every accepted contract is an extra.</param>
    /// <param name="finished">Keeps a finished run (<c>RoutesBook.AddRun</c>, 6b); null in tests that keep none.</param>
    /// <param name="clock">The time in UTC; the system's by default.</param>
    /// <param name="blockOpen">Whether the block "Plan" shows its lines (settings.json, <c>routes.planBlock</c>, gate 6b).</param>
    /// <param name="saveBlock">Keeps that for the next start; null in tests that keep nothing.</param>
    public ActiveRouteViewModel(
        Route? route, Action<Route?> save, HaulingLoad hauling, Action<RouteRun>? finished = null, Func<DateTime>? clock = null,
        bool blockOpen = true, Action<bool>? saveBlock = null)
    {
        _save = save;
        _finished = finished;
        _clock = clock ?? (() => DateTime.UtcNow);
        _saveBlock = saveBlock;
        IsBlockOpen = blockOpen;
        (_templates, _positions) = hauling is HaulingLoad.Loaded { Data: var data } ? (data.Templates, data.Positions) : ([], Positions.Empty);
        Route = route;
        Rebuild();
    }

    /// <summary>The head's tool tip (gate 6b): what a click does and why the lines fold on their own.</summary>
    public const string BlockToolTip =
        "Click to show or hide the plan's lines; they fold when you leave the hub, since the bar above lists what to accept at each stop.";

    /// <summary>
    /// The block "Plan" shows its lines (gate 6b): open from Start plan, closed from the first departure, toggled by a
    /// click on its head; kept in settings.json for the next start.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBlockOpen { get; private set; }

    public string HeaderToolTip => BlockToolTip;

    /// <summary>A route was finished at its hub (6b): the overlay says what the round paid in what time.</summary>
    public event EventHandler<RouteRun>? Finished;

    /// <summary>
    /// The station bar (6b, K5): "Start · Everus Harbor · 5 contracts · free 5 slots", "Station 2/6 · HUR-L4 · unload 3
    /// (1 done) · free 2 slots", "Station 6/6 · Everus Harbor · unload 5"; null without a route or a ship.
    /// </summary>
    [ObservableProperty]
    public partial string? StationText { get; private set; }

    /// <summary>The stops with the distance of each leg, the long ones marked.</summary>
    [ObservableProperty]
    public partial string? StationToolTip { get; private set; }

    /// <summary>The ways back accepted at the bar's stop and the ones that collect there.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PlaceholderRowView> StationRows { get; private set; } = [];

    /// <summary>The bar stands at the hub at the end: "Finish plan" instead of ›.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishPlanCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextStationCommand))]
    public partial bool IsAtEnd { get; private set; }

    /// <summary>The bar stands past the start: ‹ goes back.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousStationCommand))]
    public partial bool CanGoBack { get; private set; }

    /// <summary>"Plan finished: Everus ring · 877,000 aUEC · 1 h 20 min" (6b).</summary>
    public static string FinishedText(RouteRun run) =>
        Invariant($"Plan finished: {run.Name} · {run.Reward:N0} aUEC · {RoleText.Duration(run.Time)}");

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
        SetBlock(true);
        AppLog.Write(LogContext, Invariant($"plan started: {route.Name}, {route.Placeholders.Count} placeholders, {route.Stations.Count} stations"));
        foreach (var a in adopted)
        {
            AppLog.Write(LogContext, a.PlaceholderId is { } id ? Invariant($"start: #{a.ScanNo} → placeholder {id}") : Invariant($"start: #{a.ScanNo} extra"));
        }

        return true;
    }

    /// <summary>
    /// The overlay's plan or ship changed (a scan, a tick, an undo, another ship): what is filled follows, and so does the
    /// bar, which moves on with the ticks (E(b), <see cref="Route.Advance"/>).
    /// </summary>
    public void Show(SavedPlan? saved, Ship ship)
    {
        var before = _progress;
        (_saved, _ship) = (saved, ship);
        Rebuild();
        if (Route is { } route && before is not null && _progress is { } after)
        {
            var moved = Route.Advance(route, before, after, _clock());
            if (moved != route)
            {
                SetRoute(moved);
            }
        }
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

        // E(c): a way back accepted at another station than planned says where it was planned.
        var at = _progress is { } progress && route.At > 0 && route.At < progress.Stops.Count - 1 ? progress.Stops[route.At].Name : null;
        RouteNote? note = outcome switch
        {
            FillOutcome.Filled { Placeholder: var p } => new RouteNote.Filled(p.Kind, after.FilledCount(keys), after.Placeholders.Count,
                p.AcceptAt is { } planned && at is not null && at != planned ? PlaceNames.Short(planned) : null),
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

    /// <summary>‹: one stop back, by hand.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousStation() => StepBy(-1);

    /// <summary>›: one stop on, by hand; leaving the hub starts the round's time (D126).</summary>
    [RelayCommand(CanExecute = nameof(CanGoOn))]
    private void NextStation() => StepBy(+1);

    private bool CanGoOn() => HasRoute && !IsAtEnd;

    /// <summary>
    /// "Finish plan" at the hub (6b, D126): the run is kept in routes.json, the route ends, the cargo stays (the player
    /// ticks the rest); the overlay says what the round paid in what time.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsAtEnd))]
    private void FinishPlan()
    {
        if (Route is not { } route || _progress is not { } progress || _ship is not { } ship)
        {
            return;
        }

        var run = Route.Finish(route, _saved, progress, _positions, ship, _templates, _clock());
        _finished?.Invoke(run);
        AppLog.Write(LogContext, Invariant($"plan finished: {run.Name}, {run.Reward} aUEC, {run.Time.TotalMinutes:0} min, {run.Filled}/{run.Total} filled, {run.Extras} extras, {run.Landings} landings"));
        SetRoute(null);
        Finished?.Invoke(this, run);
    }

    /// <summary>A click on the block's head: its lines show or hide (gate 6b).</summary>
    [RelayCommand]
    private void ToggleBlock() => SetBlock(!IsBlockOpen);

    private void SetBlock(bool open)
    {
        if (open != IsBlockOpen)
        {
            IsBlockOpen = open;
            _saveBlock?.Invoke(open);
            Rebuild();
        }
    }

    private void StepBy(int delta)
    {
        if (Route is { } route && _progress is { } progress)
        {
            SetRoute(route.Step(delta, progress.Stops.Count, _clock()));
        }
    }

    private void SetRoute(Route? route)
    {
        // The first departure folds the block: from here the bar lists what to accept (gate 6b).
        var departs = Route is { DepartedUtc: null } && route is { DepartedUtc: not null };
        Route = route;
        _save(route);
        Rebuild();
        if (departs)
        {
            SetBlock(false);
        }
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
            (StationText, StationToolTip, StationRows, IsAtEnd, CanGoBack, _progress) = (null, null, [], false, false, null);
            NextStationCommand.NotifyCanExecuteChanged();
            return;
        }

        var keys = Keys(_saved);
        var missions = _saved?.Missions.Where(m => m.Contract is not null).ToDictionary(m => m.Contract!.Scan.Key) ?? [];
        Header = Invariant($"{(IsBlockOpen ? "▾" : "▸")} Plan: {route.Name} · {route.FilledCount(keys)}/{route.Placeholders.Count} filled · Σ {route.OutboundReward:N0} out · +{route.BackReward:N0} back");
        Rows = [.. route.Outbound.Select(p => Row(p, keys, missions))];
        BackRows = [.. route.Back.Select(p => Row(p, keys, missions))];
        ExtraRows = [.. route.Extras.Where(missions.ContainsKey).Select(k => missions[k].Contract!).Select(c =>
            c.Reward is { } reward ? Invariant($"+ #{c.Scan.No} extra · {reward:N0} aUEC") : Invariant($"+ #{c.Scan.No} extra"))];
        BuildBar(route);
    }

    // The bar from the route's progress (D126); only its stop simulates the ways back, logged when the stop changes.
    private void BuildBar(Route route)
    {
        if (_ship is not { } ship)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var progress = RouteProgress.Of(route, _saved, ship, _positions);
        var last = progress.Stops.Count - 1;
        var at = Math.Clamp(route.At, 0, last);
        var stop = progress.Stops[at];
        if (_progress is not { } shown || shown.Stops.Count != progress.Stops.Count || shown.Stops[Math.Min(at, shown.Stops.Count - 1)].Name != stop.Name)
        {
            AppLog.Write(LogContext, Invariant($"station {PlaceNames.Short(stop.Name)} ({at}/{last}) in {watch.ElapsedMilliseconds} ms"));
        }

        _progress = progress;
        var unload = Invariant($"unload {stop.Lines}{(stop.LinesDone > 0 ? $" ({stop.LinesDone} done)" : "")}");
        StationText = at switch
        {
            0 => Invariant($"Start · {PlaceNames.Short(stop.Name)} · {stop.Held} contracts · free {stop.FreeSlots} slots"),
            _ when at == last => Invariant($"Station {at}/{last} · {PlaceNames.Short(stop.Name)} · {unload}"),
            _ => Invariant($"Station {at}/{last} · {PlaceNames.Short(stop.Name)} · {unload} · free {stop.FreeSlots} slots"),
        };
        StationToolTip = string.Join(Environment.NewLine, progress.Stops.Zip(progress.Stops.Skip(1)).Select(leg => Leg(route.System, leg.First.Name, leg.Second.Name)));
        var keys = Keys(_saved);
        var missions = _saved?.Missions.Where(m => m.Contract is not null).ToDictionary(m => m.Contract!.Scan.Key) ?? [];
        IEnumerable<PlaceholderRowView> rows = at == 0
            ? route.Outbound.Select(p => OutboundRow(p, keys, missions))
            : stop.Returns.Select(ReturnRow).Concat(stop.Collects.Select(p => new PlaceholderRowView(
                $"collect: {p.Title} (accepted at {PlaceNames.Short(p.AcceptAt!)})", null, null, Route.IsFilled(p, keys), p.Title)));
        StationRows = [.. rows];
        (IsAtEnd, CanGoBack) = (at == last, at > 0);
        NextStationCommand.NotifyCanExecuteChanged();
    }

    // "Riker → Orison 29.5 Gm (long)"; without positions the names only.
    private string Leg(StarSystem system, string from, string to) =>
        _positions.Find(system, from) is { } a && _positions.Find(system, to) is { } b && Positions.DistanceGm(a, b) is { } gm
            ? Invariant($"{PlaceNames.Short(from)} → {PlaceNames.Short(to)} {gm:0.0} Gm{(gm > Loops.LongJumpGm ? " (long)" : "")}")
            : $"{PlaceNames.Short(from)} → {PlaceNames.Short(to)}";

    // At the hub at the start (gate 6b): "accept: Bulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · HUR-L1, HUR-L2".
    private static PlaceholderRowView OutboundRow(Placeholder p, IReadOnlySet<string> keys, Dictionary<string, Mission> missions)
    {
        var line = string.Join(" · ", [$"accept: {RoleText.KindWords[p.Kind]}", p.Title, RoleText.Reward(p.Reward, null), string.Join(", ", p.Destinations.Select(PlaceNames.Short))]);
        return Route.IsFilled(p, keys)
            ? new(Invariant($"✓ #{missions[p.FilledBy!].Contract!.Scan.No} · {line}"), null, null, true, p.Title)
            : new(line, null, null, false, p.Title);
    }

    // "accept: Junior Rank - Small Cargo Haul · 60,750 aUEC · fits"; too big in the notice colour; "✓ #7 · accept: …" once filled.
    private static PlaceholderRowView ReturnRow(ReturnAt way)
    {
        var p = way.Placeholder;
        var line = $"accept: {p.Title} · {RoleText.Reward(p.Reward, null)}";
        var collect = p.CollectAt is { Count: > 0 } more ? $" · collect at {string.Join(", ", more.Select(PlaceNames.Short))}" : null;
        return way.Fit switch
        {
            ReturnFit.Filled => new(Invariant($"✓ #{way.ScanNo} · {line}"), null, collect, true, p.Title),
            ReturnFit.Fits => new($"{line} · fits", null, collect, false, p.Title),
            ReturnFit.TooBig => new(line, Invariant($" · too big ({p.MaxScu} SCU in {(int)p.Container}s)"), collect, false, p.Title),
            _ => new(line, null, collect, false, p.Title),
        };
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
