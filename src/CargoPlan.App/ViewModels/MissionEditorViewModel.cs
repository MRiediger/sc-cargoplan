using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using CargoPlan.App.Editor;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// State of the mission editor (D19). The rows are a draft until <see cref="PlanCommand"/>; closing without it keeps the
/// overlay's plan. File dialogs and the clipboard belong to the window, so this needs neither in tests.
/// </summary>
public sealed partial class MissionEditorViewModel : ObservableObject
{
    public const string TitleText = "Missions";
    public const string UnplannedTitleText = "Missions – unplanned changes";
    public const string ClearsDeliveredText = "Planning again clears delivered marks.";
    public const string PlanText = "Plan the rows and show them in the overlay";
    public const string MissionDeletedText = "Mission deleted – plan updated, no need to press Plan.";
    public const string ChangedOutsideText =
        "The plan changed outside the editor (ContractTracker or the legend): new missions were added below. Plan replaces the plan with these rows.";

    private readonly OverlayViewModel _overlay;

    // Rows whose PropertyChanged we listen to. Clear() raises Reset without the old items, so they are tracked here.
    private readonly HashSet<MissionRow> _attached = [];

    private MissionRowsResult _result = MissionRows.ToStops([]);

    // The last plan, import, export or clipboard problem; the next edit clears it.
    private string? _actionProblem;

    // A field of its own, not the property's: the constructor sets the start value without marking a change.
    private FillOrder _fillOrder;

    // "Sync from ContractTracker": the overlay's sync, the same read the watcher runs (T5.4); null without one.
    private readonly Func<Task>? _syncFromContractTracker;

    // The overlay's missions the rows were made from: a tick keeps the list, a sync or a deletion makes a new one.
    private IReadOnlyList<Mission> _missions;

    // Set while the editor itself changes the overlay's plan, which then needs no reaction.
    private bool _ownChange;

    /// <summary>Starts with the missions of the overlay's plan, or one blank row without a plan.</summary>
    /// <param name="syncFromContractTracker">Runs a ContractTracker sync; the Sync button is off without it.</param>
    public MissionEditorViewModel(OverlayViewModel overlay, Func<Task>? syncFromContractTracker = null)
    {
        _overlay = overlay;
        _syncFromContractTracker = syncFromContractTracker;
        _fillOrder = overlay.Plan?.FillOrder ?? FillOrder.BowFirst;
        _missions = overlay.Missions;
        Rows = [.. RowsOf(overlay)];
        if (Rows.Count == 0)
        {
            Rows.Add(new MissionRow());
        }

        AttachRows();
        Evaluate();
        Rows.CollectionChanged += OnRowsChanged;
        overlay.PropertyChanged += OnOverlayChanged;
    }

    public ObservableCollection<MissionRow> Rows { get; }

    public static IReadOnlyList<FillOrderChoice> FillOrders { get; } =
    [
        new(FillOrder.BowFirst, "Bow first"),
        new(FillOrder.SternFirst, "Stern first"),
    ];

    /// <summary>
    /// Which end of each rack the crates fill from (D24), for the next <see cref="PlanCommand"/>; starts with the current
    /// plan's. Like the rows a draft: changing it changes nothing until Plan. Clear rows and Import keep it, it belongs
    /// to the player's way of loading, not to the missions.
    /// </summary>
    public FillOrder FillOrder
    {
        get => _fillOrder;
        set
        {
            if (SetProperty(ref _fillOrder, value))
            {
                Edited();
            }
        }
    }

    /// <summary>The rows went to the overlay as a new plan; the window closes on it (D19). Not raised when planning fails.</summary>
    public event EventHandler? Planned;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveRowCommand), nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(DeleteMissionCommand))]
    public partial MissionRow? SelectedRow { get; set; }

    /// <summary>
    /// What happened to the plan that the player should know, not a problem: a mission deleted, or the plan changed from
    /// outside while rows were edited. The next edit clears it.
    /// </summary>
    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>The rows differ from the overlay's plan: edited, imported or cleared since opening or the last plan.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsDirty { get; set; }

    public string Title => IsDirty ? UnplannedTitleText : TitleText;

    /// <summary>The status line: the last action's problem, else the first invalid row; null = ready to plan.</summary>
    public string? Problem => _actionProblem ?? _result.Problem;

    /// <summary>Every filled row is valid, so the rows make a route that can be planned and exported.</summary>
    public bool CanExport => _result.IsValid;

    /// <summary>
    /// The Plan button's tool tip: while the rows block it, why, like the overlay's split and merge buttons (D27); a
    /// disabled button is hard to tell from an enabled one in the dark theme.
    /// </summary>
    public string PlanToolTip => _result.Problem ?? PlanText;

    public bool HasPlan => _overlay.Plan is not null;

    /// <summary>The current plan has ticked stops, and a new plan starts without ticks (D19).</summary>
    public bool ClearsDelivered => _overlay.Plan?.Placements.Any(p => p.Delivered) == true;

    /// <summary>Stops listening to the overlay; the window calls it when it closes.</summary>
    public void Detach() => _overlay.PropertyChanged -= OnOverlayChanged;

    /// <summary>A problem the window ran into (clipboard, dialog), shown like the others until the next edit.</summary>
    public void ShowProblem(string text) => SetActionProblem(text);

    /// <summary>The overlay's plan as text for Discord or org chat; null without a plan.</summary>
    public string? PlanAsText() => _overlay.Plan is { } plan ? PlanTextRenderer.Render(plan, _overlay.Ship) : null;

    /// <summary>Replaces the rows with the missions of a mission file; a broken file keeps the rows.</summary>
    public void Import(string path)
    {
        try
        {
            var stops = MissionFileReader.Read(path);
            Rows.Clear();
            foreach (var row in MissionRows.FromStops(stops))
            {
                Rows.Add(row);
            }
        }
        catch (MissionFormatException ex)
        {
            SetActionProblem(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetActionProblem($"Import failed ({ex.Message}).");
        }
    }

    /// <summary>Writes the current rows as a mission file, planned or not. Only valid rows can be exported.</summary>
    public void Export(string path)
    {
        if (!CanExport)
        {
            return;
        }

        try
        {
            AtomicFile.Write(path, stream => MissionFileWriter.Write(stream, _result.Stops));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetActionProblem($"Export failed ({ex.Message}).");
        }
    }

    // Generated commands: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand
    [RelayCommand]
    private void AddRow()
    {
        MissionRow row = new();
        Rows.Add(row);
        SelectedRow = row;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveRow()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var index = Rows.IndexOf(row);
        Rows.RemoveAt(index);
        SelectedRow = Rows.Count == 0 ? null : Rows[Math.Min(index, Rows.Count - 1)];
    }

    /// <summary>Empties the draft for the next route; the overlay's plan stays until <see cref="PlanCommand"/>.</summary>
    [RelayCommand]
    private void ClearRows()
    {
        Rows.Clear();
        Rows.Add(new MissionRow());
        SelectedRow = null;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(+1);

    /// <summary>
    /// A new route is a new plan: ticks and moves of the old one are gone (D19). Captures keep their ids, the watermark
    /// stays (T5.4). A problem keeps the editor open.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Plan()
    {
        var (missions, route) = MissionRows.ToMissions(Rows, _overlay.Missions);
        try
        {
            var saved = LoadPlanner.Plan(_overlay.Ship, missions, route, FillOrder, _overlay.Watermark);
            ChangeOverlay(() => _overlay.ShowPlan(saved));
            IsDirty = false;
            SetActionProblem(null);
            Planned?.Invoke(this, EventArgs.Empty);
        }
        catch (PlanException ex)
        {
            SetActionProblem(ex.Message);
        }
    }

    /// <summary>
    /// Takes the selected row's mission out of the plan at once, with its crates; nothing else moves, ticks stay (D42).
    /// Unlike the rows a draft no more: deleting needs no planning. Its rows leave the draft, which stays as clean as it
    /// was; the status line says so, so nobody presses Plan for it and plans everything anew.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteMission))]
    private void DeleteMission()
    {
        if (SelectedRow?.MissionId is not { } id)
        {
            return;
        }

        var wasDirty = IsDirty;
        ChangeOverlay(() => _overlay.RemoveMission(id));
        _missions = _overlay.Missions;
        foreach (var row in Rows.Where(r => r.MissionId == id).ToList())
        {
            Rows.Remove(row);
        }

        if (Rows.Count == 0)
        {
            Rows.Add(new MissionRow());
        }

        SelectedRow = null;
        IsDirty = wasDirty;
        Status = MissionDeletedText;
    }

    /// <summary>Runs the overlay's ContractTracker sync, like a capture would (T5.4); new missions come in as rows.</summary>
    // A method returning Task gives an AsyncRelayCommand (IAsyncRelayCommand).
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand#asynchronous-commands
    [RelayCommand(CanExecute = nameof(CanSync))]
    private Task Sync() => _syncFromContractTracker?.Invoke() ?? Task.CompletedTask;

    private bool CanSync() => _syncFromContractTracker is not null;

    private bool CanDeleteMission() => SelectedRow?.MissionId is { } id && _overlay.Missions.Any(m => m.Id == id);

    private bool HasSelection() => SelectedRow is not null;

    private bool CanMoveUp() => SelectedRow is { } row && Rows.IndexOf(row) > 0;

    private bool CanMoveDown() => SelectedRow is { } row && Rows.IndexOf(row) is var index && index >= 0 && index < Rows.Count - 1;

    /// <summary>No-op at the edges, so a button pressed once too often does nothing.</summary>
    private void Move(int offset)
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var index = Rows.IndexOf(row);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Rows.Count)
        {
            return;
        }

        Rows.Move(index, target);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachRows();

        // A blank new row, from the Add button or the data grid's new-item row, takes the max crate size of the row
        // above: contracts of one board mostly share one. The crates by hand belong to their row and are not copied.
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is [MissionRow { IsBlank: true } added]
            && e.NewStartingIndex > 0)
        {
            added.MaxCrateSize = Rows[e.NewStartingIndex - 1].MaxCrateSize;
        }

        Edited();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        RemoveRowCommand.NotifyCanExecuteChanged();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        // StopNumber and Error are set by Evaluate; reacting to them would loop.
        if (MissionRow.IsInput(e.PropertyName))
        {
            Edited();
        }
    }

    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OverlayViewModel.Plan))
        {
            return;
        }

        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(ClearsDelivered));
        DeleteMissionCommand.NotifyCanExecuteChanged();

        // A tick keeps the missions; a capture, a waiting mission placed or one dropped in the legend doesn't.
        if (_ownChange || ReferenceEquals(_overlay.Missions, _missions))
        {
            return;
        }

        _missions = _overlay.Missions;
        if (IsDirty)
        {
            // The edits stay; new missions come in as rows, or Plan would drop a capture the watermark has passed.
            HashSet<int> shown = [.. Rows.Select(r => r.MissionId).OfType<int>()];
            foreach (var row in RowsOf(_overlay).Where(r => r.MissionId is { } id && !shown.Contains(id)))
            {
                Rows.Add(row);
            }

            IsDirty = true;
            Status = ChangedOutsideText;
            return;
        }

        Rows.Clear();
        foreach (var row in RowsOf(_overlay))
        {
            Rows.Add(row);
        }

        if (Rows.Count == 0)
        {
            Rows.Add(new MissionRow());
        }

        IsDirty = false;
    }

    /// <summary>Rows of the overlay's missions in route order (T5.4); none without a plan.</summary>
    private static IReadOnlyList<MissionRow> RowsOf(OverlayViewModel overlay) =>
        overlay.Saved is { } saved ? MissionRows.FromMissions(saved.Missions, [.. saved.Stops.Select(s => s.Destination)]) : [];

    private void ChangeOverlay(Action change)
    {
        _ownChange = true;
        try
        {
            change();
        }
        finally
        {
            _ownChange = false;
        }
    }

    private void Edited()
    {
        _actionProblem = null;
        Status = null;
        IsDirty = true;
        Evaluate();
    }

    private void AttachRows()
    {
        foreach (var gone in _attached.Where(r => !Rows.Contains(r)).ToList())
        {
            gone.PropertyChanged -= OnRowChanged;
            _attached.Remove(gone);
        }

        foreach (var row in Rows.Where(_attached.Add))
        {
            row.PropertyChanged += OnRowChanged;
        }
    }

    private void Evaluate()
    {
        _result = MissionRows.ToStops(Rows);
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].StopNumber = _result.StopNumbers[i];
            Rows[i].Error = _result.Errors[i];
            Rows[i].CratesText = _result.CrateTexts[i];
        }

        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(PlanToolTip));
        PlanCommand.NotifyCanExecuteChanged();
    }

    private void SetActionProblem(string? text)
    {
        _actionProblem = text;
        OnPropertyChanged(nameof(Problem));
    }
}
