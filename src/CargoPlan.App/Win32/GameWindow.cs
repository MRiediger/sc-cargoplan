using System.ComponentModel;
using System.Diagnostics;
using CargoPlan.Ocr;

namespace CargoPlan.App.Win32;

/// <summary>
/// Star Citizen's window and where its client area lies on the screen, in physical pixels (T10.1b). "Not running" and
/// "minimised" are a null result, not an exception: that is the normal case outside the game.
/// </summary>
/// <param name="Handle">The window; read only, nothing is sent to it (EAC).</param>
/// <param name="Client">The client area on the screen: the game's picture, without title bar or borders in windowed mode.</param>
internal sealed record GameWindow(nint Handle, PixelRect Client)
{
    /// <summary>Where the contract is read, on the screen (<see cref="ScanRegion"/>, relative to the client area).</summary>
    public PixelRect ScanRegionOnScreen => RegionOnScreen(Client);

    /// <summary>The game's window, or null when it doesn't run, is minimised or has no client area.</summary>
    public static GameWindow? Find() => Find(AutoLockRule.GameProcessName);

    /// <summary>
    /// The foreground window if it is the game's (the normal case behind a hotkey), else the main window of the first process
    /// of that name. The process name as a parameter keeps the default test independent of a running game.
    /// </summary>
    internal static GameWindow? Find(string processName)
    {
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        var handle = ForegroundWindowOf(processName) ?? MainWindowOf(processName);
        if (handle is not { } window || NativeMethods.GetClientOnScreen(window) is not { } client)
        {
            return null;
        }

        return new GameWindow(window, new PixelRect(client.X, client.Y, client.Width, client.Height));
    }

    /// <summary>The scan region of a client area that lies at <paramref name="client"/> on the screen.</summary>
    internal static PixelRect RegionOnScreen(PixelRect client)
    {
        var region = ScanRegion.Of(client.Width, client.Height);
        return region with { X = client.X + region.X, Y = client.Y + region.Y };
    }

    private static nint? ForegroundWindowOf(string processName)
    {
        var window = NativeMethods.GetForegroundWindow();
        return string.Equals(CursorStateMonitor.ProcessNameOf(window), processName, StringComparison.OrdinalIgnoreCase)
            ? window
            : null;
    }

    private static nint? MainWindowOf(string processName)
    {
        // Each Process holds a handle: all disposed, the one found too. MainWindowHandle is 0 for a process without a
        // visible main window; a process that exits meanwhile throws InvalidOperationException, Win32Exception without access.
        // Source: https://learn.microsoft.com/dotnet/api/system.diagnostics.process.mainwindowhandle
        var processes = Process.GetProcessesByName(processName);
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.MainWindowHandle is not 0 and var window)
                    {
                        return window;
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Gone or not ours to read: not the game window, the next process may be.
                    AppLog.Write("Game window", $"process {processName} skipped: {ex.Message}");
                }
            }

            return null;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
