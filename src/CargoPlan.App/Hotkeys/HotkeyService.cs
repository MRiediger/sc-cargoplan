using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App.Hotkeys;

/// <summary>
/// Global hotkeys that work while Star Citizen has the focus and hides the cursor (D17), polled from the key state (D97):
/// RegisterHotKey's WM_HOTKEY never arrives with the game in front. No hook, nothing in the game process (EAC).
/// Fires only with the game or one of our windows in front; the key reaches the game too, so the player picks one the game
/// doesn't use. Polls from construction until the overlay closes; hiding the overlay keeps it polling.
/// The settings window changes the hotkeys while the app runs (T4.4, D35).
/// </summary>
public sealed class HotkeyService
{
    /// <summary>30 Hz: a press of 50 ms or more is seen for sure; presses typically last 80–150 ms.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(33);

    private readonly Window _window;
    private readonly OverlayViewModel _viewModel;
    private readonly Dictionary<HotkeyAction, ICommand> _commands;
    private readonly HotkeyPoller<HotkeyAction> _poller = new(NativeMethods.IsKeyDown);
    private readonly DispatcherTimer _timer = new() { Interval = PollInterval };

    // The hotkeys set, for the log line, and the settings.json entries that were invalid, for the notice line.
    private readonly Dictionary<HotkeyAction, Hotkey> _hotkeys = [];
    private readonly Dictionary<HotkeyAction, string> _problems = [];

    // GetForegroundWindow is cheap, resolving the process is not: only on a window change, as in CursorStateMonitor.
    private nint _lastWindow;
    private bool _ownProcessInFront;
    private string? _lastProcessName;

    private bool _suspended;

    // ContractTracker on the scan's F3 (D104), shown below the hotkey problems.
    private readonly ScanKeyCheck _scanKeyCheck;

    /// <param name="window">The overlay: polling stops when it closes.</param>
    /// <param name="settings">A null entry = that hotkey is not set; null = <see cref="HotkeySettings.Default"/>, the scan's F3 only (D17, D104).</param>
    /// <param name="scanContracts">The scan (T10.3); a plain command that always runs, so a second press reaches the scanner and says "Scan running".</param>
    /// <param name="scanKeyCheck">Looks for ContractTracker on F3 at the start, on each scan press and when the scan key changes.</param>
    public HotkeyService(
        Window window, HotkeySettings? settings, OverlayViewModel viewModel, ICommand scanContracts, ScanKeyCheck scanKeyCheck)
    {
        _window = window;
        _viewModel = viewModel;
        _scanKeyCheck = scanKeyCheck;
        _commands = new()
        {
            [HotkeyAction.ToggleLock] = viewModel.ToggleLockCommand,
            [HotkeyAction.ToggleOverlay] = viewModel.ToggleOverlayCommand,
            [HotkeyAction.Replan] = viewModel.ReplanCommand,
            [HotkeyAction.DiscardPlan] = viewModel.DiscardPlanCommand,
            [HotkeyAction.Undo] = viewModel.UndoCommand,
            [HotkeyAction.Redo] = viewModel.RedoCommand,
            [HotkeyAction.ResetView] = viewModel.ResetViewCommand,
            [HotkeyAction.ScanContracts] = scanContracts,
        };

        settings ??= HotkeySettings.Default;
        (HotkeyAction, string?)[] texts =
        [
            (HotkeyAction.ToggleLock, settings.ToggleLock),
            (HotkeyAction.ToggleOverlay, settings.ToggleOverlay),
            (HotkeyAction.Replan, settings.Replan),
            (HotkeyAction.DiscardPlan, settings.DiscardPlan),
            (HotkeyAction.Undo, settings.Undo),
            (HotkeyAction.Redo, settings.Redo),
            (HotkeyAction.ResetView, settings.ResetView),
            (HotkeyAction.ScanContracts, settings.ScanContracts),
        ];
        foreach (var (action, text) in texts)
        {
            if (text is null)
            {
                continue;
            }

            if (HotkeyParser.TryParse(text, out var hotkey, out var error))
            {
                _hotkeys[action] = hotkey;
                _poller.Set(action, hotkey);
            }
            else
            {
                _problems[action] = $"Hotkey {Name(action)} \"{text}\" ignored: {error}.";
            }
        }

        CheckScanKey();
        _timer.Tick += OnTick;
        _timer.Start();
        window.Closed += OnClosed;
    }

    /// <summary>From the settings window: <paramref name="action"/> gets <paramref name="hotkey"/>; null clears.</summary>
    public void Set(HotkeyAction action, Hotkey? hotkey)
    {
        if (hotkey is null)
        {
            _hotkeys.Remove(action);
        }
        else
        {
            _hotkeys[action] = hotkey;
        }

        _poller.Set(action, hotkey);
        _problems.Remove(action);
        CheckScanKey();
    }

    /// <summary>
    /// No hotkey fires while the settings window records a combination: it is our own window in front, and pressing the
    /// lock's keys there would switch the lock instead of recording them.
    /// </summary>
    public void Suspend() => _suspended = true;

    public void Resume() => _suspended = false;

    /// <summary>The hotkey problems, then ContractTracker on the scan key; one line each.</summary>
    private void CheckScanKey()
    {
        var shared = _scanKeyCheck.Check(_hotkeys.GetValueOrDefault(HotkeyAction.ScanContracts));
        string[] lines = [.. _problems.OrderBy(p => p.Key).Select(p => p.Value), .. shared is null ? Array.Empty<string>() : [shared]];
        _viewModel.SetNotice(NoticeSource.Hotkeys, lines.Length == 0 ? null : string.Join(Environment.NewLine, lines));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window != _lastWindow)
        {
            _lastWindow = window;
            _ownProcessInFront = NativeMethods.GetWindowProcessId(window) == Environment.ProcessId;
            _lastProcessName = _ownProcessInFront ? null : CursorStateMonitor.ProcessNameOf(window);
        }

        // Keyboard.FocusedElement is the focus in this thread's active window; editor cells edit in a TextBox too.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.input.keyboard.focusedelement
        var textInputFocused = Keyboard.FocusedElement is TextBoxBase or PasswordBox;
        var armed = HotkeyPoller.IsArmed(_suspended, textInputFocused, _ownProcessInFront, _lastProcessName);
        foreach (var action in _poller.Poll(armed))
        {
            // The toolkit's commands don't check CanExecute in Execute; a button's binding does, a hotkey has to: a
            // re-plan without cargo on board does nothing.
            // Source: https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.Mvvm/Input/RelayCommand.cs (Execute)
            var command = _commands[action];
            var canExecute = command.CanExecute(null);
            AppLog.Write("Hotkey", $"{_hotkeys[action]} → {Name(action)}{(canExecute ? "" : " (not available now)")}");
            if (action == HotkeyAction.ScanContracts)
            {
                // ContractTracker may have started or closed since (D104).
                CheckScanKey();
            }

            if (canExecute)
            {
                command.Execute(null);
            }
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _window.Closed -= OnClosed;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    // Names as in settings.json, so the notice points at the line to fix.
    private static string Name(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleLock => "toggleLock",
        HotkeyAction.ToggleOverlay => "toggleOverlay",
        HotkeyAction.Replan => "replan",
        HotkeyAction.DiscardPlan => "discardPlan",
        HotkeyAction.Undo => "undo",
        HotkeyAction.Redo => "redo",
        HotkeyAction.ResetView => "resetView",
        HotkeyAction.ScanContracts => "scanContracts",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };
}
