using CargoPlan.App.Hotkeys;
using CargoPlan.App.Settings;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// The Hotkeys section of the settings window (T4.4, D35): every action, so one can refuse another's combination by
/// name before Windows would. Still no defaults (D17); the sync came with T5.5 (D44), the re-plan with B4 (D66), the
/// discard with K2.3.
/// </summary>
public sealed class HotkeysViewModel
{
    public const string Hint = "Ctrl, Alt or Win required. Win combinations may be taken by Windows itself. Pick keys Star Citizen doesn't use.";

    private readonly Func<HotkeyAction, Hotkey?, string?> _set;

    /// <param name="settings">As the session holds them; null = none set.</param>
    /// <param name="set">Registers the hotkey and saves it, or returns why not (another program has it); null clears.</param>
    /// <param name="suspend">Frees our registered hotkeys while a combination is recorded.</param>
    /// <param name="resume">Registers them again.</param>
    public HotkeysViewModel(HotkeySettings? settings, Func<HotkeyAction, Hotkey?, string?> set, Action suspend, Action resume)
    {
        _set = set;
        Lock = new(HotkeyAction.ToggleLock, HotkeyEditor.LockLabel, settings?.ToggleLock, Commit, suspend, resume);
        Overlay = new(HotkeyAction.ToggleOverlay, HotkeyEditor.OverlayLabel, settings?.ToggleOverlay, Commit, suspend, resume);
        Sync = new(HotkeyAction.SyncContractTracker, HotkeyEditor.SyncLabel, settings?.SyncContractTracker, Commit, suspend, resume);
        Replan = new(HotkeyAction.Replan, HotkeyEditor.ReplanLabel, settings?.Replan, Commit, suspend, resume);
        Discard = new(HotkeyAction.DiscardPlan, HotkeyEditor.DiscardLabel, settings?.DiscardPlan, Commit, suspend, resume);
    }

    public HotkeyEditor Lock { get; }

    public HotkeyEditor Overlay { get; }

    public HotkeyEditor Sync { get; }

    public HotkeyEditor Replan { get; }

    public HotkeyEditor Discard { get; }

    /// <summary>Every editor, in the window's order; the window cancels their recordings.</summary>
    public IEnumerable<HotkeyEditor> All => [Lock, Overlay, Sync, Replan, Discard];

    private string? Commit(HotkeyEditor editor, Hotkey? hotkey) =>
        hotkey is not null && All.FirstOrDefault(other => !ReferenceEquals(other, editor) && other.Hotkey == hotkey) is { } taken
            ? $"{hotkey} is already used for {taken.Label}."
            : _set(editor.Action, hotkey);
}
