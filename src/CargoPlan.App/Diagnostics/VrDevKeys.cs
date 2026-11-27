using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Vr;
using CargoPlan.App.Win32;

namespace CargoPlan.App.Diagnostics;

/// <summary>
/// The developer's keys of the VR mode (<c>--vr-dev</c>, T14.1) until T14.3b brings the toggleVr hotkey: Ctrl+Alt+V
/// toggles VR, Ctrl+Alt+B blocks the UI thread for 20 s, so the gate can close SteamVR while the UI hangs and see the quit
/// acknowledged anyway; Ctrl+Alt+F switches the feed's forced mode (T14.2, K7: a picture every tick, for the memory
/// plateau and the flicker check). Always armed, whatever is in front: a developer's tool started on purpose (like the scan
/// probe).
/// </summary>
internal sealed class VrDevKeys
{
    /// <summary>Long enough to close SteamVR by its window and wait for the quit (E1 of the T14.1 approval).</summary>
    public static readonly TimeSpan BlockFor = TimeSpan.FromSeconds(20);

    // Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint VK_B = 0x42;
    private const uint VK_F = 0x46;
    private const uint VK_V = 0x56;

    private readonly OverlayViewModel _viewModel;
    private readonly VrMirror _mirror;
    private readonly HotkeyPoller<DevKey> _poller = new(NativeMethods.IsKeyDown);
    private readonly DispatcherTimer _timer = new() { Interval = HotkeyService.PollInterval };

    private enum DevKey
    {
        Toggle,
        BlockUi,
        Force,
    }

    /// <param name="window">The overlay: the keys stop polling when it closes.</param>
    public VrDevKeys(OverlayViewModel viewModel, Window window, VrMirror mirror)
    {
        _viewModel = viewModel;
        _mirror = mirror;
        _poller.Set(DevKey.Toggle, new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, VK_V));
        _poller.Set(DevKey.BlockUi, new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, VK_B));
        _poller.Set(DevKey.Force, new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, VK_F));
        _timer.Tick += OnTick;
        _timer.Start();
        window.Closed += (_, _) => _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        foreach (var key in _poller.Poll(armed: true))
        {
            if (key is DevKey.Toggle)
            {
                _viewModel.ToggleVrCommand.Execute(null);
                continue;
            }

            if (key is DevKey.Force)
            {
                // Before the first VR on there is no feed yet: nothing to force.
                if (_mirror.Feed is { } feed)
                {
                    feed.Forced = !feed.Forced;
                    AppLog.Write(OpenVrSession.LogContext, $"forced feed {(feed.Forced ? "on" : "off")} (Ctrl+Alt+F)");
                }

                continue;
            }

            // On purpose: the UI thread hangs as a hung WM_PRINT would hold it (S14.0); the VR thread must not care.
            AppLog.Write(OpenVrSession.LogContext, $"blocking the UI thread for {BlockFor.TotalSeconds:F0} s (Ctrl+Alt+B)");
            Thread.Sleep(BlockFor);
            AppLog.Write(OpenVrSession.LogContext, "the UI thread runs again");
        }
    }
}
