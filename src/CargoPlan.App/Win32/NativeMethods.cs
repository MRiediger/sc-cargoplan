using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CargoPlan.App.Win32;

/// <summary>
/// Win32 calls for the overlay window. Only our own window is touched, never the game process (EAC).
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

    /// <exception cref="Win32Exception">The call failed.</exception>
    public static nint GetExStyle(nint hwnd) => Checked(() => GetWindowLongPtrW(hwnd, GWL_EXSTYLE));

    /// <exception cref="Win32Exception">The call failed.</exception>
    public static void SetExStyle(nint hwnd, nint exStyle) => Checked(() => SetWindowLongPtrW(hwnd, GWL_EXSTYLE, exStyle));

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
}
