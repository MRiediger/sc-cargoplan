using CargoPlan.App.Hotkeys;
using CargoPlan.App.Settings;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// The Hotkeys section of the settings window (T4.4, D35): both actions, so one can refuse the other's combination by
/// name before Windows would. Still no defaults (D17).
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
    }

    public HotkeyEditor Lock { get; }

    public HotkeyEditor Overlay { get; }

    private string? Commit(HotkeyEditor editor, Hotkey? hotkey)
    {
        var other = ReferenceEquals(editor, Lock) ? Overlay : Lock;
        return hotkey is not null && hotkey == other.Hotkey
            ? $"{hotkey} is already used for {other.Label}."
            : _set(editor.Action, hotkey);
    }
}
