using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;

namespace CargoPlan.App.Scanning;

/// <summary>
/// Our windows out of the picture for one capture (D100, B12): a BitBlt of the screen takes them along when they lie over
/// the scan region. The windows whose rectangle cuts the region are cloaked (DWM leaves them out of the composition, title
/// bar included), then DwmFlush lets DWM present that; dispose uncloaks them, also when the capture throws. Cloak instead
/// of Opacity 0, which reached only our layered windows (B12), and instead of Hide, which fires IsVisibleChanged (D100).
/// Measured over the game: clean in 20 of 20 for both kinds of window, away 30 ms (p95 35 ms), focus kept.
/// </summary>
/// <param name="dispatcher">The UI thread's: windows are read and cloaked there, the caller is a pool thread.</param>
/// <param name="windows">Our windows; <c>Application.Current.Windows</c> in the app.</param>
/// <param name="cloak">Cloaks (true) or uncloaks a window by its handle; <c>NativeMethods.SetCloak</c> in the app.</param>
internal sealed class RegionClearance(Dispatcher dispatcher, Func<IEnumerable<Window>> windows, Action<nint, bool> cloak)
{
    // The handles we cloaked and haven't brought back yet; read and changed on the UI thread only.
    private readonly HashSet<nint> _cloaked = [];

    /// <summary>
    /// Cloaks the windows over <paramref name="region"/> (physical screen pixels) until the result is disposed; null when
    /// there is nothing to cloak, the normal case, which then costs no flush.
    /// </summary>
    public IDisposable? Clear(PixelRect region)
    {
        var visible = dispatcher.Invoke(() => windows()
            .Where(w => w.IsVisible)
            .Select(w => (Window: w, Handle: new WindowInteropHelper(w).Handle))
            .ToList());

        // Rectangles from GetWindowRect in physical pixels (H1): WPF's Left/Top are DIPs and miss at 125 %.
        List<(Window Window, nint Handle)> over;
        using (NativeMethods.PhysicalPixelsScope.Enter())
        {
            over = [.. visible.Where(v => NativeMethods.GetWindowBounds(v.Handle) is { } b && Overlaps(region, b))];
        }

        if (over.Count == 0)
        {
            return null;
        }

        var cloaked = dispatcher.Invoke(() =>
        {
            List<(Window Window, nint Handle)> mine = [];
            // Owners first, so an owned window finds its owner already in the set.
            foreach (var (window, handle) in over.OrderBy(o => Owners(o.Window).Count()))
            {
                // One cloak per owner chain: DWM hides an owned window with its cloaked owner, and uncloaking it while
                // the owner is still cloaked fails with E_INVALIDARG (B12, R5c). A hidden owner isn't cloaked, so its
                // owned window, still on screen, is cloaked itself.
                if (Owners(window).Any(o => _cloaked.Contains(new WindowInteropHelper(o).Handle)) || !_cloaked.Add(handle))
                {
                    continue;
                }

                cloak(handle, true);
                mine.Add((window, handle));
            }

            return mine;
        });

        if (cloaked.Count == 0)
        {
            return null;
        }

        // One flush was clean in 20 of 20 over the game for both ways (D100, B12). DwmFlush returns an HRESULT and
        // doesn't throw; a failed one only risks our window in the picture, so it isn't checked.
        NativeMethods.DwmFlush();
        return new Restore(() => Uncloak(cloaked));
    }

    private void Uncloak(List<(Window Window, nint Handle)> cloaked) => dispatcher.Invoke(() =>
    {
        foreach (var (window, handle) in cloaked)
        {
            _cloaked.Remove(handle);
            // Closed during the capture: the handle is gone (and could come back for another window), so not touched.
            if (PresentationSource.FromVisual(window) is not null)
            {
                cloak(handle, false);
            }
        }
    });

    private static IEnumerable<Window> Owners(Window window)
    {
        for (var owner = window.Owner; owner is not null; owner = owner.Owner)
        {
            yield return owner;
        }
    }

    private static bool Overlaps(PixelRect region, (int X, int Y, int Width, int Height) b) =>
        b.X < region.X + region.Width && region.X < b.X + b.Width && b.Y < region.Y + region.Height && region.Y < b.Y + b.Height;

    private sealed class Restore(Action uncloak) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            uncloak();
        }
    }
}
