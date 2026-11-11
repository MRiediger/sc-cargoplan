using System.Runtime.InteropServices;
using CargoPlan.Ocr;
using SkiaSharp;

namespace CargoPlan.App.Win32;

/// <summary>
/// Copies a rectangle of the screen with GDI BitBlt (S10.0: 33–41 ms for the whole client area, the fastest of four ways,
/// D99). Only the scan region, never the whole screen; one copy, screen → DIB section, which SkiaSharp then reads in place.
/// No interface: one implementation (A4).
/// </summary>
internal static class ScreenCapture
{
    private const int BytesPerPixel = 4;

    // Source: https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-
    private const int ErrorInvalidHandle = 6;

    /// <summary>
    /// The pixels of <paramref name="screen"/> (physical screen pixels), or null with the Win32 error when GDI refuses, e.g.
    /// without a desktop (locked session, CI): a result, not an exception (H3). Every handle but the DIB section is freed
    /// before this returns; the section goes with the result's Dispose.
    /// </summary>
    /// <param name="rasterOperation">SRCCOPY; CAPTUREBLT only for the overlay measurement (T10.1b, D100).</param>
    public static CapturedRegion? Region(PixelRect screen, out int error, uint rasterOperation = NativeMethods.SRCCOPY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(screen.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(screen.Height);

        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        var dib = NativeMethods.CreateTopDownDib(screen.Width, screen.Height, out var bits);
        if (dib == 0)
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }

        if (!CopyScreen(dib, screen, rasterOperation, out error))
        {
            NativeMethods.DeleteObject(dib);
            return null;
        }

        // GDI may still be drawing into the section; the bits are ours only after GdiFlush.
        // Source: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection (Remarks)
        NativeMethods.GdiFlush();

        // The bitmap reads the section's memory in place; Opaque, because BitBlt leaves the alpha byte undefined.
        // Source: https://learn.microsoft.com/dotnet/api/skiasharp.skbitmap.installpixels
        var bitmap = new SKBitmap();
        var info = new SKImageInfo(screen.Width, screen.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        if (!bitmap.InstallPixels(info, bits, screen.Width * BytesPerPixel))
        {
            bitmap.Dispose();
            NativeMethods.DeleteObject(dib);
            throw new InvalidOperationException($"SkiaSharp refused a {screen.Width}×{screen.Height} Bgra8888 DIB section.");
        }

        return new CapturedRegion(bitmap, dib);
    }

    // The screen DC, a memory DC with the section selected, the copy, then everything back and freed in reverse order.
    // Source: https://learn.microsoft.com/en-us/windows/win32/gdi/capturing-an-image
    private static bool CopyScreen(nint dib, PixelRect screen, uint rasterOperation, out int error)
    {
        nint screenDc = 0, memoryDc = 0, previous = 0;
        try
        {
            screenDc = NativeMethods.GetDC(0);
            memoryDc = screenDc == 0 ? 0 : NativeMethods.CreateCompatibleDC(screenDc);
            previous = memoryDc == 0 ? 0 : NativeMethods.SelectObject(memoryDc, dib);
            if (previous == 0)
            {
                // GetDC, CreateCompatibleDC and SelectObject set no last error; ERROR_INVALID_HANDLE stands for all three.
                error = ErrorInvalidHandle;
                return false;
            }

            if (NativeMethods.BitBlt(memoryDc, 0, 0, screen.Width, screen.Height, screenDc, screen.X, screen.Y, rasterOperation))
            {
                error = 0;
                return true;
            }

            error = Marshal.GetLastPInvokeError();
            return false;
        }
        finally
        {
            if (previous != 0)
            {
                NativeMethods.SelectObject(memoryDc, previous);
            }

            if (memoryDc != 0)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            if (screenDc != 0)
            {
                NativeMethods.ReleaseDC(0, screenDc);
            }
        }
    }
}

/// <summary>A captured region: a Bgra8888 bitmap over a GDI DIB section, which it owns. Dispose after reading.</summary>
internal sealed class CapturedRegion(SKBitmap bitmap, nint dib) : IDisposable
{
    private nint _dib = dib;

    public SKBitmap Bitmap { get; } = bitmap;

    public void Dispose()
    {
        // The bitmap first: it points into the section's memory.
        Bitmap.Dispose();
        if (_dib != 0)
        {
            NativeMethods.DeleteObject(_dib);
            _dib = 0;
        }
    }
}
