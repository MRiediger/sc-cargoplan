using CargoPlan.App.Editor;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Import;
using CargoPlan.Import.ContractTracker;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>State of the overlay window. Shows what Core computes; no planning here.</summary>
/// <param name="ships">The ships there are (D59: a ship is a catalog entry); null = only <paramref name="ship"/>.</param>
public sealed partial class OverlayViewModel(Ship ship, ShipCatalog? ships = null) : ObservableObject
{
    public const string NoPlanText = "No plan. Open the editor to enter missions.";

    public const string AllDeliveredText = "All stops delivered.";

    public const string MoveRefusedPrefix = "Move refused: ";

    public const string TickRefusedPrefix = "Tick refused: ";

    public const string SplitRefusedPrefix = "Split refused: ";

    public const string MergeRefusedPrefix = "Merge refused: ";

    public const string SplitText = "Split crate";

    public const string MergeText = "Merge crates";

    public const string SelectOneCrateText = "Select one crate to split.";

    public const string SelectCratesText = "Select two or more crates to merge.";

    public const string SyncPausedText = "ContractTracker sync paused: plan file not loaded. Plan in the editor to start it again.";

    public const string DeletedContractsPrefix = "Deleted in ContractTracker, crates still on board: ";

    public const string DeletedContractsSuffix = ". Tick their stops or delete the missions in the editor.";

    public const string DatabaseLooksNewText = "ContractTracker database looks new – all contracts imported.";

    /// <summary>Auto-sync off (D44): what a capture says instead of coming into the plan.</summary>
    public static string NewContractsText(int count) =>
        $"{count} new {(count == 1 ? "contract" : "contracts")} in ContractTracker – take {(count == 1 ? "it" : "them")} in with Sync from ContractTracker (editor or hotkey).";

    public static string ReplannedText(string madeFor, string current) => $"Plan was made for {madeFor}, replanned for {current}.";

    public const string PlanUnchangedText = "Plan unchanged";

    public const string ReplanToolTipText = "Re-plan: lay out all cargo on board anew – ticks and crate sizes stay, you restack the ship";

    public const string NothingToReplanText = "Re-plan: no cargo on board";

    /// <summary>What the overlay asks before every re-plan (D66): no way past it, button or hotkey.</summary>
    public const string ReplanConfirmText =
        "Re-plan? All cargo on board is laid out anew: crates change racks and cells, you restack the ship, and manual moves are lost. Ticks and crate sizes stay.";

    /// <summary>
    /// Asks the player before a re-plan and says whether to go on; the overlay window shows a message box. The view model
    /// stays WPF-free, and without a window the answer is no: a re-plan never runs unconfirmed.
    /// </summary>
    public Func<string, bool> ConfirmReplan { get; set; } = _ => false;

    /// <summary>
    /// What the player's Re-plan did (D66), instead of asking first: how many crates on board moved, or that nothing did;
    /// then the missions that still wait.
    /// </summary>
    public static string ReplanText(bool changed, int moved, IReadOnlyList<string> waiting)
    {
        var text = changed ? $"Plan rebuilt – {moved} {(moved == 1 ? "crate" : "crates")} moved" : PlanUnchangedText;
        return waiting.Count switch
        {
            0 => text,
            1 => $"{text} – {waiting[0]} still doesn't fit",
            _ => $"{text} – {string.Join(", ", waiting)} still don't fit",
        };
    }

    public const string StopsMergedPrefix = "Stops merged: ";

    /// <summary>The notice for stops renamed to another name of their place (B1); null if none was.</summary>
    public static string? StopsMergedText(IReadOnlyList<(string From, string To)> merged) =>
        merged.Count == 0 ? null : StopsMergedPrefix + string.Join(", ", merged.Select(m => $"{m.From} → {m.To}"));

    /// <summary>What the overlay asks before a ship change with a plan (D59).</summary>
    public static string ReplanQuestionText(string ship) =>
        $"Replan for {ship}? Crate positions change and manual moves are lost; split crates keep their sizes.";

    public static string TicksStayText(int stops) => $"{stops} {(stops == 1 ? "stop" : "stops")} already ticked – ticks stay.";

    /// <summary>One line for what went wrong with ship-outlines.json (D62): the first problem and how many more; null if none.</summary>
    public static string? OutlinesNoticeText(IReadOnlyList<string> problems) => problems.Count switch
    {
        0 => null,
        1 => $"Ship outlines: {problems[0]} – ships without one show the bow wedge.",
        _ => $"Ship outlines: {problems[0]} (+{problems.Count - 1} more) – ships without one show the bow wedge.",
    };

    /// <summary>The notice line when a hologram can't be shown (D63); one per run, from <see cref="HoloLoader"/>.</summary>
    public static string HoloNoticeText(string reason) => $"Ship hologram not available: {reason} – showing the outline";

    /// <summary>
    /// The outlines loaded at the start (D55), matched to a ship by its Fleetyards slug. Set once by the composition root;
    /// the scene built before it gets its marker here.
    /// </summary>
    public ShipOutlineCatalog Outlines
    {
        get;
        init
        {
            field = value;
            Scene = Marked(Scene);
        }
    } = ShipOutlineCatalog.Empty;

    /// <summary>What lies behind the racks (D62, D63), from the settings; a change redraws the ship view at once, plan and camera stay.</summary>
    public ShipOutlineMode OutlineMode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Scene = Marked(Scene);
            OnPropertyChanged(nameof(Scene));
            OnPropertyChanged(nameof(Holo));
        }
    }

    /// <summary>The hologram backdrop's opacity (D63), from the settings; bound by the backdrop, so the slider applies live.</summary>
    public double HoloOpacity
    {
        get;
        set => SetProperty(ref field, value);
    } = AppearanceRules.DefaultHoloOpacity;

    /// <summary>The hologram's hull in the shaded and flat looks (D63), "#AARRGGBB" from the settings; bound by the backdrop.</summary>
    public string HoloHullColor
    {
        get;
        set => SetProperty(ref field, value);
    } = AppearanceRules.DefaultHoloHullColor;

    /// <summary>The hologram's edges in the hidden-line look, like <see cref="HoloHullColor"/>.</summary>
    public string HoloEdgeColor
    {
        get;
        set => SetProperty(ref field, value);
    } = AppearanceRules.DefaultHoloEdgeColor;

    // The last hologram the loader handed over; shown only while it fits (Holo).
    private PreparedHolo? _holo;

    /// <summary>
    /// The hologram the backdrop draws (D63): the one <see cref="HoloLoader"/> handed over, while a hologram look is chosen
    /// and it belongs to <see cref="Ship"/>; null otherwise, so another ship or the outline hides it at once, before the
    /// loader answers. Not loaded here: the view model only shows.
    /// </summary>
    public PreparedHolo? Holo => OutlineMode.IsHologram() && _holo is { } holo && holo.ShipId == Ship.Id ? holo : null;

    /// <summary>
    /// From <see cref="HoloLoader"/>: the hologram for the current ship and look, or null for the outline. The scene is
    /// built anew only when the hologram's box changes, since a new scene empties the selection (D25): another look of the
    /// same ship only redraws the backdrop.
    /// </summary>
    public void ShowHolo(PreparedHolo? holo)
    {
        if (ReferenceEquals(holo, _holo))
        {
            return;
        }

        var before = Holo?.Placement;
        _holo = holo;
        if (!Equals(before, Holo?.Placement))
        {
            Scene = Marked(Scene);
            OnPropertyChanged(nameof(Scene));
        }

        OnPropertyChanged(nameof(Holo));
    }

    /// <summary>The ship the plan, the ship view and the header are for; changed only by <see cref="ChangeShip"/>.</summary>
    public Ship Ship { get; private set; } = ship;

    public ShipCatalog Ships { get; } = ships ?? new ShipCatalog([ship], []);

    private IReadOnlyList<string> _recentShips = [];

    private IReadOnlyList<ShipChoice>? _shipSelection;

    /// <summary>
    /// The ships chosen last, latest first, the current one included (T7.2c): from settings.json at the start, then kept
    /// by <see cref="ChangeShip"/>, which updates them before it tells <see cref="Ship"/>, so the settings get both at once.
    /// </summary>
    public IReadOnlyList<string> RecentShips
    {
        get => _recentShips;
        init => _recentShips = value;
    }

    /// <summary>
    /// The header's ship selection (T7.2b): the recent ships on top (T7.2c), then the catalog grouped by manufacturer,
    /// ships that didn't load last. Built anew by <see cref="ChangeShip"/>, since Recent changes with the ship.
    /// </summary>
    public IReadOnlyList<ShipChoice> ShipSelection => _shipSelection ??= ShipChoices.Of(Ships, RecentShips, Ship.Id);

    /// <summary>The entry of <see cref="Ship"/> in <see cref="ShipSelection"/>; follows <see cref="ChangeShip"/>.</summary>
    public ShipChoice CurrentShipChoice => ShipSelection.First(c => c.Id == Ship.Id);

    /// <summary>
    /// The plan with its missions and the ContractTracker watermark: the one state every change goes through and
    /// <c>PlanSaver</c> saves (D18, T5.4). Set by <see cref="ShowPlan"/>, ticks, moves, split and merge, the sync and
    /// removing a mission. Written by hand like <see cref="Selection"/>: the scene is built before PropertyChanged, so
    /// bindings read the new one. A new plan builds a new scene, which empties the selection (D25): buttons and tool tips
    /// follow.
    /// </summary>
    public SavedPlan? Saved
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            Scene = Marked(value is null ? EmptyScene(Ship) : ShipViewBuilder.Build(value, Ship, LoadPlanner.Validate(value.Plan, Ship)));
            OnPropertyChanged(nameof(Plan));
            OnPropertyChanged(nameof(Missions));
            OnPropertyChanged(nameof(Watermark));
            OnPropertyChanged(nameof(Scene));
            OnPropertyChanged(nameof(HasPlan));
            OnPropertyChanged(nameof(SplitToolTip));
            OnPropertyChanged(nameof(MergeToolTip));
            OnPropertyChanged(nameof(ReplanToolTip));
            SplitCommand.NotifyCanExecuteChanged();
            MergeCommand.NotifyCanExecuteChanged();
            ReplanCommand.NotifyCanExecuteChanged();
            UpdateDeletedContracts();
        }
    }

    /// <summary>The crates of <see cref="Saved"/>; null without a plan.</summary>
    public LoadPlan? Plan => Saved?.Plan;

    /// <summary>
    /// The crates picked by right click in the ship view (D25), bound two-way: the view picks, split and merge select
    /// the crates they made (D27). Written by hand, not generated: a partial property can't take the initial value.
    /// </summary>
    public CrateSelection Selection
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(SplitToolTip));
                OnPropertyChanged(nameof(MergeToolTip));
                SplitCommand.NotifyCanExecuteChanged();
                MergeCommand.NotifyCanExecuteChanged();
            }
        }
    } = CrateSelection.Empty;

    /// <summary>The split button's tool tip: what it does, or why it can't (D25: the reason Core would refuse with).</summary>
    public string SplitToolTip => SplitProblem() ?? SplitText;

    /// <summary>The merge button's tool tip, like <see cref="SplitToolTip"/>.</summary>
    public string MergeToolTip => MergeProblem() ?? MergeText;

    /// <summary>The header's Re-plan button's tool tip (D66), like <see cref="SplitToolTip"/>: what it does, or why it can't.</summary>
    public string ReplanToolTip => CanReplan() ? ReplanToolTipText : NothingToReplanText;

    /// <summary>
    /// The player's lock: --locked sets the start value, the lock window switches it via <see cref="ToggleLockCommand"/>.
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty#notifying-dependent-properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial bool IsManuallyLocked { get; set; }

    /// <summary>Set by the cursor state monitor while Star Citizen has the mouse (T3.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    [NotifyPropertyChangedFor(nameof(AreToolWindowsHidden))]
    public partial bool IsAutoLocked { get; set; }

    /// <summary>
    /// true = click-through: mouse input goes to the window below. Default false, see D11.
    /// The manual lock forces locked but doesn't lift the auto-lock: unlocked by hand still locks
    /// while flying, or the next tractor-beam click would be lost.
    /// </summary>
    public bool IsLocked => IsManuallyLocked || IsAutoLocked;

    private readonly Dictionary<NoticeSource, string> _notices = [];

    /// <summary>Problems the player should know about, one line per source in <see cref="NoticeSource"/> order; empty hides them.</summary>
    public IReadOnlyList<string> Notices { get; private set; } = [];

    /// <summary>
    /// Sets or clears (null) the line of one source and leaves the others alone: a successful settings save must not
    /// clear a hotkey problem, which stays until the next start (D18).
    /// </summary>
    public void SetNotice(NoticeSource source, string? text)
    {
        if (_notices.GetValueOrDefault(source) == text)
        {
            return;
        }

        if (text is null)
        {
            _notices.Remove(source);
        }
        else
        {
            _notices[source] = text;
        }

        Notices = [.. Enum.GetValues<NoticeSource>().Where(_notices.ContainsKey).Select(s => _notices[s])];
        OnPropertyChanged(nameof(Notices));
    }

    /// <summary>The missions of <see cref="Saved"/>, as entered or captured (D23, D42); empty without a plan.</summary>
    public IReadOnlyList<Mission> Missions => Saved?.Missions ?? [];

    /// <summary>The ContractTracker watermark of <see cref="Saved"/> (D39 addendum); 0 without a plan.</summary>
    public long Watermark => Saved?.Watermark ?? 0;

    // The ids ContractTracker still had at the last read; null before one. Kept to name deleted contracts on every change.
    private IReadOnlySet<long>? _present;

    // plan.json couldn't be read: the sync must not overwrite it with a plan of its own (T5.4 planning).
    private bool _contractTrackerPaused;

    /// <summary>
    /// Shows a plan made in the editor or loaded; ends a sync pause. A plan for another ship (plan.json) is planned anew
    /// for this one and says so (D59): every other way into <see cref="Saved"/> goes through Core with <see cref="Ship"/>,
    /// so the ship view never gets a plan of another ship.
    /// </summary>
    public void ShowPlan(SavedPlan saved)
    {
        if (saved.Plan.ShipId == Ship.Id)
        {
            SetNotice(NoticeSource.Replan, null);
        }
        else
        {
            SetNotice(NoticeSource.Replan, ReplannedText(Ships.Find(saved.Plan.ShipId)?.Name ?? saved.Plan.ShipId, Ship.Name));
            saved = LoadPlanner.Replan(saved, Ship);
        }

        // A plan from before B1 may hold two names of one place; the editor's plans never do.
        var unified = DestinationNames.Unify(saved, Ship, []);
        SetNotice(NoticeSource.Stops, StopsMergedText(unified.Merged));
        Saved = unified.Saved;
        SetNotice(NoticeSource.Move, null);
        if (_contractTrackerPaused)
        {
            _contractTrackerPaused = false;
            SetNotice(NoticeSource.ContractTracker, null);
        }
    }

    /// <summary>
    /// The question before <see cref="ChangeShip"/> (D59), for the overlay's dialog; null = change at once (no mission in
    /// the plan, or the same ship). Names the ticked stops, which keep their ticks.
    /// </summary>
    public string? ShipChangeQuestion(Ship ship)
    {
        if (Missions.Count == 0 || ship.Id == Ship.Id)
        {
            return null;
        }

        var ticked = Scene.Legend.Count(l => l.Delivered);
        return ticked == 0
            ? ReplanQuestionText(ship.Name)
            : ReplanQuestionText(ship.Name) + Environment.NewLine + TicksStayText(ticked);
    }

    /// <summary>
    /// Puts the overlay on another ship (D59): the plan is planned anew for it (<see cref="LoadPlanner.Replan"/>), the
    /// missions, ticks and the watermark stay, so the ContractTracker sync goes on as before. Without a plan only the ship
    /// view changes. Clears the start's ship and replan notices: the player chose.
    /// </summary>
    public void ChangeShip(Ship ship)
    {
        if (ship.Id == Ship.Id)
        {
            return;
        }

        // Planned before the switch: the scene is built once, for the new ship with its plan.
        var replanned = Saved is { } saved ? LoadPlanner.Replan(saved, ship) : null;
        _recentShips = ShipChoices.Remember(_recentShips, Ship.Id, ship.Id);
        _shipSelection = null;
        Ship = ship;
        SetNotice(NoticeSource.Ship, null);
        SetNotice(NoticeSource.Replan, null);
        if (replanned is null)
        {
            Scene = Marked(EmptyScene(ship));
            OnPropertyChanged(nameof(Scene));
        }
        else
        {
            Saved = replanned;
        }

        OnPropertyChanged(nameof(Ship));
        OnPropertyChanged(nameof(ShipSelection));
        OnPropertyChanged(nameof(CurrentShipChoice));
        OnPropertyChanged(nameof(Holo));
    }

    /// <summary>
    /// The one way a ContractTracker read reaches the plan (T5.4), on the UI thread: Core takes the captures in
    /// (<see cref="ContractSync.Apply"/>). A read without new captures and without a missing id changes nothing (D38: diff,
    /// don't react), so no new scene and no save; the button (<paramref name="manual"/>) always syncs, which also places
    /// waiting missions that fit now. Without a plan, a capture starts one. A new database (D44) always changes the plan:
    /// its watermark goes back, also when the file is still empty.
    /// </summary>
    public void ApplyContractTracker(ContractTrackerReadResult result, bool manual = false)
    {
        if (_contractTrackerPaused)
        {
            return;
        }

        SetNotice(NoticeSource.ContractTracker, result.Notice ?? (result.DatabaseLooksNew ? DatabaseLooksNewText : null));
        // The plan didn't know the new captures' ids when asking; they are there, of course.
        if (result.Present is { } present)
        {
            _present = new HashSet<long>([.. present, .. result.Candidates.SelectMany(c => c.ContractIds)]);
        }

        var known = ContractTrackerQuery().KnownIds;
        if (!manual && !result.DatabaseLooksNew && result.Candidates.Count == 0 && (result.Present is not { } read || known.All(read.Contains)))
        {
            UpdateDeletedContracts();
            return;
        }

        // One name per place before Core sees the captures (B1), so new crates join the rack of their place (D41). A read
        // without news returned above, so the notice of loading stays until something comes in.
        var current = Saved ?? new SavedPlan([], new LoadPlan(Ship.Id, [], []));
        var unified = DestinationNames.Unify(current, Ship, [.. result.Candidates.Select(c => c.ToCapture())]);
        SetNotice(NoticeSource.Stops, StopsMergedText(unified.Merged));
        var applied = ContractSync.Apply(unified.Saved, Ship, unified.Captures, result.Present, result.DatabaseLooksNew);
        if (!ReferenceEquals(applied, current))
        {
            Saved = applied;
        }

        UpdateDeletedContracts();
    }

    /// <summary>
    /// Auto-sync off (D44): a read only counts the captures above the watermark; the plan stays as it is, deleted contracts
    /// included. The reader's notice wins; the count goes once the button or hotkey takes them in.
    /// </summary>
    public void ShowNewContracts(ContractTrackerReadResult result)
    {
        if (_contractTrackerPaused)
        {
            return;
        }

        SetNotice(
            NoticeSource.ContractTracker,
            result.Notice ?? (result.Candidates.Count > 0 ? NewContractsText(result.Candidates.Count) : null));
    }

    /// <summary>A read that failed for a reason the player has to fix, e.g. a file that is no database.</summary>
    public void ShowContractTrackerProblem(string text) => SetNotice(NoticeSource.ContractTracker, text);

    /// <summary>Stops the sync until the next <see cref="ShowPlan"/>; plan.json couldn't be read and stays as it is.</summary>
    public void PauseContractTracker()
    {
        _contractTrackerPaused = true;
        SetNotice(NoticeSource.ContractTracker, SyncPausedText);
    }

    /// <summary>What the next read needs: the watermark and the capture ids of the plan, whose deletion it reports.</summary>
    public (long Watermark, IReadOnlyCollection<long> KnownIds) ContractTrackerQuery() =>
        (Watermark, [.. Missions.SelectMany(m => m.Contract?.ContractIds ?? [])]);

    /// <summary>Takes a mission out with its crates (D42), from the editor's Delete mission; nothing else moves.</summary>
    public void RemoveMission(int missionId)
    {
        // Unreachable without a plan: the editor has no mission rows and the legend no lines then.
        var saved = Saved ?? throw new InvalidOperationException(NoPlanText);
        Saved = LoadPlanner.RemoveMission(saved, Ship, missionId);
    }

    /// <summary>
    /// Lays out the cargo on board anew, as a fresh plan of the same missions would (D66): the header's and the editor's
    /// Re-plan button and the replan hotkey. Ticks, crate sizes and fill order stay, waiting missions get placed where
    /// they fit. Only after the player said yes (<see cref="ConfirmReplan"/>); then the notice line says what happened. If
    /// nothing moved and nothing came in, the plan stays as it is (no new scene, the selection stays). Synchronous: the
    /// measured cases take a few milliseconds (B4).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReplan))]
    private void Replan()
    {
        var saved = Saved ?? throw new InvalidOperationException(NoPlanText);
        if (!ConfirmReplan(ReplanConfirmText))
        {
            return;
        }

        var replanned = LoadPlanner.Replan(saved, Ship);
        var moved = LoadPlanner.MovedCrates(saved.Plan, replanned.Plan);
        var changed = moved > 0 || replanned.Missions.Count(m => m.Unplaced) < saved.Missions.Count(m => m.Unplaced);
        if (changed)
        {
            Saved = replanned;
        }

        SetNotice(NoticeSource.Replan, null);
        SetNotice(NoticeSource.Move, ReplanText(changed, moved, [.. replanned.Missions.Where(m => m.Unplaced).Select(MissionText.Name)]));
    }

    private bool CanReplan() => Plan?.Crates.Any(c => !c.Delivered) == true;

    /// <summary>Ticking the line of a mission that didn't fit drops it (D41): abandoned, out of the plan.</summary>
    [RelayCommand]
    private void DropMission(int missionId) => RemoveMission(missionId);

    /// <summary>The notice for contracts deleted in ContractTracker with crates on board; derived again on every change.</summary>
    private void UpdateDeletedContracts()
    {
        if (_present is null || Saved is not { } saved || ContractSync.Conflicts(saved, _present) is not [_, ..] conflicts)
        {
            SetNotice(NoticeSource.DeletedContracts, null);
            return;
        }

        var names = conflicts.Select(saved.MissionOf).Select(m => $"{MissionText.Name(m)} ({MissionText.Source(m.Contract)})");
        SetNotice(NoticeSource.DeletedContracts, DeletedContractsPrefix + string.Join(", ", names) + DeletedContractsSuffix);
    }

    /// <summary>Both windows hide while true. Hiding resets nothing: position, zoom, orbit and lock stay as they are.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreToolWindowsHidden))]
    public partial bool IsOverlayHidden { get; set; }

    /// <summary>
    /// Editor and settings window hide with the overlay, and while Star Citizen has the mouse: owned by the topmost overlay
    /// they are topmost too and would catch the tractor-beam click (D11, D19). Not with the manual lock: that is the
    /// player's choice with a free cursor.
    /// </summary>
    public bool AreToolWindowsHidden => IsOverlayHidden || IsAutoLocked;

    /// <summary>
    /// false hides the legend's stop lines; warnings and notices stay (D33). Bound by the window, so the settings
    /// window (T4.2) can switch it live. By hand, not generated: a partial property can't take the initial value.
    /// </summary>
    public bool IsLegendVisible
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>Where the legend stands (D33); bound like <see cref="IsLegendVisible"/>.</summary>
    public LegendPosition LegendPosition
    {
        get;
        set => SetProperty(ref field, value);
    } = LegendPosition.Default;

    // Generates ToggleLockCommand (IRelayCommand), bound by the lock window's button and run by the toggleLock hotkey.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand
    [RelayCommand]
    private void ToggleLock() => IsManuallyLocked = !IsManuallyLocked;

    /// <summary>Run by the toggleOverlay hotkey: with a hidden cursor nobody can click the overlay away (D17).</summary>
    [RelayCommand]
    private void ToggleOverlay() => IsOverlayHidden = !IsOverlayHidden;

    /// <summary>
    /// Ticks a stop off in the legend, or back after a misclick: the whole stop, split parts included (D16). Ticking back
    /// is refused once another crate took a cell of the stop (D24 addendum); the reason goes to the notice line, like a
    /// refused move. A method parameter makes the generator write an <c>IRelayCommand&lt;string&gt;</c>; the legend passes
    /// the destination.
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand#command-parameters
    [RelayCommand]
    private void ToggleDelivered(string destination)
    {
        // Unreachable without a plan: the legend has no lines then.
        var plan = Plan ?? throw new InvalidOperationException(NoPlanText);
        var delivered = plan.PlacementsForStop(destination).All(p => p.Delivered);
        try
        {
            Saved = WithCrates(LoadPlanner.SetDelivered(plan, Ship, destination, !delivered));
            SetNotice(NoticeSource.Move, null);
        }
        catch (PlanException ex)
        {
            SetNotice(NoticeSource.Move, TickRefusedPrefix + ex.Message);
        }
    }

    /// <summary>
    /// Puts crates on cells, run by a right drag in the ship view (D25). Core finds room where the cells don't hold them
    /// and derives the hints anew (D65); what moved besides the dragged crates goes to the notice line. Only if no rack
    /// takes them does the plan stay, with "Move refused: …" as the preview showed it. Saved like every change of
    /// <see cref="Plan"/>. One record as parameter: a generated command takes at most one.
    /// </summary>
    [RelayCommand]
    private void MoveCrates(Gesture.MoveCrates move) => Apply(plan => CrateDrop.Outcome(plan, Ship, move));

    /// <summary>Puts a whole stop into a rack, run by a right drag of its legend line into the ship view (D25); like <see cref="MoveCrates"/>.</summary>
    [RelayCommand]
    private void MoveStop(Gesture.MoveStop move) => Apply(plan => CrateDrop.StopOutcome(plan, Ship, move));

    private void Apply(Func<LoadPlan, MoveOutcome> resolve)
    {
        // Unreachable without a plan: the view has no cargo and the legend no lines to drag then.
        var outcome = resolve(Plan ?? throw new InvalidOperationException(NoPlanText));
        if (outcome.Plan is not { } moved)
        {
            SetNotice(NoticeSource.Move, MoveRefusedPrefix + outcome.Refusal);
            return;
        }

        Saved = WithCrates(moved);
        SetNotice(NoticeSource.Move, outcome.Notices.Count == 0 ? null : string.Join(" · ", outcome.Notices));
    }

    /// <summary>
    /// Splits the one selected crate into the two largest parts, the way the kiosk delivered it (D27); run by the header
    /// button. Both parts stay selected, so splitting again or merging back needs no new pick.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSplit))]
    private void Split() => Edit(SplitRefusedPrefix, saved => LoadPlanner.SplitCrate(saved, Ship, Selected.Single()));

    /// <summary>Merges the selected crates into one (D27), which stays selected; like <see cref="Split"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanMerge))]
    private void Merge() => Edit(MergeRefusedPrefix, saved => LoadPlanner.MergeCrates(saved, Ship, [.. Selected]));

    private bool CanSplit() => SplitProblem() is null;

    private bool CanMerge() => MergeProblem() is null;

    /// <summary>The selected crates of the current scene; empty after every plan change (D25).</summary>
    private IReadOnlySet<Crate> Selected => Selection.In(Scene);

    // Selected crates belong to the current plan, so Plan isn't null here.
    private string? SplitProblem() => Selected.Count == 1 && Plan is { } plan
        ? LoadPlanner.SplitProblem(Saved!, Ship, Selected.Single())
        : SelectOneCrateText;

    private string? MergeProblem() => Selected.Count >= 2 && Plan is { } plan
        ? LoadPlanner.MergeProblem(Saved!, Ship, [.. Selected])
        : SelectCratesText;

    /// <summary>
    /// Runs a split or merge on plan and missions together, shows and saves the result (<see cref="ShowPlan"/>) and
    /// selects the crates it made. A refusal goes to the notice line, like a refused move (D24).
    /// </summary>
    private void Edit(string refusedPrefix, Func<SavedPlan, SavedPlan> edit)
    {
        // Unreachable without a plan: the selection is empty then, so the buttons are disabled.
        var plan = Plan ?? throw new InvalidOperationException(NoPlanText);
        try
        {
            var saved = edit(Saved!);
            HashSet<Crate> before = [.. plan.Crates];
            ShowPlan(saved);
            Selection = CrateSelection.Of(Scene, saved.Plan.Crates.Where(c => !before.Contains(c)));
        }
        catch (PlanException ex)
        {
            SetNotice(NoticeSource.Move, refusedPrefix + ex.Message);
        }
    }

    public bool HasPlan => Plan is not null;

    /// <summary>
    /// What the ship view and the legend draw: the plan with Core's validation (D24), or the empty ship without a plan.
    /// Built once per change of <see cref="Plan"/>, not per binding.
    /// </summary>
    public ShipScene Scene { get; private set; } = EmptyScene(ship);

    /// <summary>The plan with new crates (a tick, a move); missions and watermark stay.</summary>
    private SavedPlan WithCrates(LoadPlan plan) => new(Missions, plan, Watermark);

    private static ShipScene EmptyScene(Ship ship) => ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);

    /// <summary>
    /// The scene with its bow marker (D62, D63): the hologram where one is shown for the scene's ship, else the ship's
    /// outline, the wedge for a ship without one, or none. Every scene the overlay shows passes here; placing an outline
    /// costs a pass over its points.
    /// </summary>
    private ShipScene Marked(ShipScene scene) => scene with
    {
        Marker = OutlineMode == ShipOutlineMode.None ? new BowMarker.None()
            : Holo is { } holo && holo.ShipId == scene.Ship.Id ? new BowMarker.Holo(holo.Placement.Min, holo.Placement.Max)
            : Outlines.For(scene.Ship) is { } outline ? new BowMarker.Outline(OutlinePlacement.Place(outline, scene.Ship.Bounds))
            : new BowMarker.Wedge(),
    };
}
