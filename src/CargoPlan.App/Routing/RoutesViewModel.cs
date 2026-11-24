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
public sealed record LoopRowView(LoopCandidate Loop, string Title, string? Figures, string Details, string ToolTip, bool Grey, string? Hub)
{
    public bool CanOpen => Hub is not null;
}

/// <summary>
/// The Plan tab (the Routes tab, D120; T13.2): the port matrix of the hauling templates (design §8.5) under a filter whose
/// parts cycle on a click, with what the scans saw (T13.3, <see cref="ObservedEdges"/>, computed by the caller). The matrix
/// is Core's (<see cref="PortMatrix"/>); this formats it. Rank and faction go to settings.json (routes.rank,
/// routes.faction), the other filters live for the session. The loops (T13.4, D124) are computed off the UI thread by
/// whoever listens to <see cref="LoopInputsChanged"/> and come back through <see cref="ShowLoops"/>.
/// </summary>
public sealed partial class RoutesViewModel : ObservableObject
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

    // The view of the Plan tab (T13.3b, T13.4): the port matrix, the recipe of a hub for the overlay's ship, the loops.
    private static readonly string[] Views = ["Matrix", "Recipe", "Loops"];

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

    // The planner's answers for the loops, kept for the tab's life: after a scan nearly every question is an old one.
    private readonly FitCache _fits = new();

    /// <param name="save">Called with the new section when rank or faction change; null in tests that don't save.</param>
    /// <param name="observed">What the scans in routes.json add (T13.3); none without.</param>
    public RoutesViewModel(HaulingLoad load, ShipDataVersion version, RoutesSettings settings, Action<RoutesSettings>? save, ObservedEdges? observed = null)
    {
        _settings = settings;
        _save = save;
        _observed = observed ?? ObservedEdges.Empty;
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

    /// <summary>The matrix and its legend: with hauling data, in the matrix view.</summary>
    public bool ShowsMatrix => HasData && IsMatrix;

    /// <summary>The recipe's lines: with hauling data, in the recipe view.</summary>
    public bool ShowsRecipe => HasData && IsRecipe;

    /// <summary>The loop list: with hauling data, in the loops view.</summary>
    public bool ShowsLoops => HasData && IsLoops;

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
        AskForLoops();
        Rebuild();
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
    /// values as now change nothing.
    /// </summary>
    public void Follow(RoutesSettings settings)
    {
        if (settings.Rank == _settings.Rank && settings.Faction == _settings.Faction && settings.Times == _settings.Times)
        {
            return;
        }

        // The times only rank the loops (T13.4): no new computation for them.
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

    /// <summary>A loop's row: the recipe of its hub, for the session (T13.4, D124); routes.hub stays. Others do nothing.</summary>
    [RelayCommand]
    private void OpenLoop(LoopRowView? row)
    {
        if (row?.Hub is not { } hub)
        {
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
                _system = Step(_system, Systems.Length, step);
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
        OnPropertyChanged(nameof(ShowsMatrix));
        OnPropertyChanged(nameof(ShowsRecipe));
        OnPropertyChanged(nameof(ShowsLoops));
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

        var matrix = PortMatrix.Build(_templates, Filter, _observed);
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
        if (!IsMatrix)
        {
            // The matrix's lines say nothing about the recipe or the loops.
            (WithoutPlaceText, EmptyText, RegionsText) = (null, null, null);
        }

        BuildRecipe();
        BuildLoops();
    }

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
        var times = _settings.Times.Loop;
        LoopRows = system is null ? [] : [.. Loops.Rank(_loops.Where(l => l.System == system), Sorts[_sort].Value, times).Select(l => Row(l, times))];
    }

    // "Everus Harbor ring" / "957k/loop · ~578k/h · 160k/landing" / "6 landings · 70 crates · 6 jumps (4 long) · travel – · templates".
    private static LoopRowView Row(LoopCandidate loop, LoopTimes times)
    {
        var title = loop.Kind switch
        {
            LoopKind.LocalRing => $"{loop.Start} ring",
            LoopKind.Direct => Invariant($"{loop.Start} → {PlaceNames.Short(loop.Stations[0])} · {Plain(loop.Contracts[0].Template.Title)}{(loop.Alike > 1 ? $" ×{loop.Alike}" : "")}"),
            _ => $"LEO ring from {loop.Start}",
        };
        var hubs = loop.Kind == LoopKind.SystemWide ? " hubs" : "";
        var status = (loop.NeedsScansAt, loop.Observed) switch
        {
            ({ } at, 0) => $"needs scans at {at}",
            ({ } at, var seen) => Invariant($"observed {seen}/{loop.Total}{hubs} · needs scans at {at}"),
            (null, 0) => "templates",
            (null, var seen) => Invariant($"observed {seen}/{loop.Total}{hubs}"),
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
            figures = loop.Reward is { } reward
                ? Invariant($"{Short(reward)}{Plus(loop)}/loop · ~{Short((long)loop.PerHour(times)!.Value)}{Plus(loop)}/h · {Short((long)loop.PerLanding!.Value)}{Plus(loop)}/landing")
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

        var hub = loop.Kind != LoopKind.SystemWide && loop.System == StarSystem.Stanton ? loop.Start : null;
        return new(loop, title, figures, details, tip.ToString(), !loop.HasNumbers, hub);
    }

    private static string Plus(LoopCandidate loop) => loop.RewardPartial ? "+" : "";

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

        // The board's hub (D121) once at the top, a mark on each template it concerns (E2 of T13.3a). A cell's pickup and
        // drop-off differ, so only one side can be the hub.
        var shown = cell.Templates.Take(MaxToolTipTemplates).ToList();
        if (shown.Any(e => e.PickupFromHub))
        {
            tip.Append(Invariant($"\n{HubMark}pickup: the board's hub, not in the game data"));
        }

        if (shown.Any(e => e.DropoffFromHub))
        {
            tip.Append(Invariant($"\n{HubMark}drop-off: the board's hub, not in the game data"));
        }

        foreach (var entry in shown)
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

        return new PortCellView(CellText(cell), tip.ToString());
    }
}
