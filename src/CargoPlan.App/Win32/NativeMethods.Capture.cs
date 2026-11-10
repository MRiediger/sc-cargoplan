using System.Runtime.InteropServices;

namespace CargoPlan.App.Win32;

/// <summary>
/// Reading the game window's position and copying a rectangle of the screen (T10.1b): read-only queries of another
/// process's window and GDI on our own memory. Nothing is sent to or loaded into the game process (EAC).
/// </summary>
internal static partial class NativeMethods
{
    // Source: https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context
    private static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    // Raster operations, wingdi.h. Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt
    public const uint SRCCOPY = 0x00CC0020;
    public const uint CAPTUREBLT = 0x40000000;

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader
    private const uint BI_RGB = 0;

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection
    private const uint DIB_RGB_COLORS = 0;

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute (9th entry, the
    // enum starts at 1)
    private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguiresources
    private const uint GR_GDIOBJECTS = 0;

    // GetCurrentProcess() returns this pseudo-handle; it needs no closing.
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess
    private static readonly nint CurrentProcess = -1;

    /// <summary>
    /// Physical pixels for the calling thread until disposed (T10.1b): WPF runs system-DPI-aware, and such a thread gets
    /// another process's window coordinates scaled to the system DPI on a monitor with another scale. The old context comes
    /// back on dispose, so a thread-pool thread leaves as it came.
    /// Source: https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows
    /// ("Lack of documentation about virtualization")
    /// </summary>
    public readonly struct PhysicalPixelsScope : IDisposable
    {
        private readonly nint _previous;

        private PhysicalPixelsScope(nint previous) => _previous = previous;

        /// <summary>NULL back means the context was refused (Windows before 10 1703): coordinates stay as they were.</summary>
        public static PhysicalPixelsScope Enter() => new(SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2));

        public void Dispose()
        {
            if (_previous != 0)
            {
                SetThreadDpiAwarenessContext(_previous);
            }
        }
    }

    /// <summary>The client area in screen pixels; null when the window is gone, minimised or has no client area.</summary>
    public static (int X, int Y, int Width, int Height)? GetClientOnScreen(nint hwnd)
    {
        if (IsIconic(hwnd) || !GetClientRect(hwnd, out var client))
        {
            return null;
        }

        // The client rectangle's origin is always 0,0; ClientToScreen moves it to where it lies on the screen.
        POINT origin = new() { x = 0, y = 0 };
        if (!ClientToScreen(hwnd, ref origin))
        {
            return null;
        }

        var (width, height) = (client.right - client.left, client.bottom - client.top);
        return width > 0 && height > 0 ? (origin.x, origin.y, width, height) : null;
    }

    /// <summary>
    /// The window's bounds as DWM draws them, without the invisible resize borders but with the title bar; for the probe's
    /// log only (the scan region is placed from the client area). null if DWM doesn't answer.
    /// </summary>
    public static (int X, int Y, int Width, int Height)? GetFrameBounds(nint hwnd)
    {
        RECT bounds;
        unsafe
        {
            if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &bounds, (uint)sizeof(RECT)) != 0)
            {
                return null;
            }
        }

        return (bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top);
    }

    /// <summary>The window's rectangle in screen pixels (our own windows, for the overlap check); null if it is gone.</summary>
    public static (int X, int Y, int Width, int Height)? GetWindowBounds(nint hwnd) =>
        GetWindowRect(hwnd, out var r) ? (r.left, r.top, r.right - r.left, r.bottom - r.top) : null;

    /// <summary>How many GDI objects this process holds now: the leak check of the capture.</summary>
    public static uint GdiObjectCount() => GetGuiResources(CurrentProcess, GR_GDIOBJECTS);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext
    [LibraryImport("user32.dll")]
    private static partial nint SetThreadDpiAwarenessContext(nint dpiContext);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-isiconic
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint hWnd);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclientrect
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-clienttoscreen
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint hWnd, ref POINT lpPoint);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetwindowattribute
    [LibraryImport("dwmapi.dll")]
    private static unsafe partial int DwmGetWindowAttribute(nint hwnd, uint dwAttribute, void* pvAttribute, uint cbAttribute);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguiresources
    [LibraryImport("user32.dll")]
    private static partial uint GetGuiResources(nint hProcess, uint uiFlags);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc (NULL = the whole screen)
    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hWnd);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc
    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createcompatibledc
    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deletedc
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-selectobject
    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deleteobject
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);

    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-gdiflush
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GdiFlush();

    // PrintWindow flags, winuser.h (PW_RENDERFULLCONTENT since Windows 8.1, not on the function's page).
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow
    public const uint PW_CLIENTONLY = 0x00000001;
    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    /// <summary>
    /// The overlay measurement's variant V1 (T10.1b, D100). Synchronous, and the window's own process renders the picture
    /// (it receives WM_PRINT): with the game, a call into Star Citizen's message loop, not only a read.
    /// </summary>
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow (Remarks)
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    /// <summary>Waits until DWM has presented this process's queued changes (variant V2: our window hidden on screen).</summary>
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush
    [LibraryImport("dwmapi.dll")]
    public static partial int DwmFlush();

    /// <summary>
    /// A 32-bit top-down DIB section of <paramref name="width"/> × <paramref name="height"/>: BGRA bytes, rows from the top,
    /// so SkiaSharp's Bgra8888 reads it as it is. The system allocates the bits; DeleteObject frees them.
    /// </summary>
    public static nint CreateTopDownDib(int width, int height, out nint bits)
    {
        BITMAPINFOHEADER header = new()
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // negative = top-down
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BI_RGB,
        };
        return CreateDIBSection(0, ref header, DIB_RGB_COLORS, out bits, 0, 0);
    }

    // BITMAPINFO with no colour table (32 bpp, BI_RGB) is the header alone.
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection
    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial nint CreateDIBSection(nint hdc, ref BITMAPINFOHEADER pbmi, uint usage, out nint ppvBits, nint hSection, uint offset);

    // Blittable layouts, field order as in windef.h / wingdi.h.
    // Sources: https://learn.microsoft.com/en-us/windows/win32/api/windef/ns-windef-rect
    //          https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
}
