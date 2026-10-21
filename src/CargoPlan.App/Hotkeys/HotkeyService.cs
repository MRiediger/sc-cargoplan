using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App.Hotkeys;

/// <summary>
/// Global hotkeys on the overlay's window: work while Star Citizen has the focus and hides the cursor (D17).
/// RegisterHotKey is a plain Windows service, no keyboard hook, nothing in the game process (EAC).
/// Registered once the HWND exists, freed when the overlay closes; hiding the overlay keeps the HWND and the hotkeys.
/// The settings window changes them while the app runs (T4.4, D35).
/// </summary>
public sealed class HotkeyService
{
    private readonly Window _window;
    private readonly OverlayViewModel _viewModel;
    private readonly Dictionary<HotkeyAction, ICommand> _commands;

    // What should be registered, what is, and what went wrong, per action. Wanted and registered differ while suspended
    // or when another program has the combination.
    private readonly Dictionary<HotkeyAction, Hotkey> _wanted = [];
    private readonly HashSet<HotkeyAction> _registered = [];
    private readonly Dictionary<HotkeyAction, string> _problems = [];

    private HwndSource? _source;
    private nint _hwnd;
    private bool _suspended;

    /// <param name="window">Not shown yet: registers on its SourceInitialized.</param>
    /// <param name="settings">null or a null entry = that hotkey is not set (no defaults, D17).</param>
    /// <param name="syncContractTracker">The editor's "Sync from ContractTracker", which lives in the composition root, not in the view model (D44).</param>
    public HotkeyService(Window window, HotkeySettings? settings, OverlayViewModel viewModel, ICommand syncContractTracker)
    {
        _window = window;
        _viewModel = viewModel;
        _commands = new()
        {
            [HotkeyAction.ToggleLock] = viewModel.ToggleLockCommand,
            [HotkeyAction.ToggleOverlay] = viewModel.ToggleOverlayCommand,
            [HotkeyAction.SyncContractTracker] = syncContractTracker,
        };

        (HotkeyAction, string?)[] texts =
        [
            (HotkeyAction.ToggleLock, settings?.ToggleLock),
            (HotkeyAction.ToggleOverlay, settings?.ToggleOverlay),
            (HotkeyAction.SyncContractTracker, settings?.SyncContractTracker),
        ];
        foreach (var (action, text) in texts)
        {
            if (text is null)
            {
                continue;
            }

            if (HotkeyParser.TryParse(text, out var hotkey, out var error))
            {
                _wanted[action] = hotkey;
            }
            else
            {
                _problems[action] = $"Hotkey {Name(action)} \"{text}\" ignored: {error}.";
            }
        }

        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnClosed;
    }

    /// <summary>
    /// From the settings window: the old hotkey of <paramref name="action"/> goes, the new one is registered; null clears.
    /// </summary>
    /// <returns>null = registered (or cleared); otherwise why not, and the old hotkey stays registered (D35).</returns>
    public string? TrySet(HotkeyAction action, Hotkey? hotkey)
    {
        var old = _wanted.GetValueOrDefault(action);
        Unregister(action);

        if (hotkey is not null && IsActive && !Register(action, hotkey))
        {
            if (old is not null && !Register(action, old))
            {
                _problems[action] = InUse(action, old);
                UpdateNotice();
            }

            return $"{hotkey} is in use by another program; pick another one.";
        }

        if (hotkey is null)
        {
            _wanted.Remove(action);
        }
        else
        {
            _wanted[action] = hotkey;
        }

        _problems.Remove(action);
        UpdateNotice();
        return null;
    }

    /// <summary>
    /// Frees our hotkeys while the settings window records a combination: Windows turns a registered combination into
    /// WM_HOTKEY, so its keys would never reach the window, and pressing the lock's keys would switch the lock instead.
    /// </summary>
    public void Suspend()
    {
        if (_suspended)
        {
            return;
        }

        _suspended = true;
        foreach (var action in _registered.ToList())
        {
            Unregister(action);
        }
    }

    /// <summary>Registers again what is wanted; a combination taken meanwhile shows in the notice line.</summary>
    public void Resume()
    {
        if (!_suspended)
        {
            return;
        }

        _suspended = false;
        RegisterWanted();
    }

    // Registering needs the HWND; while suspended nothing is registered.
    private bool IsActive => _hwnd != 0 && !_suspended;

    // RegisterHotKey needs the window handle, which exists from SourceInitialized on.
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _window.SourceInitialized -= OnSourceInitialized;
        _hwnd = new WindowInteropHelper(_window).Handle;

        // Always hooked: the settings window can add a hotkey later.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.interop.hwndsource.addhook
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
        RegisterWanted();
    }

    private void RegisterWanted()
    {
        foreach (var (action, hotkey) in _wanted)
        {
            if (Register(action, hotkey))
            {
                _problems.Remove(action);
            }
            else
            {
                _problems[action] = InUse(action, hotkey);
            }
        }

        UpdateNotice();
    }

    private bool Register(HotkeyAction action, Hotkey hotkey)
    {
        if (!NativeMethods.TryRegisterHotKey(_hwnd, Id(action), hotkey))
        {
            return false;
        }

        _registered.Add(action);
        return true;
    }

    private void Unregister(HotkeyAction action)
    {
        if (_registered.Remove(action))
        {
            NativeMethods.UnregisterHotKey(_hwnd, Id(action));
        }
    }

    private void UpdateNotice() => _viewModel.SetNotice(
        NoticeSource.Hotkeys,
        _problems.Count == 0 ? null : string.Join(Environment.NewLine, _problems.OrderBy(p => p.Key).Select(p => p.Value)));

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _commands.FirstOrDefault(c => Id(c.Key) == (int)wParam).Value is { } command)
        {
            command.Execute(null);
            handled = true;
        }

        return 0;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _window.SourceInitialized -= OnSourceInitialized;
        _window.Closed -= OnClosed;
        _source?.RemoveHook(WndProc);

        foreach (var action in _registered.ToList())
        {
            Unregister(action);
        }
    }

    // Any id in 0x0000..0xBFFF is fine for an application; Windows sends it back as WM_HOTKEY's wParam. 0 stays unused.
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey (Remarks)
    private static int Id(HotkeyAction action) => (int)action + 1;

    // Names as in settings.json, so the notice points at the line to fix.
    private static string Name(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleLock => "toggleLock",
        HotkeyAction.ToggleOverlay => "toggleOverlay",
        HotkeyAction.SyncContractTracker => "syncContractTracker",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private static string InUse(HotkeyAction action, Hotkey hotkey) =>
        $"Hotkey {hotkey} ({Name(action)}) is already in use; pick another one in the settings window.";
}
