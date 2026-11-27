using CargoPlan.Ocr;

namespace CargoPlan.App.Vr;

/// <summary>
/// The scan's capture as a pause of the VR feed (D100 addendum T14.0, K10): from clearing the region until it is given
/// back, the feed skips its tick, so the headset never shows a cloaked frame and the scan never waits for the feed.
/// </summary>
internal sealed class VrScanPause
{
    private int _holders;

    /// <summary>Read by the capture thread before every capture.</summary>
    public bool Active => Volatile.Read(ref _holders) > 0;

    /// <summary>Wraps the scanner's <c>clear</c>: the pause starts before the windows are cloaked and ends after they are back.</summary>
    public Func<PixelRect, IDisposable?> Bracket(Func<PixelRect, IDisposable?> clear) => region =>
    {
        Interlocked.Increment(ref _holders);
        try
        {
            return new Hold(this, clear(region));
        }
        catch
        {
            // No hold to dispose: the pause ends here, the failure goes on to the scanner.
            Interlocked.Decrement(ref _holders);
            throw;
        }
    };

    private sealed class Hold(VrScanPause pause, IDisposable? inner) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 1)
            {
                return;
            }

            try
            {
                inner?.Dispose();
            }
            finally
            {
                Interlocked.Decrement(ref pause._holders);
            }
        }
    }
}
