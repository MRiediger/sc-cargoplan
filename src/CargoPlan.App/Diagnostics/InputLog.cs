using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CargoPlan.App.Win32;

namespace CargoPlan.App.Diagnostics;

/// <summary>
/// <c>--log-input</c> (T13.3a): the overlay's activation, capture, button and wheel messages, and WPF's mouse capture after
/// each left press, in cargoplan.log. For the scroll check over the game: who has the foreground, whether a press reaches
/// the window, whether the thumb keeps the capture until the release. Mouse moves are left out, they would flood the log.
/// Each line says whether the overlay is the foreground window and its ex-style. Observes only; never changes a message.
/// </summary>
internal static class InputLog
{
    private const string Context = "Input";

    // Sources: https://learn.microsoft.com/windows/win32/inputdev/wm-activate
    //          https://learn.microsoft.com/windows/win32/inputdev/wm-mouseactivate
    //          https://learn.microsoft.com/windows/win32/inputdev/wm-capturechanged
    //          https://learn.microsoft.com/windows/win32/inputdev/wm-lbuttondown, wm-lbuttonup
    //          https://learn.microsoft.com/windows/win32/inputdev/wm-mousewheel, wm-mousehwheel
    private const int WM_ACTIVATE = 0x0006;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int WM_CAPTURECHANGED = 0x0215;

    // WM_ACTIVATE's low word: WA_INACTIVE, WA_ACTIVE, WA_CLICKACTIVE. https://learn.microsoft.com/windows/win32/inputdev/wm-activate
    private static readonly string[] ActivateStates = ["inactive", "active", "click-active"];

    private const int LowWordMask = 0xFFFF;

    /// <summary>Logs <paramref name="window"/>'s input from its HWND on; call before Show.</summary>
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook((nint h, int msg, nint wParam, nint lParam, ref bool handled) =>
            {
                if (Describe(msg, wParam, lParam) is { } text)
                {
                    Write(hwnd, text);
                }

                return 0;
            });
            Write(hwnd, "attached");
        };

        // Handled ones too: the thumb and the track's buttons handle the press and take the capture.
        window.AddHandler(Mouse.GotMouseCaptureEvent, new MouseEventHandler((_, e) => Write(window, $"WPF capture got by {e.OriginalSource.GetType().Name}")), handledEventsToo: true);
        window.AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler((_, e) => Write(window, $"WPF capture lost by {e.OriginalSource.GetType().Name}")), handledEventsToo: true);
        window.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            var pressed = e.OriginalSource.GetType().Name;
            // After the press is handled: who holds the capture now.
            window.Dispatcher.InvokeAsync(
                () => Write(window, $"left press on {pressed}, then Mouse.Captured = {Mouse.Captured?.GetType().Name ?? "none"}"),
                DispatcherPriority.Input);
        }), handledEventsToo: true);
    }

    private static string? Describe(int msg, nint wParam, nint lParam) => msg switch
    {
        WM_ACTIVATE => $"WM_ACTIVATE {ActivateState(wParam)}",
        WM_MOUSEACTIVATE => "WM_MOUSEACTIVATE",
        WM_CAPTURECHANGED => string.Create(CultureInfo.InvariantCulture, $"WM_CAPTURECHANGED to 0x{lParam:X}"),
        WM_LBUTTONDOWN => "WM_LBUTTONDOWN",
        WM_LBUTTONUP => "WM_LBUTTONUP",
        WM_MOUSEWHEEL => string.Create(CultureInfo.InvariantCulture, $"WM_MOUSEWHEEL {HighWord(wParam)}"),
        WM_MOUSEHWHEEL => string.Create(CultureInfo.InvariantCulture, $"WM_MOUSEHWHEEL {HighWord(wParam)}"),
        _ => null,
    };

    private static string ActivateState(nint wParam) => (int)(wParam & LowWordMask) is var state && state < ActivateStates.Length ? ActivateStates[state] : state.ToString(CultureInfo.InvariantCulture);

    // The wheel's delta is the signed high word of wParam.
    private static short HighWord(nint wParam) => unchecked((short)((long)wParam >> 16));

    private static void Write(Window window, string text) => Write(new WindowInteropHelper(window).Handle, text);

    // The ex-style on every line: it changes with lock and tab (D11 addendum T13.3a).
    private static void Write(nint hwnd, string text) =>
        AppLog.Write(Context, string.Create(CultureInfo.InvariantCulture,
            $"{text} | foreground: {(hwnd != 0 && NativeMethods.GetForegroundWindow() == hwnd ? "overlay" : "other")} | ex-style 0x{(hwnd != 0 ? NativeMethods.GetExStyle(hwnd) : 0):X8}"));
}
