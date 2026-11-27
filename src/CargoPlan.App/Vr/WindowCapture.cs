using System.Diagnostics;
using CargoPlan.App.Win32;

namespace CargoPlan.App.Vr;

/// <summary>
/// One picture of our window for the headset (T14.2, D131, path b of S14.0): PrintWindow with PW_RENDERFULLCONTENT into a
/// DIB section of our own, then the key, the corner mask, the cursor, the comparison with the last picture and the copy in
/// RGBA. Only our own window, read in our own process; nothing touches the game (EAC). Capture thread only: PrintWindow
/// waits for the UI thread (WM_PRINT), so a hung UI thread holds this thread and never the VR thread.
/// </summary>
internal sealed class WindowCapture(nint hwnd)
{
    /// <summary>SetOverlayRaw refuses a longer edge (S14.0 open point 2: 4097 × 100 gives InvalidParameter).</summary>
    public const int MaxEdge = 4096;

    // The cursor's hot spots by shape: GetIconInfo makes two bitmaps a call, the shapes are few.
    private readonly Dictionary<nint, (int X, int Y)> _hotspots = [];

    private Dib? _dib;
    private byte[] _previous = [];
    private int[] _mask = [];
    private (int Width, int Height, double Radius) _maskFor;

    /// <summary>Where the cursor stands over the window, or null; any thread (two reads, no message to the window).</summary>
    public static CursorSpot? CursorOver(nint hwnd)
    {
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        if (NativeMethods.GetShownCursor() is not { } shown || NativeMethods.GetWindowBounds(hwnd) is not { } b)
        {
            return null;
        }

        var (shape, x, y) = shown;
        var (inX, inY) = (x - b.X, y - b.Y);
        return inX >= 0 && inY >= 0 && inX < b.Width && inY < b.Height ? new CursorSpot(inX, inY, shape) : null;
    }

    public VrShot Capture(VrCaptureRequest request)
    {
        // Physical pixels: WPF runs system-DPI-aware, the bounds must match the DIB PrintWindow fills (T10.1b).
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        if (NativeMethods.Cloaked(hwnd) is > 0)
        {
            return new VrShot(VrShotKind.Cloaked);
        }

        if (NativeMethods.GetWindowBounds(hwnd) is not { Width: > 0, Height: > 0 } bounds)
        {
            return new VrShot(VrShotKind.Failed);
        }

        if (bounds.Width > MaxEdge || bounds.Height > MaxEdge)
        {
            return new VrShot(VrShotKind.TooLarge);
        }

        if (_dib is null || _dib.Width != bounds.Width || _dib.Height != bounds.Height)
        {
            _dib?.Dispose();
            _dib = new Dib(bounds.Width, bounds.Height);
            _previous = new byte[bounds.Width * bounds.Height * 4];
        }

        var watch = Stopwatch.StartNew();
        var printed = NativeMethods.PrintWindow(hwnd, _dib.Dc, NativeMethods.PW_RENDERFULLCONTENT);
        NativeMethods.GdiFlush();
        var print = watch.Elapsed.TotalMilliseconds;
        if (!printed)
        {
            return new VrShot(VrShotKind.Failed, null, print);
        }

        watch.Restart();
        var span = _dib.Span;
        if (request.Style.Key is { } key)
        {
            FramePixels.ApplyKey(span, key);
        }

        FramePixels.ClearCorners(span, Mask(bounds.Width, bounds.Height, request.Style.CornerRadiusPixels));
        DrawCursor(_dib, bounds.X, bounds.Y);

        if (!request.Force && span.SequenceEqual(_previous))
        {
            return new VrShot(VrShotKind.Unchanged, null, print, watch.Elapsed.TotalMilliseconds);
        }

        span.CopyTo(_previous);
        var frame = request.Rent(bounds.Width, bounds.Height);
        FramePixels.CopyAsRgba(span, frame.Rgba);
        return new VrShot(VrShotKind.Captured, frame, print, watch.Elapsed.TotalMilliseconds);
    }

    /// <summary>Frees the DIB and its DC; the next capture makes them again. Capture thread, when it ends.</summary>
    public void Release()
    {
        _dib?.Dispose();
        (_dib, _previous) = (null, []);
    }

    private int[] Mask(int width, int height, double radius)
    {
        if (_maskFor != (width, height, radius))
        {
            (_mask, _maskFor) = (FramePixels.CornerMask(width, height, radius), (width, height, radius));
        }

        return _mask;
    }

    private void DrawCursor(Dib dib, int windowX, int windowY)
    {
        if (NativeMethods.GetShownCursor() is not { } shown)
        {
            return;
        }

        var (shape, x, y) = shown;

        if (!_hotspots.TryGetValue(shape, out var hot))
        {
            if (NativeMethods.GetCursorHotspot(shape) is not { } found)
            {
                return;
            }

            _hotspots[shape] = hot = found;
        }

        var (left, top) = (x - windowX - hot.X, y - windowY - hot.Y);
        if (x - windowX < 0 || y - windowY < 0 || x - windowX >= dib.Width || y - windowY >= dib.Height)
        {
            return;
        }

        // GDI leaves alpha 0 where it draws: every pixel it changed turns opaque, or the cursor vanishes in VR (S14.0).
        var (x0, y0) = (Math.Max(0, left), Math.Max(0, top));
        var (x1, y1) = (Math.Min(dib.Width, left + CursorBox), Math.Min(dib.Height, top + CursorBox));
        if (x1 <= x0 || y1 <= y0)
        {
            return;
        }

        var rowBytes = (x1 - x0) * 4;
        var before = new byte[rowBytes * (y1 - y0)];
        var span = dib.Span;
        for (var row = y0; row < y1; row++)
        {
            span.Slice(((row * dib.Width) + x0) * 4, rowBytes).CopyTo(before.AsSpan((row - y0) * rowBytes));
        }

        NativeMethods.DrawCursor(dib.Dc, left, top, shape);
        NativeMethods.GdiFlush();
        for (var row = y0; row < y1; row++)
        {
            var now = span.Slice(((row * dib.Width) + x0) * 4, rowBytes);
            var was = before.AsSpan((row - y0) * rowBytes, rowBytes);
            for (var i = 0; i < rowBytes; i += 4)
            {
                if (!now.Slice(i, 4).SequenceEqual(was.Slice(i, 4)))
                {
                    now[i + 3] = 0xFF;
                }
            }
        }
    }

    // Larger than any system cursor at 200 % (32 px at 100 %).
    private const int CursorBox = 96;

    /// <summary>A top-down 32-bit DIB section selected into a memory DC of its own (the spike's, S14.0).</summary>
    private sealed class Dib : IDisposable
    {
        private readonly nint _section;
        private readonly nint _previous;

        public Dib(int width, int height)
        {
            (Width, Height) = (width, height);
            _section = NativeMethods.CreateTopDownDib(width, height, out var bits);
            Bits = bits;
            var screen = NativeMethods.GetDC(0);
            Dc = NativeMethods.CreateCompatibleDC(screen);
            NativeMethods.ReleaseDC(0, screen);
            _previous = NativeMethods.SelectObject(Dc, _section);
        }

        public int Width { get; }

        public int Height { get; }

        public nint Dc { get; }

        public nint Bits { get; }

        public unsafe Span<byte> Span => new((void*)Bits, Width * Height * 4);

        public void Dispose()
        {
            NativeMethods.SelectObject(Dc, _previous);
            NativeMethods.DeleteDC(Dc);
            NativeMethods.DeleteObject(_section);
        }
    }
}
