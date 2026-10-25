using CargoPlan.App.Hotkeys;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// One hotkey in the settings window (T4.4, D35): click, press the keys, done. WPF-free; the window turns key events into
/// modifiers and a virtual-key code and calls <see cref="Capture"/>. A refused combination keeps the old hotkey.
/// </summary>
public sealed partial class HotkeyEditor : ObservableObject
{
    public const string LockLabel = "Lock / unlock";

    public const string OverlayLabel = "Hide / show overlay";

    public const string SyncLabel = "Sync from ContractTracker";

    public const string ReplanLabel = "Re-plan";

    public const string DiscardLabel = "Discard plan";

    public const string UndoLabel = "Undo";

    public const string RedoLabel = "Redo";

    public const string NotSetText = "Not set";

    public const string RecordingText = "Press the keys…";

    /// <summary>Shown while recording: a combination another program or Windows has registered never reaches this window.</summary>
    public const string RecordingHint = "Esc cancels. Nothing happens? Another program or Windows uses that combination.";

    private readonly Func<HotkeyEditor, Hotkey?, string?> _commit;
    private readonly Action _suspend;
    private readonly Action _resume;

    /// <param name="text">From settings.json; null = not set (D17), invalid = not set with the reason shown.</param>
    /// <param name="commit">Registers and saves, or returns why not (<see cref="HotkeysViewModel"/>).</param>
    /// <param name="suspend">Frees our registered hotkeys while recording, or their keys would never reach the window.</param>
    /// <param name="resume">Registers them again.</param>
    public HotkeyEditor(HotkeyAction action, string label, string? text, Func<HotkeyEditor, Hotkey?, string?> commit, Action suspend, Action resume)
    {
        Action = action;
        Label = label;
        _commit = commit;
        _suspend = suspend;
        _resume = resume;

        if (text is null)
        {
            return;
        }

        if (HotkeyParser.TryParse(text, out var hotkey, out var error))
        {
            Hotkey = hotkey;
        }
        else
        {
            Problem = $"\"{text}\" in settings.json ignored: {error}.";
        }
    }

    public HotkeyAction Action { get; }

    public string Label { get; }

    /// <summary>Registered and saved; null = not set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    public partial Hotkey? Hotkey { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    public partial bool IsRecording { get; private set; }

    /// <summary>Why the last combination was refused; null = nothing to say.</summary>
    [ObservableProperty]
    public partial string? Problem { get; private set; }

    /// <summary>The button's text.</summary>
    public string Display => IsRecording ? RecordingText : Hotkey?.ToString() ?? NotSetText;

    /// <summary>
    /// A key pressed while recording. Modifier keys alone never get here (the window waits for the key after them).
    /// Refused by the rules: the reason shows and recording goes on. Valid: recording ends, then it is committed.
    /// </summary>
    public void Capture(HotkeyModifiers modifiers, uint virtualKey)
    {
        if (!IsRecording)
        {
            return;
        }

        if (!HotkeyParser.TryCreate(modifiers, virtualKey, out var hotkey, out var error))
        {
            Problem = $"Refused: {error}.";
            return;
        }

        Stop();
        Commit(hotkey);
    }

    /// <summary>Esc or the focus gone (window hidden or closed): the hotkey stays as it was.</summary>
    public void Cancel()
    {
        if (IsRecording)
        {
            Stop();
        }
    }

    [RelayCommand]
    private void Record()
    {
        if (IsRecording)
        {
            return;
        }

        Problem = null;
        IsRecording = true;
        _suspend();
    }

    /// <summary>No hotkey for this action; written as null (settings-format.md).</summary>
    [RelayCommand]
    private void Clear()
    {
        Cancel();
        Commit(null);
    }

    // Resumed before committing: the commit registers the new hotkey next to the other action's.
    private void Stop()
    {
        IsRecording = false;
        _resume();
    }

    private void Commit(Hotkey? hotkey)
    {
        Problem = _commit(this, hotkey);
        if (Problem is null)
        {
            Hotkey = hotkey;
        }
    }
}
