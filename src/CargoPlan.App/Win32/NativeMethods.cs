using System.ComponentModel;
using System.Runtime.InteropServices;
using CargoPlan.App.Hotkeys;

namespace CargoPlan.App.Win32;

/// <summary>
/// Win32 calls for our own windows, plus read-only queries of the foreground window and the cursor.
/// Never anything in the game process (EAC).
/// The Ptr variants exist in 64-bit user32.dll only; the app runs as a 64-bit process.
/// </summary>
internal static partial class NativeMethods
{
    // Sources: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
    //          https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
    public const int GWL_EXSTYLE = -20;
    public const nint WS_EX_TRANSPARENT = 0x00000020;
    public const nint WS_EX_LAYERED = 0x00080000;
    public const nint WS_EX_NOACTIVATE = 0x08000000;
    public const nint WS_EX_APPWINDOW = 0x00040000;

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-cursorinfo
    public const uint CURSOR_SHOWING = 0x00000001;

    // Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
    public const int WM_HOTKEY = 0x0312;

    // Source: https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-
    private const int ERROR_ACCESS_DENIED = 5;

    // Source: https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--1300-1699-
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    /// <exception cref="Win32Exception">The call failed.</exception>
    public static nint GetExStyle(nint hwnd) => Checked(() => GetWindowLongPtrW(hwnd, GWL_EXSTYLE));

    /// <exception cref="Win32Exception">The call failed.</exception>
    public static void SetExStyle(nint hwnd, nint exStyle) => Checked(() => SetWindowLongPtrW(hwnd, GWL_EXSTYLE, exStyle));

    /// <summary>
    /// On: WS_EX_TRANSPARENT sends hit-tests to the window below. Off: only that bit goes;
    /// WS_EX_LAYERED stays, WPF owns it for AllowsTransparency and removing it breaks rendering (D11).
    /// </summary>
    /// <exception cref="Win32Exception">The call failed.</exception>
    public static void SetClickThrough(nint hwnd, bool clickThrough)
    {
        var style = GetExStyle(hwnd);
        var newStyle = clickThrough
            ? style | WS_EX_TRANSPARENT | WS_EX_LAYERED
            : style & ~WS_EX_TRANSPARENT;

        if (newStyle != style)
        {
            SetExStyle(hwnd, newStyle);
        }
    }

    /// <summary>
    /// WS_EX_NOACTIVATE: a click reaches the window but doesn't make it the foreground window, so Star Citizen keeps
    /// the foreground and the free cursor at the terminal (D11, addenda T3.2 and T3.6). Such a window leaves the
    /// taskbar unless WS_EX_APPWINDOW is set too, hence <paramref name="showInTaskbar"/>.
    /// </summary>
    /// <exception cref="Win32Exception">The call failed.</exception>
    public static void PreventActivation(nint hwnd, bool showInTaskbar)
    {
        var style = GetExStyle(hwnd) | WS_EX_NOACTIVATE;
        SetExStyle(hwnd, showInTaskbar ? style | WS_EX_APPWINDOW : style);
    }

    /// <summary>The window the user works with; 0 while activation switches.</summary>
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    /// <summary>Process that created <paramref name="hwnd"/>; null for 0 or a window that is gone by now.</summary>
    public static int? GetWindowProcessId(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        // Returns the thread id, 0 for an invalid handle; the process id comes through the out parameter.
        return GetWindowThreadProcessId(hwnd, out var processId) == 0 ? null : (int)processId;
    }

    /// <summary>
    /// Whether the system draws the cursor. Star Citizen hides it with ShowCursor in flight and first person
    /// (flags = 0, spike in D11 addendum T3.3).
    /// The secure desktop (Win+L, UAC, Ctrl+Alt+Del) denies access; the game gets no input there,
    /// so that counts as showing: no auto-lock. Any other failure is a bug (e.g. wrong cbSize) and throws.
    /// </summary>
    /// <exception cref="Win32Exception">The call failed for another reason than the secure desktop.</exception>
    public static bool IsCursorShowing()
    {
        CURSORINFO info = new() { cbSize = (uint)Marshal.SizeOf<CURSORINFO>() };
        if (GetCursorInfo(ref info))
        {
            return (info.flags & CURSOR_SHOWING) != 0;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error != ERROR_ACCESS_DENIED)
        {
            throw new Win32Exception(error);
        }

        return true;
    }

    /// <summary>
    /// Registers a system-wide hotkey for our own window: Windows posts WM_HOTKEY with <paramref name="id"/> as wParam,
    /// whichever program has the focus. No hook, nothing in another process (EAC, D17).
    /// false = another program (or our other hotkey) already has the combination; the player picks another one.
    /// </summary>
    /// <exception cref="Win32Exception">The call failed for another reason (a bug, e.g. a window of another thread).</exception>
    public static bool TryRegisterHotKey(nint hwnd, int id, Hotkey hotkey)
    {
        if (RegisterHotKey(hwnd, id, (uint)hotkey.Modifiers, hotkey.VirtualKey))
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error != ERROR_HOTKEY_ALREADY_REGISTERED)
        {
            throw new Win32Exception(error);
        }

        return false;
    }

    /// <exception cref="Win32Exception">The hotkey was not registered by this thread for this window.</exception>
    public static void UnregisterHotKey(nint hwnd, int id)
    {
        if (!UnregisterHotKeyNative(hwnd, id))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    // 0 is both "failed" and a valid previous value; only a non-zero last error after clearing it means failure.
    private static nint Checked(Func<nint> call)
    {
        Marshal.SetLastPInvokeError(0);
        var result = call();
        if (result == 0 && Marshal.GetLastPInvokeError() is var error and not 0)
        {
            throw new Win32Exception(error);
        }

        return result;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorinfo
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorInfo(ref CURSORINFO pci);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey
    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKeyNative(nint hWnd, int id);

    // Blittable layouts, field order as in winuser.h / windef.h.
    // Sources: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-cursorinfo
    //          https://learn.microsoft.com/en-us/windows/win32/api/windef/ns-windef-point
    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public uint cbSize;
        public uint flags;
        public nint hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }
}
