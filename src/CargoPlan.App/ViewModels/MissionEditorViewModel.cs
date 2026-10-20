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

    private readonly OverlayViewModel _overlay;

    // Rows whose PropertyChanged we listen to. Clear() raises Reset without the old items, so they are tracked here.
    private readonly HashSet<MissionRow> _attached = [];

    private MissionRowsResult _result = MissionRows.ToStops([]);

    // The last plan, import, export or clipboard problem; the next edit clears it.
    private string? _actionProblem;

    // A field of its own, not the property's: the constructor sets the start value without marking a change.
    private FillOrder _fillOrder;

    /// <summary>Starts with the missions of the overlay's plan, or one blank row without a plan.</summary>
    public MissionEditorViewModel(OverlayViewModel overlay)
    {
        _overlay = overlay;
        _fillOrder = overlay.Plan?.FillOrder ?? FillOrder.BowFirst;
        Rows = [.. MissionRows.FromStops(overlay.Saved?.Stops ?? [])];
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
    [NotifyCanExecuteChangedFor(nameof(RemoveRowCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial MissionRow? SelectedRow { get; set; }

    /// <summary>The rows differ from the overlay's plan: edited, imported or cleared since opening or the last plan.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsDirty { get; set; }

    public string Title => IsDirty ? UnplannedTitleText : TitleText;

    /// <summary>The status line: the last action's problem, else the first invalid row; null = ready to plan.</summary>
    public string? Problem => _actionProblem ?? _result.Problem;

    /// <summary>Every filled row is valid, so the rows make a route that can be planned and exported.</summary>
    public bool CanExport => _result.IsValid;

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

    /// <summary>A new route is a new plan: ticks and moves of the old one are gone (D19). A problem keeps the editor open.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Plan()
    {
        var stops = _result.Stops;
        try
        {
            var plan = LoadPlanner.Plan(_overlay.Ship, stops.ToStops(), FillOrder);
            _overlay.ShowPlan(SavedPlan.FromStops(stops, plan, _overlay.Watermark));
            IsDirty = false;
            SetActionProblem(null);
            Planned?.Invoke(this, EventArgs.Empty);
        }
        catch (PlanException ex)
        {
            SetActionProblem(ex.Message);
        }
    }

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
        if (e.PropertyName == nameof(OverlayViewModel.Plan))
        {
            OnPropertyChanged(nameof(HasPlan));
            OnPropertyChanged(nameof(ClearsDelivered));
        }
    }

    private void Edited()
    {
        _actionProblem = null;
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
        PlanCommand.NotifyCanExecuteChanged();
    }

    private void SetActionProblem(string? text)
    {
        _actionProblem = text;
        OnPropertyChanged(nameof(Problem));
    }
}
