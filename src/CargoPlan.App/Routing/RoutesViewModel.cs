using System.Globalization;
using System.Text;
using CargoPlan.App.Settings;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static System.FormattableString;

namespace CargoPlan.App.Routing;

/// <summary>
/// One row of the port matrix: a pickup, as shown (<see cref="PlaceNames.Short"/>: "HUR-L1") and in full, and one cell per
/// drop-off column, null where nothing goes.
/// </summary>
public sealed record PortRowView(string Pickup, string Label, IReadOnlyList<PortCellView?> Cells);

/// <summary>A cell as the grid shows it: "13 · 1.1M", and the most SCU and the templates in its tool tip.</summary>
public sealed record PortCellView(string Text, string ToolTip);

/// <summary>
/// One loop as the list shows it (T13.4): a title, the aUEC figures, the structure and status, the stations and
/// contracts in the tool tip. Grey when it needs scans before it has figures.
/// </summary>
/// <param name="Hub">The Stanton hub whose recipe a click shows; null where there is none (system-wide, Pyro).</param>
/// <param name="Figures">"957k/loop · ~578k/h · 160k/landing"; null without contracts.</param>
/// <param name="Chosen">The row clicked last: "Start plan" starts it (K4 of T13.6).</param>
public sealed record LoopRowView(LoopCandidate Loop, string Title, string? Figures, string Details, string ToolTip, bool Grey, string? Hub, bool Chosen = false)
{
    public bool CanOpen => Hub is not null;
}

/// <summary>"Start plan" (T13.6): the loop to make a route of, its row's title as the route's name, what the scans saw.</summary>
public sealed record PlanStart(LoopCandidate Loop, string Name, ObservedEdges Edges);

/// <summary>
/// The Plan tab (the Routes tab, D120; T13.2): the port matrix of the hauling templates (design §8.5) under a filter whose
/// parts cycle on a click, with what the scans saw (T13.3, <see cref="ObservedEdges"/>, computed by the caller). The matrix
/// is Core's (<see cref="PortMatrix"/>); this formats it. Rank and faction go to settings.json (routes.rank,
/// routes.faction), the other filters live for the session. The loops (T13.4, D124) are computed off the UI thread by
/// whoever listens to <see cref="LoopInputsChanged"/> and come back through <see cref="ShowLoops"/>.
/// </summary>
public sealed partial class RoutesViewModel : ObservableObject, IMeasuredSummary
{
    private const int MaxToolTipTemplates = 15;

    private const string ObservedMark = "● ";

    // A template whose pickup or drop-off is the board's hub (D121), in a cell's tool tip; a house for the hub.
    private const string HubMark = "⌂ ";

    private static readonly (string Label, HaulRank Value)[] Ranks =
        [.. Enum.GetValues<HaulRank>().Select(r => ($"Rank ≤ {r}", r))];

    private static readonly (string Label, DirectFilter Value)[] Directs =
        [("Direct: any", DirectFilter.All), ("Direct only", DirectFilter.Only), ("No direct", DirectFilter.Without)];

    private static readonly (string Label, HaulContractor? Value)[] Contractors =
        [("Any contractor", null), ("Covalex", HaulContractor.Covalex), ("Red Wind", HaulContractor.RedWind)];

    private static readonly (string Label, HaulFaction? Value)[] Factions =
        [("Any faction", null), ("Headhunters", HaulFaction.Hh), ("CFP", HaulFaction.Cfp)];

    private static readonly (string Label, HaulMissionType? Value)[] Areas =
        [("All areas", null), ("Local", HaulMissionType.Local), ("Planetary", HaulMissionType.Planetary), ("Stellar", HaulMissionType.Stellar), ("Interstellar", HaulMissionType.Interstellar)];

    private static readonly (string Label, StarSystem? Value)[] Systems =
        [("Stanton", StarSystem.Stanton), ("Pyro", StarSystem.Pyro), ("Interstellar", null)];

    // The view of the Plan tab (T13.3b, T13.4, T13.7): the port matrix, the recipe of a hub for the overlay's ship, the
    // loops, the map of the matrix's cells.
    private static readonly string[] Views = ["Matrix", "Recipe", "Loops", "Map"];

    private const int MapView = 3;

    private static readonly (string Label, LoopSort Value)[] Sorts =
        [("Sort: ~aUEC/h", LoopSort.PerHour), ("Sort: aUEC/loop", LoopSort.PerLoop), ("Sort: aUEC/landing", LoopSort.PerLanding)];

    // The four Stanton hubs in board order (Stanton1–4): the recipe's start (routes.hub).
    private static readonly (string AreaKey, string Name)[] Hubs = [.. BoardHubs.ByArea.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => (h.Key, h.Value))];

    private readonly IReadOnlyList<HaulTemplate> _templates;

    private readonly Positions _positions;

    private readonly Action<RoutesSettings>? _save;

    private ObservedEdges _observed;

    private RoutesSettings _settings;

    private int _rank;

    private int _direct;

    private int _contractor;

    private int _faction;

    private int _area;

    private int _system;

    private int _view;

    // routes.hub: where the player is (the hub button, saved).
    private int _hub;

    // The hub the recipe shows when a loop's row chose it (T13.4, D124): for the session, never saved; null = _hub.
    private int? _shownHub;

    private int _sort;

    private Ship? _ship;

    // The loops of the inputs below, as computed; empty until the first result.
    private IReadOnlyList<LoopCandidate> _loops = [];

    private bool _loopsComputed;

    // The loop row clicked last (K4 of T13.6), for the session: its kind, start and title, since the loops are computed anew.
    private (LoopKind Kind, string Start, string Title)? _chosen;

    // What "Start plan" starts now, with its row's title; null for nothing.
    private (LoopCandidate Loop, string Title)? _startTarget;

    // The planner's answers for the loops, kept for the tab's life: after a scan nearly every question is an old one.
    private readonly FitCache _fits = new();

    // The finished runs of routes.json (T13.6c) and what they measured with the overlay's ship; none without a ship.
    private IReadOnlyList<RouteRun> _runs;

    private MeasuredTimes _measured = MeasuredTimes.Empty;

    // The settings window's line under the switch (IMeasuredSummary); empty without a ship.
    private string _measuredSummary = "";

    // The map's start the player clicked (T13.7), for the session; routes.hub stays.
    private string? _mapFocus;

    // The active route (T13.6), drawn on the map in the status line's colour.
    private Route? _route;

    // The matrix of the filter, as the matrix view shows it; the map draws this one (A1).
    private PortMatrix? _matrix;

    // The custom route clicked on the map from the focus (T13.7b, D129), for the session, and what it comes to for the
    // overlay's ship; null without one.
    private CustomPlan? _custom;

    private CustomResult? _customResult;

    // Every node of a system for the map's layout, from everything its templates and the scans name; made anew with them.
    private (StarSystem System, ObservedEdges Observed, IReadOnlyDictionary<string, MapNode> NodeOf, IReadOnlyList<MapNode> Universe)? _mapNodes;

    /// <param name="save">Called with the new section when rank or faction change; null in tests that don't save.</param>
    /// <param name="observed">What the scans in routes.json add (T13.3); none without.</param>
    /// <param name="runs">The finished runs in routes.json (T13.6c): the loops' measured times; none without.</param>
    public RoutesViewModel(
        HaulingLoad load, ShipDataVersion version, RoutesSettings settings, Action<RoutesSettings>? save, ObservedEdges? observed = null, IReadOnlyList<RouteRun>? runs = null)
    {
        _settings = settings;
        _save = save;
        _observed = observed ?? ObservedEdges.Empty;
        _runs = runs ?? [];
        // Core's default filter (Stanton, stellar, T13.2 picture gate); rank and faction from settings.json.
        var start = RouteFilter.Default;
        _rank = Array.FindIndex(Ranks, r => r.Value == settings.MaxRank);
        _faction = Array.FindIndex(Factions, f => f.Value == settings.PlayerFaction);
        _direct = Array.FindIndex(Directs, d => d.Value == start.Direct);
        _contractor = Array.FindIndex(Contractors, c => c.Value == start.Contractor);
        _area = Array.FindIndex(Areas, a => a.Value == start.MissionType);
        _system = Array.FindIndex(Systems, s => s.Value == start.System);
        _hub = Math.Max(0, Array.FindIndex(Hubs, h => h.Name == settings.Hub));
        _sort = Math.Max(0, Array.FindIndex(Sorts, s => s.Value == settings.Sort));
        (_templates, _positions, StatusText) = load switch
        {
            HaulingLoad.Loaded loaded => (loaded.Data.Templates, loaded.Data.Positions, (string?)null),
            HaulingLoad.UnknownFormat => ([], Positions.Empty, $"The hauling data in ship data {version.Display} can't be used by this app – see cargoplan.log"),
            _ => ([], Positions.Empty, $"No hauling data in ship data {version.Display} – update ship data"),
        };
        Rebuild();
    }

    /// <summary>Instead of the matrix when there are no hauling data (K2: in the tab, never as a notice); null with data.</summary>
    public string? StatusText { get; }

    public bool HasData => StatusText is null;

    public string Legend => "Each cell: contracts · aUEC together; ● seen in your scans";

    public string RankLabel => Ranks[_rank].Label;

    public string DirectLabel => Directs[_direct].Label;

    public string ContractorLabel => Contractors[_contractor].Label;

    public string FactionLabel => Factions[_faction].Label;

    public string AreaLabel => Areas[_area].Label;

    public string SystemLabel => Systems[_system].Label;

    public string ViewLabel => Views[_view];

    /// <summary>The recipe instead of the matrix (T13.3b); the matrix's own filters hide.</summary>
    public bool IsRecipe => _view == 1;

    /// <summary>The loops instead of the matrix (T13.4); the matrix's own filters hide, the system filter stays.</summary>
    public bool IsLoops => _view == 2;

    public bool IsMatrix => _view == 0;

    /// <summary>The map instead of the matrix (T13.7): the matrix's cells as a graph, under the matrix's filters.</summary>
    public bool IsMap => _view == MapView;

    /// <summary>The filters from area on: the matrix's, which the map shares (T13.7).</summary>
    public bool ShowsMatrixFilters => IsMatrix || IsMap;

    /// <summary>"Start plan" shows with the recipe and the loops (T13.6), and with the map once a custom route has a contract (T13.7b).</summary>
    public bool OffersStart => HasData && (IsRecipe || IsLoops || (IsMap && HasCustom));

    /// <summary>The matrix and its legend: with hauling data, in the matrix view.</summary>
    public bool ShowsMatrix => HasData && IsMatrix;

    /// <summary>The recipe's lines: with hauling data, in the recipe view.</summary>
    public bool ShowsRecipe => HasData && IsRecipe;

    /// <summary>The loop list: with hauling data, in the loops view.</summary>
    public bool ShowsLoops => HasData && IsLoops;

    /// <summary>The map: with hauling data, in the map view.</summary>
    public bool ShowsMap => HasData && IsMap;

    public string MapLegend => "Line: contracts there · thicker: more aUEC · solid ●: seen in your scans · dashed: game data only · click a station: from there";

    /// <summary>What the map draws; null outside the map view or without a system.</summary>
    [ObservableProperty]
    public partial MapScene? Map { get; private set; }

    /// <summary>"No map for this system." or what the map leaves out; null otherwise.</summary>
    [ObservableProperty]
    public partial string? MapNote { get; private set; }

    /// <summary>The custom route has a contract (T13.7b): its lines and "Start plan" show with the map.</summary>
    public bool HasCustom => _custom is { Picks.Count: > 0 };

    /// <summary>One line per contract of the custom route, as the recipe's, with "fits" or "too big" (T13.7b).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> CustomLines { get; private set; } = [];

    /// <summary>"Own plan: Everus Harbor → Baijini Point → … → Everus Harbor · 7 contracts · Σ … aUEC · …"; null without one.</summary>
    [ObservableProperty]
    public partial string? CustomSummary { get; private set; }

    /// <summary>"Hub: Everus Harbor"; "(from Loops)" while a loop's row shows another hub than routes.hub.</summary>
    public string HubLabel => _shownHub is { } shown && shown != _hub ? $"Hub: {Hubs[shown].Name} (from Loops)" : $"Hub: {Hubs[_hub].Name}";

    public string SortLabel => Sorts[_sort].Label;

    /// <summary>The loops' times as the settings window sets them, shown, not edited here (T13.4).</summary>
    public string TimesText => Invariant($"{_settings.Times.Station} min · {_settings.Times.Crate} s · {_settings.Times.Jump} min");

    public string TimesToolTip => "Time per landing · per crate · per long jump (over 10 Gm), for ~aUEC/h; set them in Settings → Plan tab";

    /// <summary>The loop list (T13.4): ranked by the sort, the system filter's system only.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LoopRowView> LoopRows { get; private set; } = [];

    /// <summary>Why there are no loops, or that they are still being worked out; null otherwise.</summary>
    [ObservableProperty]
    public partial string? LoopsNote { get; private set; }

    /// <summary>What the loops are computed from now; null without hauling data or a ship.</summary>
    public LoopInputs? LoopInputs { get; private set; }

    /// <summary>The inputs changed: compute <see cref="LoopInputs.Run"/> off the UI thread, then <see cref="ShowLoops"/>.</summary>
    public event EventHandler<LoopInputs>? LoopInputsChanged;

    /// <summary>
    /// The loops computed from <paramref name="inputs"/>; dropped when the tab's inputs have changed since (a later result
    /// is on its way). Re-sorting needs no new result: the times and the sort only rank.
    /// </summary>
    public void ShowLoops(LoopInputs inputs, IReadOnlyList<LoopCandidate> loops)
    {
        if (!ReferenceEquals(inputs, LoopInputs))
        {
            return;
        }

        (_loops, _loopsComputed) = (loops, true);
        BuildLoops();
    }

    /// <summary>One line per role, what leaves the hub first, then the way back ("Back · … · accept at HUR-L1").</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RecipeLines { get; private set; } = [];

    /// <summary>
    /// "Σ 460,750 out · +232,000 back = 692,750 aUEC · worst case 512/512 SCU in 50 crates · fits MISC Hull B": the
    /// departure, the ways back and both, so it compares with a loop's aUEC (gate T13.4); null without a recipe.
    /// </summary>
    [ObservableProperty]
    public partial string? RecipeSummary { get; private set; }

    /// <summary>Why there is no recipe, or what it lacks; null otherwise.</summary>
    [ObservableProperty]
    public partial string? RecipeNote { get; private set; }

    /// <summary>The overlay's ship (T13.3b): the recipe is for it; a change makes it anew.</summary>
    public void ShowShip(Ship ship)
    {
        _ship = ship;
        Measure();
        AskForLoops();
        Rebuild();
    }

    /// <summary>The runs after "Finish plan" kept one more (T13.6c): the loops rank anew, nothing is computed again.</summary>
    public void ShowRuns(IReadOnlyList<RouteRun> runs)
    {
        _runs = runs;
        Measure();
        BuildLoops();
    }

    /// <summary>The line under the settings window's switch changed (gate finding T13.6c).</summary>
    public event EventHandler? Changed;

    string IMeasuredSummary.Text => _measuredSummary;

    // The runs of the overlay's ship per loop (D127); a run left out for its time is a line in the log, once per change.
    private void Measure()
    {
        _measured = _ship is { } ship ? MeasuredTimes.For(_runs, ship.Id) : MeasuredTimes.Empty;
        foreach (var line in _measured.Ignored)
        {
            AppLog.Write("Routes", line);
        }

        var summary = (_ship, _measured.EnoughCount) switch
        {
            (null, _) => "",
            ({ } s, 0) => Invariant($"No loop measured yet with {s.Name} – finish a plan {MeasuredTimes.MinRuns} times."),
            ({ } s, 1) => $"1 loop measured with {s.Name}",
            ({ } s, var n) => Invariant($"{n} loops measured with {s.Name}"),
        };
        if (summary != _measuredSummary)
        {
            _measuredSummary = summary;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The check before accepting (T13.3b, D123): the scanned offer matched to its template, against the recipe of the hub
    /// for the ship, whichever view is shown. Null without hauling data, a ship or a single template.
    /// </summary>
    public RecipeFit? FitOf(ObservedScan scan)
    {
        if (!HasData || _ship is not { } ship)
        {
            return null;
        }

        // Against routes.hub, where the scan happens (K1 of the T13.4 approval), whichever hub the recipe shows.
        var observation = Observation.Of(scan);
        return RecipeFit.Of(TemplateMatch.Find(observation, _templates, _positions), RecipeFor(ship, _hub), ship, observation.Destinations);
    }

    // The board of the hub's area up to the filter's rank, both factions (Stanton has none), with what the scans saw.
    private Recipe RecipeFor(Ship ship, int hub) =>
        Recipe.For(Board.For(_templates, StarSystem.Stanton, Hubs[hub].AreaKey, Ranks[_rank].Value, null), ship, _observed);

    [ObservableProperty]
    public partial bool ShowXs { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowSmall { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowMedium { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowLarge { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowBulk { get; set; } = true;

    /// <summary>The drop-offs, one column each, alphabetical.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Columns { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<PortRowView> Rows { get; private set; } = [];

    /// <summary>"2 templates not shown: …"; null when every template the filter lets through has a cell.</summary>
    [ObservableProperty]
    public partial string? WithoutPlaceText { get; private set; }

    /// <summary>With Pyro: Red Wind's region blocks are not on a board until a scan shows which station has which (design §5).</summary>
    [ObservableProperty]
    public partial string? RegionsText { get; private set; }

    /// <summary>"No templates for this filter." when data are there but the filter leaves no cell; else null.</summary>
    [ObservableProperty]
    public partial string? EmptyText { get; private set; }

    public RouteFilter Filter => new(
        Ranks[_rank].Value,
        new HashSet<HaulSize>(Sizes()),
        Directs[_direct].Value,
        Contractors[_contractor].Value,
        Factions[_faction].Value,
        Areas[_area].Value,
        Systems[_system].Value);

    /// <summary>
    /// "13 · 1.1M": templates and the sum of the known rewards ("–" if none, "+" if some are unknown), "● " first where a
    /// scan saw the pair (T13.3); the most SCU is in the tool tip.
    /// </summary>
    public static string CellText(PortCell cell)
    {
        var reward = cell.RewardSum is { } sum ? Short(sum) + (cell.UnknownRewards > 0 ? "+" : "") : "–";
        return Invariant($"{(cell.Observed ? ObservedMark : "")}{cell.Count} · {reward}");
    }

    /// <summary>
    /// Rank and faction as the settings window set them (T13.3): the filter shows them, nothing is saved back. The same
    /// values as now change nothing. The times (T13.4) and the measured times' switch (T13.6c) rank the loops anew.
    /// </summary>
    public void Follow(RoutesSettings settings)
    {
        if (settings.Rank == _settings.Rank && settings.Faction == _settings.Faction && settings.Times == _settings.Times
            && settings.UseMeasured == _settings.UseMeasured)
        {
            return;
        }

        // The times and the switch only rank the loops: no new computation for them.
        var boards = settings.Rank != _settings.Rank || settings.Faction != _settings.Faction;
        _settings = settings;
        _rank = Array.FindIndex(Ranks, r => r.Value == settings.MaxRank);
        _faction = Array.FindIndex(Factions, f => f.Value == settings.PlayerFaction);
        OnPropertyChanged(nameof(RankLabel));
        OnPropertyChanged(nameof(FactionLabel));
        OnPropertyChanged(nameof(TimesText));
        if (boards)
        {
            AskForLoops();
        }

        Rebuild();
    }

    /// <summary>What the scans saw, from routes.json after a scan (T13.3); the matrix and the loops follow.</summary>
    public void ShowObserved(ObservedEdges observed)
    {
        _observed = observed;
        AskForLoops();
        Rebuild();
    }

    /// <summary>aUEC in short: 950, 57k, 482k, 1.2M.</summary>
    public static string Short(long reward) => reward switch
    {
        < 1_000 => reward.ToString(CultureInfo.InvariantCulture),
        < 1_000_000 => Invariant($"{reward / 1_000}k"),
        _ => Invariant($"{reward / 1_000_000.0:0.#}M"),
    };

    [RelayCommand]
    private void Next(string filter) => Cycle(filter, +1);

    [RelayCommand]
    private void Previous(string filter) => Cycle(filter, -1);

    /// <summary>A loop's row was chosen with "Start plan" (T13.6): make the route of it, then show the Cargo tab.</summary>
    public event EventHandler<PlanStart>? StartRequested;

    /// <summary>"Start plan" has a loop with figures to start (K4 of T13.6).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartPlanCommand))]
    public partial bool CanStartPlan { get; private set; }

    /// <summary>What "Start plan" starts, or why it can't.</summary>
    [ObservableProperty]
    public partial string StartPlanToolTip { get; private set; } = "";

    /// <summary>The route of the loop <see cref="StartPlanToolTip"/> names; the App starts it and shows the Cargo tab.</summary>
    [RelayCommand(CanExecute = nameof(CanStartPlan))]
    private void StartPlan()
    {
        if (_startTarget is { Loop: var loop, Title: var title })
        {
            StartRequested?.Invoke(this, new PlanStart(loop, title, _observed));
        }

        // The custom route is the plan now (T13.7b): the map draws it as the active route.
        if (IsMap && _custom is not null)
        {
            _custom = null;
            BuildMap();
        }
    }

    /// <summary>
    /// A loop's row: chosen for "Start plan" (K4 of T13.6); a Stanton hub's row also shows the recipe of its hub, for the
    /// session (T13.4, D124); routes.hub stays. A row without a recipe (LEO, Pyro) is only marked.
    /// </summary>
    [RelayCommand]
    private void OpenLoop(LoopRowView? row)
    {
        if (row is null)
        {
            return;
        }

        _chosen = (row.Loop.Kind, row.Loop.Start, row.Title);
        if (row.Hub is not { } hub)
        {
            BuildLoops();
            return;
        }

        _shownHub = Array.FindIndex(Hubs, h => h.Name == hub);
        _view = 1;
        _system = Array.FindIndex(Systems, s => s.Value == StarSystem.Stanton);
        OnPropertyChanged(nameof(HubLabel));
        OnPropertyChanged(nameof(SystemLabel));
        ViewChanged();
        Rebuild();
    }

    /// <summary>
    /// A node of the map clicked (T13.7): the start for the session, only what leaves it and comes back stays full; the
    /// same node again shows everything. routes.hub and the recipe's hub stay.
    /// </summary>
    [RelayCommand]
    private void Focus(string? node)
    {
        // Another start begins another custom route (T13.7b).
        _mapFocus = node is null || node == _mapFocus ? null : node;
        _custom = null;
        BuildMap();
    }

    /// <summary>
    /// A click on a lit edge of the map with a station in focus (T13.7b, D129): the edge's next best-paying contract into
    /// the custom route; nothing without a ship or on an edge the focus dims.
    /// </summary>
    [RelayCommand]
    private void AddEdge(MapEdge? edge)
    {
        if (edge is null || CustomContextNow() is not { } context || Map?.Graph is not { } graph || !graph.IsLit(edge))
        {
            return;
        }

        _custom = (_custom ?? CustomPlan.Empty(_mapFocus!)).Add(edge, context);
        BuildMap();
    }

    /// <summary>A right click on an edge (T13.7b): its last contract out of the custom route.</summary>
    [RelayCommand]
    private void RemoveEdge(MapEdge? edge)
    {
        if (edge is null || _custom is null)
        {
            return;
        }

        _custom = _custom.Remove(edge);
        BuildMap();
    }

    /// <summary>"Clear": the custom route goes, the focus stays.</summary>
    [RelayCommand]
    private void ClearCustom()
    {
        _custom = null;
        BuildMap();
    }

    // What a custom route is worked out with: a station in focus (a surface is no place to start), a ship, a system.
    private CustomContext? CustomContextNow() =>
        _mapFocus is { } focus && _ship is { } ship && Systems[_system].Value is { } system && _positions.Find(system, focus) is not null
            ? new CustomContext(system, _positions, _observed, ship, _fits)
            : null;

    /// <summary>The active route (T13.6) or none: the map draws its legs and numbers its stations (T13.7).</summary>
    public void ShowRoute(Route? route)
    {
        if (route == _route)
        {
            return;
        }

        _route = route;
        BuildMap();
    }

    partial void OnShowXsChanged(bool value) => Rebuild();

    partial void OnShowSmallChanged(bool value) => Rebuild();

    partial void OnShowMediumChanged(bool value) => Rebuild();

    partial void OnShowLargeChanged(bool value) => Rebuild();

    partial void OnShowBulkChanged(bool value) => Rebuild();

    private void Cycle(string filter, int step)
    {
        static int Step(int index, int count, int step) => (index + step + count) % count;

        switch (filter)
        {
            case "rank":
                _rank = Step(_rank, Ranks.Length, step);
                Save(_settings with { Rank = EnumWords.Word(Ranks[_rank].Value) });
                OnPropertyChanged(nameof(RankLabel));
                AskForLoops();
                break;
            case "direct":
                _direct = Step(_direct, Directs.Length, step);
                OnPropertyChanged(nameof(DirectLabel));
                break;
            case "contractor":
                _contractor = Step(_contractor, Contractors.Length, step);
                OnPropertyChanged(nameof(ContractorLabel));
                break;
            case "faction":
                _faction = Step(_faction, Factions.Length, step);
                Save(_settings with { Faction = Factions[_faction].Value is { } f ? EnumWords.Word(f) : null });
                OnPropertyChanged(nameof(FactionLabel));
                AskForLoops();
                break;
            case "area":
                _area = Step(_area, Areas.Length, step);
                OnPropertyChanged(nameof(AreaLabel));
                break;
            case "system":
                // A custom route stays in its system (T13.7b).
                (_system, _custom) = (Step(_system, Systems.Length, step), null);
                OnPropertyChanged(nameof(SystemLabel));
                break;
            case "view":
                _view = Step(_view, Views.Length, step);
                ViewChanged();
                break;
            case "hub":
                // From the hub shown: after a loop's row, the next hub from there becomes routes.hub.
                (_hub, _shownHub) = (Step(_shownHub ?? _hub, Hubs.Length, step), null);
                Save(_settings with { Hub = Hubs[_hub].Name });
                OnPropertyChanged(nameof(HubLabel));
                AskForLoops();
                break;
            case "sort":
                _sort = Step(_sort, Sorts.Length, step);
                Save(_settings with { LoopsSort = RoutesSettings.Word(Sorts[_sort].Value) });
                OnPropertyChanged(nameof(SortLabel));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(filter), filter, "Not a filter of the Plan tab.");
        }

        Rebuild();
    }

    private void ViewChanged()
    {
        OnPropertyChanged(nameof(ViewLabel));
        OnPropertyChanged(nameof(IsRecipe));
        OnPropertyChanged(nameof(IsLoops));
        OnPropertyChanged(nameof(IsMatrix));
        OnPropertyChanged(nameof(IsMap));
        OnPropertyChanged(nameof(ShowsMatrixFilters));
        OnPropertyChanged(nameof(OffersStart));
        OnPropertyChanged(nameof(ShowsMatrix));
        OnPropertyChanged(nameof(ShowsRecipe));
        OnPropertyChanged(nameof(ShowsLoops));
        OnPropertyChanged(nameof(ShowsMap));
    }

    // New inputs for the loops: the listener computes them off the UI thread. Without data or a ship there are none.
    private void AskForLoops()
    {
        if (!HasData || _ship is not { } ship)
        {
            return;
        }

        LoopInputs = new LoopInputs(_templates, _positions, _observed, ship, Ranks[_rank].Value, _settings.PlayerFaction, Hubs[_hub].Name, _fits);
        LoopInputsChanged?.Invoke(this, LoopInputs);
    }

    private void Save(RoutesSettings settings)
    {
        _settings = settings;
        _save?.Invoke(settings);
    }

    private IEnumerable<HaulSize> Sizes()
    {
        (bool Shown, HaulSize Size)[] sizes =
            [(ShowXs, HaulSize.Xs), (ShowSmall, HaulSize.Small), (ShowMedium, HaulSize.Medium), (ShowLarge, HaulSize.Large), (ShowBulk, HaulSize.Bulk)];
        return sizes.Where(s => s.Shown).Select(s => s.Size);
    }

    private void Rebuild()
    {
        if (!HasData)
        {
            return;
        }

        var matrix = _matrix = PortMatrix.Build(_templates, Filter, _observed);
        Columns = matrix.Dropoffs;
        Rows = [.. matrix.Pickups.Select(p => new PortRowView(p, PlaceNames.Short(p), [.. matrix.Dropoffs.Select(d => View(p, d, matrix.Cell(p, d)))]))];
        WithoutPlaceText = matrix.WithoutPlace switch
        {
            0 => null,
            1 => "1 template not shown: the game data name no pickup or destination for it.",
            var n => Invariant($"{n} templates not shown: the game data name no pickup or destination for them."),
        };
        EmptyText = Rows.Count == 0 ? "No templates for this filter." : null;
        RegionsText = Systems[_system].Value == StarSystem.Pyro ? "Red Wind's regional templates are not shown yet: no scan has placed them." : null;
        if (!IsMatrix && !IsMap)
        {
            // The matrix's lines say nothing about the recipe or the loops; the map draws the matrix, they hold for it.
            (WithoutPlaceText, EmptyText, RegionsText) = (null, null, null);
        }

        BuildRecipe();
        BuildLoops();
        BuildMap();
    }

    /// <summary>
    /// The map of the matrix just built (T13.7, D128): its cells as edges between the system's nodes, with the focus and
    /// the route; only while the map is shown. The nodes of the system are made once per system and scans.
    /// </summary>
    private void BuildMap()
    {
        if (!HasData || !IsMap || _matrix is not { } matrix || Systems[_system].Value is not { } system)
        {
            Map = null;
            MapNote = HasData && IsMap && Systems[_system].Value is null ? "No map for this system." : null;
            return;
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (_mapNodes is not { } nodes || nodes.System != system || !ReferenceEquals(nodes.Observed, _observed))
        {
            var everything = PortMatrix.Build(_templates, RouteFilter.Everything(system), _observed);
            var nodeOf = MapNodes.For(everything.Pickups.Concat(everything.Dropoffs), _positions, system);
            _mapNodes = nodes = (system, _observed, nodeOf, [.. nodeOf.Values.Distinct()]);
        }

        // The custom route for the overlay's ship (T13.7b): its stations light the edges from them too.
        var context = CustomContextNow();
        _customResult = _custom is { Picks.Count: > 0 } custom && context is not null ? custom.Evaluate(context) : null;
        var graph = RouteMap.Build(matrix, nodes.NodeOf, system, _mapFocus, _route, _customResult?.Loop.Stations);
        var hub = system == StarSystem.Stanton ? Hubs[_shownHub ?? _hub].Name : null;
        var canPick = context is not null && graph.Focus is not null;
        Map = new MapScene(graph, nodes.Universe, system, hub, e => EdgeToolTip(e) + (canPick && graph.IsLit(e) ? PickHint : ""), n => NodeToolTip(graph, n), canPick);
        MapNote = graph.InsideOneNode switch
        {
            0 => null,
            1 => "1 contract between places of one planet's surface is not drawn.",
            var n => Invariant($"{n} contracts between places of one planet's surface are not drawn."),
        };
        (CustomLines, CustomSummary) = _customResult is { } result && context is not null ? Custom(result, context.Ship) : ([], null);
        OnPropertyChanged(nameof(HasCustom));
        OnPropertyChanged(nameof(OffersStart));
        BuildStart();
        AppLog.Write("Routes", Invariant($"map {system}: {graph.Nodes.Count} nodes, {graph.Edges.Count} edges in {watch.Elapsed.TotalMilliseconds:0.0} ms"));
    }

    private const string PickHint = "\nclick: add its next contract to your plan · right click: take its last back";

    // The custom route as the recipe shows a hub's: one line per contract, then the sum (T13.7b).
    private (IReadOnlyList<string> Lines, string? Summary) Custom(CustomResult result, Ship ship)
    {
        var loop = result.Loop;
        string[] lines =
        [
            .. result.Roles.Select(r =>
            {
                var role = r.Role;
                var t = role.Template;
                var places = string.Join(", ", role.Destinations.Select(PlaceNames.Short));
                var where = (role.Kind, role.AcceptAt) switch
                {
                    (RecipeKind.Return, _) => RoleText.Accept(role.AcceptAt, role.CollectAt)!,
                    (_, { } at) => $"{places} · accept at {PlaceNames.Short(at)}",
                    _ => places,
                };
                var line = RoleText.Line(role.Kind, t.Title, RoleText.Reward(t.Reward, _observed.RewardOf(t)), null, t.Legs.Sum(l => l.MinScu), t.MaxScu, t.Legs.Max(l => l.Container), where);
                return $"{line} · {(r.Fits ? "fits" : "too big")}";
            }),
        ];
        string[] trip = [loop.Start, .. loop.Stations, loop.Start];
        var reward = loop.Reward is { } sum ? Invariant($"Σ {sum:N0}{(loop.RewardPartial ? "+" : "")} aUEC") : "– aUEC: no reward in the game data, a scan shows it";
        var tooBig = result.Roles.Count(r => !r.Fits);
        var fit = tooBig == 0 ? $"fits {ship.Name}" : Invariant($"{tooBig} too big for {ship.Name}");
        var contracts = result.Roles.Count == 1 ? "1 contract" : Invariant($"{result.Roles.Count} contracts");
        return (lines, Invariant($"{CustomTitle(loop.Start)}: {string.Join(" → ", trip.Select(PlaceNames.Short))} · {contracts} · {reward} · {loop.Landings} landings · {loop.Crates} crates · {loop.LongJumps} long jumps · {fit}"));
    }

    // "Own plan from Everus Harbor": the custom route's name (T13.7b; the look is Martin's gate).
    private static string CustomTitle(string start) => $"Own plan from {start}";

    /// <summary>The loops of the system filter's system, ranked by the sort under the settings' times (cheap: no planner).</summary>
    private void BuildLoops()
    {
        if (!HasData)
        {
            return;
        }

        var system = Systems[_system].Value;
        LoopsNote = (_ship, _loopsComputed, system) switch
        {
            (null, _, _) => "Loops: no ship",
            (_, false, _) => "Loops: working them out …",
            (_, _, null) => "No loops for this system.",
            _ => null,
        };
        // The switch lives here, not in Core: off, Rank gets no measurements and every row is the model's (T13.6c).
        var times = _settings.Times.Loop;
        var measured = _settings.UseMeasured ? _measured : null;
        LoopRows = system is null
            ? []
            : [.. Loops.Rank(_loops.Where(l => l.System == system), Sorts[_sort].Value, times, measured)
                .Select(l => Row(l, times, _measured, _settings.UseMeasured) is var row && IsChosen(row.Loop, row.Title) ? row with { Chosen = true } : row)];
        BuildStart();
    }

    private bool IsChosen(LoopCandidate loop, string title) => _chosen == (loop.Kind, loop.Start, title);

    // K4 of T13.6: in Loops the row clicked last, else the top row with figures; in the recipe the row clicked last if it
    // starts at the hub shown (a direct run), else that hub's ring. The matrix starts nothing.
    private void BuildStart()
    {
        var shownHub = Hubs[_shownHub ?? _hub].Name;
        (LoopCandidate Loop, string Title)? target = (_view, _loopsComputed) switch
        {
            // The map starts the custom route (T13.7b), whether the loops are worked out or not.
            (MapView, _) => _customResult is { Loop: var custom } ? (custom, CustomTitle(custom.Start)) : null,
            (0, _) or (_, false) => null,
            (2, _) => LoopRows.FirstOrDefault(r => r.Chosen) is { } chosen
                ? (chosen.Loop, chosen.Title)
                : LoopRows.FirstOrDefault(r => r.Loop.HasNumbers) is { } top ? (top.Loop, top.Title) : null,
            _ => (_loops.FirstOrDefault(l => l.Start == shownHub && IsChosen(l, Title(l)))
                    ?? _loops.FirstOrDefault(l => l.Kind == LoopKind.LocalRing && l.System == StarSystem.Stanton && l.Start == shownHub))
                is { } loop ? (loop, Title(loop)) : null,
        };
        _startTarget = target;
        CanStartPlan = target is { Loop.HasNumbers: true };
        StartPlanToolTip = (target, _loopsComputed) switch
        {
            (null, _) when IsMap => "Start plan: click a station, then the contracts on its edges",
            ({ } t, _) when IsMap => $"Start plan: {t.Title} – its contracts as placeholders in the Cargo tab",
            (_, false) => "Start plan: the loops are being worked out …",
            (null, _) => IsRecipe ? $"Start plan: no loop starts at {shownHub}" : "Start plan: no loop with figures",
            ({ Loop.HasNumbers: false } t, _) => $"Start plan: {t.Title} – needs scans at {t.Loop.NeedsScansAt}",
            ({ } t, _) => $"Start plan: {t.Title} – its contracts as placeholders in the Cargo tab",
        };
    }

    // "Everus Harbor ring", "Everus Harbor → HUR-L1 · Senior Direct Small", "LEO ring from Everus Harbor".
    private static string Title(LoopCandidate loop) => loop.Kind switch
    {
        LoopKind.LocalRing => $"{loop.Start} ring",
        LoopKind.Direct => Invariant($"{loop.Start} → {PlaceNames.Short(loop.Stations[0])} · {Plain(loop.Contracts[0].Template.Title)}{(loop.Alike > 1 ? $" ×{loop.Alike}" : "")}"),
        LoopKind.Custom => CustomTitle(loop.Start),
        _ => $"LEO ring from {loop.Start}",
    };

    // "Everus Harbor ring" / "957k/loop · ~578k/h · 160k/landing" / "6 landings · 70 crates · 6 jumps (4 long) · travel – · templates".
    // With 3 runs or more and the switch on (T13.6c): "957k/loop · 718k/h · …" / "… · measured 3× · 1 h 20 min", no "~".
    private static LoopRowView Row(LoopCandidate loop, LoopTimes times, MeasuredTimes measured, bool useMeasured)
    {
        var title = Title(loop);
        var hubs = loop.Kind == LoopKind.SystemWide ? " hubs" : "";
        var runs = measured.Of(loop);
        var used = useMeasured && loop.HasNumbers && runs is { IsEnough: true } ? runs : null;
        var status = (used, loop.NeedsScansAt, loop.Observed) switch
        {
            ({ } m, _, _) => Invariant($"measured {m.Count}× · {Median(m)}"),
            (_, { } at, 0) => $"needs scans at {at}",
            (_, { } at, var seen) => Invariant($"observed {seen}/{loop.Total}{hubs} · needs scans at {at}"),
            (_, null, 0) => "templates",
            (_, null, var seen) => Invariant($"observed {seen}/{loop.Total}{hubs}"),
        };
        var travel = loop.TravelGm switch
        {
            null => loop.System == StarSystem.Stanton ? "travel –" : $"travel: {loop.System}",
            < 0.05 => "travel –",
            { } gm => Invariant($"travel {gm:0.0} Gm"),
        };

        string? figures = null;
        var details = status;
        if (loop.HasNumbers)
        {
            // A measured hour is the player's own, no estimate: no "~" (T13.6c).
            var perHour = Loops.PerHour(loop, times, used is null ? null : measured);
            figures = loop.Reward is { } reward
                ? Invariant($"{Short(reward)}{Plus(loop)}/loop · {(used is null ? "~" : "")}{Short((long)perHour!.Value)}{Plus(loop)}/h · {Short((long)loop.PerLanding!.Value)}{Plus(loop)}/landing")
                : "– aUEC: no reward in the game data, a scan shows it";
            details = Invariant($"{loop.Landings} landings · {loop.Crates} crates · {loop.Legs} jumps ({loop.LongJumps} long) · {travel} · {status}");
        }

        string[] trip = [loop.Start, .. loop.Stations, loop.Start];
        var tip = new StringBuilder(string.Join(" → ", trip.Select(PlaceNames.Short)));
        foreach (var c in loop.Contracts)
        {
            var reward = c.Template.Reward is { } r ? Invariant($"{r:N0} aUEC") : "–";
            tip.Append(Invariant($"\n{RoleText.KindWords[c.Kind]} · {c.Template.Title} · {reward} · {c.WorstScu} SCU{(RoleText.Accept(c.AcceptAt, c.CollectAt) is { } accept ? $" · {accept}" : "")}"));
        }

        // The runs, also where they don't count: the player sees what the switch or the threshold leaves out (T13.6c).
        tip.Append((runs, used) switch
        {
            (null, _) => "",
            (_, { } m) => Invariant($"\nmeasured {m.Count}×, median {Median(m)}: {string.Join(" · ", m.Recent.Select(r => Invariant($"{RoleText.Duration(r.Time)} ({r.FinishedUtc.ToLocalTime():d MMM})")))}"),
            ({ IsEnough: false } m, _) => Invariant($"\nmeasured {m.Count}× – {MeasuredTimes.MinRuns} needed"),
            ({ } m, _) => Invariant($"\nmeasured {m.Count}× · {Median(m)} – not used"),
        });

        var hub = loop.Kind != LoopKind.SystemWide && loop.System == StarSystem.Stanton ? loop.Start : null;
        return new(loop, title, figures, details, tip.ToString(), !loop.HasNumbers, hub);
    }

    private static string Plus(LoopCandidate loop) => loop.RewardPartial ? "+" : "";

    private static string Median(MeasuredLoop runs) => RoleText.Duration(TimeSpan.FromMinutes(runs.MedianMinutes));

    // "Experienced Rank - Direct Medium Cargo Haul" → "Experienced Direct Medium".
    private static string Plain(string title) => title.Replace(" Rank - ", " ", StringComparison.Ordinal).Replace(" Cargo Haul", "", StringComparison.Ordinal);

    /// <summary>The recipe of the hub's board for the ship (<see cref="Recipe.For"/>, D123), only while it is shown.</summary>
    private void BuildRecipe()
    {
        (IReadOnlyList<string> lines, string? summary, string? note) = (IsRecipe, _ship, Systems[_system].Value) switch
        {
            (false, _, _) => ([], null, null),
            (true, null, _) => ([], null, "Recipe: no ship"),
            (true, _, not StarSystem.Stanton) => ([], null, "Recipe: Stanton hubs only"),
            (true, { } ship, _) => Shown(RecipeFor(ship, _shownHub ?? _hub), ship),
        };
        (RecipeLines, RecipeSummary, RecipeNote) = (lines, summary, note);
    }

    private (IReadOnlyList<string> Lines, string? Summary, string? Note) Shown(Recipe recipe, Ship ship) =>
    (
        [.. recipe.Outbound.Concat(recipe.Returns).Select(Line)],
        recipe.Outbound.Count == 0
            ? null
            : Invariant(
                $"Σ {recipe.RewardSum:N0} out · +{recipe.ReturnSum:N0} back = {recipe.RewardSum + recipe.ReturnSum:N0} aUEC · worst case {recipe.WorstScu}/{ship.TotalScu} SCU in {recipe.Crates} crates · fits {ship.Name}"),
        recipe.Note);

    // "Bulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · 41–432 SCU in 16s · HUR-L1, HUR-L2"
    private string Line(RecipeRole role)
    {
        var t = role.Template;
        var where = RoleText.Accept(role.AcceptAt, role.CollectAt) ?? string.Join(", ", role.Destinations.Select(PlaceNames.Short));
        return RoleText.Line(role.Kind, t.Title, RoleText.Reward(t.Reward, _observed.RewardOf(t)), null, t.Legs.Sum(l => l.MinScu), t.MaxScu, t.Legs.Max(l => l.Container), where);
    }

    private static PortCellView? View(string pickup, string dropoff, PortCell? cell)
    {
        if (cell is null)
        {
            return null;
        }

        var tip = new StringBuilder(Invariant($"{pickup} → {dropoff} · most SCU in one contract: {cell.MaxScu}"));
        HubLines(tip, [cell]);
        TemplateLines(tip, cell);
        return new PortCellView(CellText(cell), tip.ToString());
    }

    // The board's hub (D121) once at the top, a mark on each template it concerns (E2 of T13.3a). A cell's pickup and
    // drop-off differ, so only one side can be the hub; a map's edge of several cells says it once for all.
    private static void HubLines(StringBuilder tip, IEnumerable<PortCell> cells)
    {
        PortCellTemplate[] shown = [.. cells.SelectMany(c => c.Templates.Take(MaxToolTipTemplates))];
        if (shown.Any(e => e.PickupFromHub))
        {
            tip.Append(Invariant($"\n{HubMark}pickup: the board's hub, not in the game data"));
        }

        if (shown.Any(e => e.DropoffFromHub))
        {
            tip.Append(Invariant($"\n{HubMark}drop-off: the board's hub, not in the game data"));
        }
    }

    // One line per template, as the matrix's cell lists them (T13.2, T13.3), for the matrix and the map (T13.7).
    private static void TemplateLines(StringBuilder tip, PortCell cell)
    {
        foreach (var entry in cell.Templates.Take(MaxToolTipTemplates))
        {
            var t = entry.Template;
            var reward = (t.Reward, entry.ObservedReward) switch
            {
                ({ } data, _) => Invariant($"{data:N0} aUEC"),
                (null, { } seen) => Invariant($"{seen:N0} aUEC observed"),
                _ => "no reward in the game data",
            };
            var mark = entry.PickupFromHub || entry.DropoffFromHub ? HubMark : "";
            tip.Append(Invariant($"\n{mark}{t.Title} · {reward} · {t.Legs.Sum(l => l.MinScu)}–{t.MaxScu} SCU"));
            if (entry.Observed is { } seenHere)
            {
                tip.Append(Invariant($" · observed {seenHere.Count}× · last {seenHere.LastUtc.ToLocalTime():d MMM}"));
            }
        }

        if (cell.Count > MaxToolTipTemplates)
        {
            tip.Append(Invariant($"\n… and {cell.Count - MaxToolTipTemplates} more"));
        }
    }

    /// <summary>
    /// An edge's tool tip (T13.7): "Everus Harbor → HUR-L5 High Course Station · 12.9 Gm · 4 contracts · Σ 245,000 aUEC ·
    /// most SCU in one contract: 432", the board's hub once, then the templates as the matrix's cell lists them, one
    /// section per place where a surface stands for several (K1).
    /// </summary>
    public static string EdgeToolTip(MapEdge edge)
    {
        var reward = (edge.RewardSum, edge.UnknownRewards) switch
        {
            (null, _) => "– aUEC: no reward in the game data, a scan shows it",
            ({ } sum, 0) => Invariant($"Σ {sum:N0} aUEC"),
            ({ } sum, var unknown) => Invariant($"Σ {sum:N0} aUEC + {unknown} without a reward"),
        };
        var contracts = edge.Count == 1 ? "1 contract" : Invariant($"{edge.Count} contracts");
        var tip = new StringBuilder(Invariant($"{edge.From} → {edge.To} · {edge.Gm:0.0} Gm · {contracts} · {reward} · most SCU in one contract: {edge.MaxScu}"));
        HubLines(tip, edge.Parts.Select(p => p.Cell));
        foreach (var part in edge.Parts)
        {
            if (edge.Parts.Count > 1)
            {
                tip.Append(Invariant($"\n{part.Pickup} → {part.Dropoff}:"));
            }

            TemplateLines(tip, part.Cell);
        }

        return tip.ToString();
    }

    // "Everus Harbor · 12 out · 9 in · Σ out 1,234,500 aUEC", the places of a surface, and what a click does.
    private static string NodeToolTip(RouteMap graph, MapNode node)
    {
        var figures = graph.Figures(node.Key);
        var reward = figures.OutReward is { } sum ? Invariant($" · Σ out {sum:N0} aUEC") : "";
        var tip = new StringBuilder(Invariant($"{node.Key} · {figures.Out} out · {figures.In} in{reward}"));
        if (node.Kind == MapNodeKind.Surface)
        {
            tip.Append(Invariant($"\nsurface and moons: {string.Join(", ", node.Places)}"));
        }

        tip.Append(graph.Focus == node.Key ? "\nclick: show every contract again" : "\nclick: only what leaves here and comes back");
        return tip.ToString();
    }
}
