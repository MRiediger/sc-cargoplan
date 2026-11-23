using System.Globalization;
using System.Text;
using CargoPlan.App.Settings;
using CargoPlan.Core.Json;
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
/// The Plan tab (the Routes tab, D120; T13.2): the port matrix of the hauling templates (design §8.5) under a filter whose
/// parts cycle on a click, with what the scans saw (T13.3, <see cref="ObservedEdges"/>, computed by the caller). The matrix
/// is Core's (<see cref="PortMatrix"/>); this formats it. Rank and faction go to settings.json (routes.rank,
/// routes.faction), the other filters live for the session.
/// </summary>
public sealed partial class RoutesViewModel : ObservableObject
{
    private const int MaxToolTipTemplates = 15;

    private const string ObservedMark = "● ";

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

    private readonly IReadOnlyList<HaulTemplate> _templates;

    private readonly Action<RoutesSettings>? _save;

    private ObservedEdges _observed;

    private RoutesSettings _settings;

    private int _rank;

    private int _direct;

    private int _contractor;

    private int _faction;

    private int _area;

    private int _system;

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
        (_templates, StatusText) = load switch
        {
            HaulingLoad.Loaded loaded => (loaded.Data.Templates, (string?)null),
            HaulingLoad.UnknownFormat => ([], $"The hauling data in ship data {version.Display} can't be used by this app – see cargoplan.log"),
            _ => ([], $"No hauling data in ship data {version.Display} – update ship data"),
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

    /// <summary>What the scans saw, from routes.json after a scan (T13.3); the matrix follows.</summary>
    public void ShowObserved(ObservedEdges observed)
    {
        _observed = observed;
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
                break;
            case "area":
                _area = Step(_area, Areas.Length, step);
                OnPropertyChanged(nameof(AreaLabel));
                break;
            case "system":
                _system = Step(_system, Systems.Length, step);
                OnPropertyChanged(nameof(SystemLabel));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(filter), filter, "Not a filter of the Plan tab.");
        }

        Rebuild();
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
    }

    private static PortCellView? View(string pickup, string dropoff, PortCell? cell)
    {
        if (cell is null)
        {
            return null;
        }

        var tip = new StringBuilder(Invariant($"{pickup} → {dropoff} · most SCU in one contract: {cell.MaxScu}"));
        foreach (var entry in cell.Templates.Take(MaxToolTipTemplates))
        {
            var t = entry.Template;
            var reward = (t.Reward, entry.ObservedReward) switch
            {
                ({ } data, _) => Invariant($"{data:N0} aUEC"),
                (null, { } seen) => Invariant($"{seen:N0} aUEC observed"),
                _ => "no reward in the game data",
            };
            tip.Append(Invariant($"\n{t.Title} · {reward} · {t.Legs.Sum(l => l.MinScu)}–{t.MaxScu} SCU"));
            if (entry.Observed is { } seenHere)
            {
                tip.Append(Invariant($" · observed {seenHere.Count}× · last {seenHere.LastUtc.ToLocalTime():d MMM}"));
            }
            if (entry.PickupFromHub)
            {
                tip.Append(" · pickup: hub of the board, not in the game data");
            }

            if (entry.DropoffFromHub)
            {
                tip.Append(" · drop-off: hub of the board, not in the game data");
            }
        }

        if (cell.Count > MaxToolTipTemplates)
        {
            tip.Append(Invariant($"\n… and {cell.Count - MaxToolTipTemplates} more"));
        }

        return new PortCellView(CellText(cell), tip.ToString());
    }
}
