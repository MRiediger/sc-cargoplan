using CargoPlan.App.ShipView;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>State of the overlay window. Shows what Core computes; no planning here.</summary>
public sealed partial class OverlayViewModel(Ship ship) : ObservableObject
{
    public const string NoPlanText = "No plan. Open the editor to enter missions.";

    public const string AllDeliveredText = "All stops delivered.";

    public const string MoveRefusedPrefix = "Move refused: ";

    public const string SplitRefusedPrefix = "Split refused: ";

    public const string MergeRefusedPrefix = "Merge refused: ";

    public const string SplitText = "Split crate";

    public const string MergeText = "Merge crates";

    public const string SelectOneCrateText = "Select one crate to split.";

    public const string SelectCratesText = "Select two or more crates to merge.";

    public Ship Ship { get; } = ship;

    // [ObservableProperty] on partial properties (Toolkit 8.4+, C# 14 here): the generator writes the
    // body with SetProperty and PropertyChanged.
    // Source: https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.0
    // A new plan builds a new scene, which empties the selection (D25): buttons and tool tips follow.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty#notifying-dependent-commands
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Scene))]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
    [NotifyPropertyChangedFor(nameof(SplitToolTip), nameof(MergeToolTip))]
    [NotifyCanExecuteChangedFor(nameof(SplitCommand), nameof(MergeCommand))]
    public partial LoadPlan? Plan { get; set; }

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

    /// <summary>
    /// The missions <see cref="Plan"/> was made from, as entered (D23), saved with it (D18). Set together with it by
    /// <see cref="ShowPlan"/>.
    /// </summary>
    public IReadOnlyList<MissionStop> Missions { get; private set; } = [];

    /// <summary>Shows a plan with its missions. Missions first: the plan's PropertyChanged starts the save, which reads both.</summary>
    public void ShowPlan(SavedPlan saved)
    {
        Missions = saved.Missions;
        Plan = saved.Plan;
        SetNotice(NoticeSource.Move, null);
    }

    /// <summary>Both windows hide while true. Hiding resets nothing: position, zoom, orbit and lock stay as they are.</summary>
    [ObservableProperty]
    public partial bool IsOverlayHidden { get; set; }

    // Generates ToggleLockCommand (IRelayCommand), bound by the lock window's button and run by the toggleLock hotkey.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand
    [RelayCommand]
    private void ToggleLock() => IsManuallyLocked = !IsManuallyLocked;

    /// <summary>Run by the toggleOverlay hotkey: with a hidden cursor nobody can click the overlay away (D17).</summary>
    [RelayCommand]
    private void ToggleOverlay() => IsOverlayHidden = !IsOverlayHidden;

    /// <summary>
    /// Ticks a stop off in the legend, or back after a misclick: the whole stop, split parts included (D16).
    /// A method parameter makes the generator write an <c>IRelayCommand&lt;string&gt;</c>; the legend passes the destination.
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand#command-parameters
    [RelayCommand]
    private void ToggleDelivered(string destination)
    {
        // Unreachable without a plan: the legend has no lines then.
        var plan = Plan ?? throw new InvalidOperationException(NoPlanText);
        var delivered = plan.PlacementsForStop(destination).All(p => p.Delivered);
        Plan = LoadPlanner.SetDelivered(plan, Ship, destination, !delivered);
    }

    /// <summary>
    /// Puts crates on cells, run by a right drag in the ship view (D25). Core checks the result and derives the hints
    /// anew; if a crate doesn't hold there, Core refuses and the plan stays, with the reason the preview showed in a
    /// notice line (D24). Saved like every change of <see cref="Plan"/>. One record as parameter: a generated command
    /// takes at most one.
    /// </summary>
    [RelayCommand]
    private void MoveCrates(Gesture.MoveCrates move) =>
        TryMove(plan => LoadPlanner.MoveCrates(plan, Ship, CrateDrop.Moves(Ship, move) ?? throw new PlanException(CrateDrop.OutsideRacks)));

    /// <summary>Puts a whole stop into a rack, run by a right drag of its legend line into the ship view (D25); like <see cref="MoveCrates"/>.</summary>
    [RelayCommand]
    private void MoveStop(Gesture.MoveStop move) => TryMove(plan => LoadPlanner.MoveStop(plan, Ship, move.Destination, move.TargetRackId));

    private void TryMove(Func<LoadPlan, LoadPlan> move)
    {
        // Unreachable without a plan: the view has no cargo and the legend no lines to drag then.
        var plan = Plan ?? throw new InvalidOperationException(NoPlanText);
        try
        {
            Plan = move(plan);
            SetNotice(NoticeSource.Move, null);
        }
        catch (PlanException ex)
        {
            // Only "doesn't fit": the player's call, shown, not a bug. Other exceptions are bugs and stay unhandled.
            SetNotice(NoticeSource.Move, MoveRefusedPrefix + ex.Message);
        }
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
        ? LoadPlanner.SplitProblem(new SavedPlan(Missions, plan), Ship, Selected.Single())
        : SelectOneCrateText;

    private string? MergeProblem() => Selected.Count >= 2 && Plan is { } plan
        ? LoadPlanner.MergeProblem(new SavedPlan(Missions, plan), Ship, [.. Selected])
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
            var saved = edit(new SavedPlan(Missions, plan));
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

    // Runs before PropertyChanged for Plan and Scene, so bindings read the new scene.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty#running-code-upon-changes
    partial void OnPlanChanged(LoadPlan? value) => Scene = value is null
        ? EmptyScene(Ship)
        : ShipViewBuilder.Build(value, Ship, LoadPlanner.Validate(value, Ship));

    private static ShipScene EmptyScene(Ship ship) => ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);
}
