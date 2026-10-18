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
/// </summary>
public sealed class HotkeyService
{
    // Any id in 0x0000..0xBFFF is fine for an application; Windows sends it back as WM_HOTKEY's wParam.
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey (Remarks)
    private const int ToggleLockId = 1;
    private const int ToggleOverlayId = 2;

    private readonly Window _window;
    private readonly OverlayViewModel _viewModel;
    private readonly List<(int Id, string Name, string? Text, ICommand Command)> _actions;
    private readonly Dictionary<int, ICommand> _registered = [];
    private HwndSource? _source;
    private nint _hwnd;

    /// <param name="window">Not shown yet: registers on its SourceInitialized.</param>
    /// <param name="settings">null or a null entry = that hotkey is not set (no defaults, D17).</param>
    public HotkeyService(Window window, HotkeySettings? settings, OverlayViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;
        // Names as in settings.json, so the notice points at the line to fix.
        _actions =
        [
            (ToggleLockId, "toggleLock", settings?.ToggleLock, viewModel.ToggleLockCommand),
            (ToggleOverlayId, "toggleOverlay", settings?.ToggleOverlay, viewModel.ToggleOverlayCommand),
        ];

        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnClosed;
    }

    // RegisterHotKey needs the window handle, which exists from SourceInitialized on.
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _window.SourceInitialized -= OnSourceInitialized;
        var hwnd = _hwnd = new WindowInteropHelper(_window).Handle;

        List<string> problems = [];
        foreach (var (id, name, text, command) in _actions)
        {
            if (text is null)
            {
                continue;
            }

            if (!HotkeyParser.TryParse(text, out var hotkey, out var error))
            {
                problems.Add($"Hotkey {name} \"{text}\" ignored: {error}.");
            }
            else if (!NativeMethods.TryRegisterHotKey(hwnd, id, hotkey))
            {
                problems.Add($"Hotkey {hotkey} ({name}) is already in use; pick another one in settings.json.");
            }
            else
            {
                _registered[id] = command;
            }
        }

        _viewModel.HotkeyNotice = problems.Count == 0 ? null : string.Join(Environment.NewLine, problems);

        if (_registered.Count > 0)
        {
            // Source: https://learn.microsoft.com/dotnet/api/system.windows.interop.hwndsource.addhook
            _source = HwndSource.FromHwnd(hwnd);
            _source.AddHook(WndProc);
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _registered.TryGetValue((int)wParam, out var command))
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

        foreach (var id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }

        _registered.Clear();
    }
}
