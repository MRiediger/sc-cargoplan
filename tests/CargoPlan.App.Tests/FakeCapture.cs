using System.Collections.Concurrent;
using System.Diagnostics;
using CargoPlan.App.Vr;

namespace CargoPlan.App.Tests;

/// <summary>
/// The window capture without a window (T14.2): every capture is recorded with its time, thread and request; what it
/// returns is <see cref="Next"/>'s next kind, a new frame by default. <see cref="Block"/> holds a capture as a hung UI
/// thread holds PrintWindow (S14.0).
/// </summary>
internal sealed class FakeCapture
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _gate = new();
    private CursorSpot? _cursor;
    private int _inside;

    public ConcurrentQueue<VrShotKind> Next { get; } = new();

    public VrShotKind Otherwise { get; set; } = VrShotKind.Captured;

    public ConcurrentQueue<(double Milliseconds, int Thread, VrCaptureRequest Request)> Captures { get; } = new();

    public ConcurrentQueue<int> Releases { get; } = new();

    /// <summary>Every frame handed over, in order.</summary>
    public ConcurrentQueue<VrFrame> Frames { get; } = new();

    public ManualResetEventSlim? Block { get; set; }

    public double PrintMilliseconds { get; set; } = 5;

    /// <summary>The most captures that ran at the same time.</summary>
    public int MostAtOnce { get; private set; }

    public CursorSpot? Cursor
    {
        get
        {
            lock (_gate)
            {
                return _cursor;
            }
        }

        set
        {
            lock (_gate)
            {
                _cursor = value;
            }
        }
    }

    public double[] Times => [.. Captures.Select(c => c.Milliseconds)];

    public VrFeed Feed(VrScanPause? pause = null, Action<string>? log = null, TimeSpan? statsWindow = null) =>
        new(() => Cursor, Capture, () => Releases.Enqueue(Environment.CurrentManagedThreadId), pause ?? new VrScanPause(), log ?? (_ => { }), statsWindow);

    private VrShot Capture(VrCaptureRequest request)
    {
        var inside = Interlocked.Increment(ref _inside);
        lock (_gate)
        {
            MostAtOnce = Math.Max(MostAtOnce, inside);
        }

        try
        {
            Captures.Enqueue((_clock.Elapsed.TotalMilliseconds, Environment.CurrentManagedThreadId, request));
            Block?.Wait();
            var kind = Next.TryDequeue(out var next) ? next : Otherwise;
            if (kind is not VrShotKind.Captured)
            {
                return new VrShot(kind, null, PrintMilliseconds);
            }

            var frame = request.Rent(4, 2);
            Frames.Enqueue(frame);
            return new VrShot(kind, frame, PrintMilliseconds);
        }
        finally
        {
            Interlocked.Decrement(ref _inside);
        }
    }
}
