using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;

namespace CargoPlan.App.Scanning;

/// <summary>
/// Our windows out of the picture for one capture (D100): a BitBlt of the screen takes the overlay along when it lies
/// over the scan region, with or without CAPTUREBLT. Only the windows whose rectangle cuts the region go to Opacity 0, then
/// DwmFlush lets DWM present that; dispose brings each one's own opacity back, also when the capture throws. Opacity
/// instead of Hide: hiding fires IsVisibleChanged, and the settings window closes its colour picker and ends a hotkey
/// recording on it. Measured over the game: clean in 20 of 20, the windows away 24 ms (p95 31 ms).
/// </summary>
/// <param name="dispatcher">The UI thread's: windows are changed there, the caller is a pool thread.</param>
/// <param name="windows">Our windows; <c>Application.Current.Windows</c> in the app.</param>
internal sealed class RegionClearance(Dispatcher dispatcher, Func<IEnumerable<Window>> windows)
{
    /// <summary>
    /// Makes the windows over <paramref name="region"/> (physical screen pixels) invisible until the result is disposed;
    /// null when none lies over it, the normal case, which then costs no flush.
    /// </summary>
    public IDisposable? Clear(PixelRect region)
    {
        var visible = dispatcher.Invoke(() => windows()
            .Where(w => w.IsVisible && w.Opacity > 0)
            .Select(w => (Window: w, Handle: new WindowInteropHelper(w).Handle))
            .ToList());

        // Rectangles from GetWindowRect in physical pixels (H1): WPF's Left/Top are DIPs and miss at 125 %.
        List<Window> over;
        using (NativeMethods.PhysicalPixelsScope.Enter())
        {
            over = [.. visible.Where(v => NativeMethods.GetWindowBounds(v.Handle) is { } b && Overlaps(region, b)).Select(v => v.Window)];
        }

        if (over.Count == 0)
        {
            return null;
        }

        var previous = dispatcher.Invoke(() => over.Select(w =>
        {
            var opacity = w.Opacity;
            w.Opacity = 0;
            return (Window: w, Opacity: opacity);
        }).ToList());

        // One flush was clean in 20 of 20 over the game, none leaked in 10 of 20 (D100). DwmFlush returns an HRESULT and
        // doesn't throw; a failed one only risks our window in the picture, so it isn't checked.
        NativeMethods.DwmFlush();
        return new Restore(dispatcher, previous);
    }

    private static bool Overlaps(PixelRect region, (int X, int Y, int Width, int Height) b) =>
        b.X < region.X + region.Width && region.X < b.X + b.Width && b.Y < region.Y + region.Height && region.Y < b.Y + b.Height;

    private sealed class Restore(Dispatcher dispatcher, List<(Window Window, double Opacity)> previous) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            dispatcher.Invoke(() =>
            {
                foreach (var (window, opacity) in previous)
                {
                    window.Opacity = opacity;
                }
            });
        }
    }
}
