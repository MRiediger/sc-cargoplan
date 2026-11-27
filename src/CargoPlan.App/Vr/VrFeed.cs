using System.Collections.Concurrent;
using System.Diagnostics;
using CargoPlan.App.Diagnostics;
using CargoPlan.App.Win32;
using static System.FormattableString;

namespace CargoPlan.App.Vr;

/// <summary>A picture for the headset in RGBA (SetOverlayRaw's order); the feed lends it out and takes it back.</summary>
internal sealed class VrFrame(int width, int height)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public byte[] Rgba { get; } = new byte[width * height * 4];
}

/// <summary>Where the cursor stands in the window (physical pixels from its top left) and its shape; null when outside or hidden.</summary>
internal readonly record struct CursorSpot(int X, int Y, nint Shape);

internal enum VrShotKind
{
    /// <summary>A new picture, handed over in <see cref="VrShot.Frame"/>.</summary>
    Captured,

    /// <summary>The same picture as last time: nothing to upload.</summary>
    Unchanged,

    /// <summary>The window is cloaked (the scan, D100): taken again on the next tick.</summary>
    Cloaked,

    /// <summary>An edge above 4096 px, which SetOverlayRaw refuses (S14.0, K4).</summary>
    TooLarge,

    /// <summary>The window is gone or PrintWindow failed.</summary>
    Failed,
}

/// <param name="Rent">A frame of the given size from the feed's buffers, to write the picture into.</param>
internal sealed record VrCaptureRequest(VrFrameStyle Style, bool Force, Func<int, int, VrFrame> Rent);

/// <param name="PrintMilliseconds">How long PrintWindow waited (E5 S14.0, K5); 0 when it didn't run.</param>
/// <param name="PrepMilliseconds">Key, mask, cursor, comparison and the copy for SteamVR.</param>
internal sealed record VrShot(VrShotKind Kind, VrFrame? Frame = null, double PrintMilliseconds = 0, double PrepMilliseconds = 0);

/// <summary>
/// The capture thread of the VR feed (T14.2, D131). It runs while the session is connected, takes a picture only when the
/// UI thread has signalled a change (<see cref="MarkDirty"/>) or the cursor has moved over the window, at most 10 times a
/// second (G3 of T14.2: 15 Hz cost 3.2 pp late frames over the game), and once more <see cref="TrailingDelay"/> after the last signal, because WPF draws on its render thread after the
/// UI thread's render work (K2). It never calls OpenVR: the newest picture waits in one slot for the VR thread
/// (<see cref="Take"/>), and a newer one replaces it. PrintWindow waits for the UI thread (S14.0), so <see cref="Stop"/>
/// never waits for this thread; there is only ever one, so the capture never runs twice at once.
/// </summary>
/// <param name="cursor">Polled every <see cref="CursorPoll"/> on this thread.</param>
/// <param name="capture">One picture; runs on this thread only.</param>
/// <param name="release">Frees what the capture holds (GDI); on this thread, when it ends.</param>
/// <param name="log">One line per <paramref name="statsWindow"/> (10 s) and one per PrintWindow wait above <see cref="LongWait"/>.</param>
internal sealed class VrFeed(
    Func<CursorSpot?> cursor,
    Func<VrCaptureRequest, VrShot> capture,
    Action release,
    VrScanPause pause,
    Action<string> log,
    TimeSpan? statsWindow = null)
{
    public const string ThreadName = "CargoPlan VR capture";

    /// <summary>10 Hz: D131 allows up to 15, G3 of T14.2 over the game didn't (+3.2 pp late frames at 15 Hz).</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1.0 / 10);

    public static readonly TimeSpan TrailingDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>As often as the hotkeys poll (D97); GetCursorInfo costs 0.01 ms (S14.0).</summary>
    public static readonly TimeSpan CursorPoll = TimeSpan.FromMilliseconds(33);

    public static readonly TimeSpan StatsWindow = TimeSpan.FromSeconds(10);

    /// <summary>A PrintWindow wait longer than this gets a line of its own (S14.0: 511 and 1 555 ms at the start).</summary>
    public static readonly TimeSpan LongWait = TimeSpan.FromMilliseconds(100);

    // One being written, one waiting, one uploading.
    private const int MaxFree = 3;

    private readonly Lock _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly ConcurrentQueue<VrFrame> _free = new();
    private readonly TimeSpan _statsWindow = statsWindow ?? StatsWindow;

    // Under _gate.
    private Thread? _thread;
    private bool _wanted;
    private VrFrameStyle _style = VrFrameStyle.None;

    private int _dirty;
    private VrFrame? _slot;
    private volatile bool _forced;
    private volatile bool _tooLarge;

    public VrFeedStats Stats { get; } = new();

    /// <summary>Whether the capture thread exists: from <see cref="Start"/> until it has seen <see cref="Stop"/>.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _thread is not null;
            }
        }
    }

    /// <summary>The developer's forced mode (K7): a picture every tick, uploaded even when unchanged.</summary>
    public bool Forced
    {
        get => _forced;
        set
        {
            _forced = value;
            _wake.Set();
        }
    }

    /// <summary>Whether the last capture found an edge above 4096 px; the VR thread turns it into the notice.</summary>
    public bool TooLarge => _tooLarge;

    /// <summary>Called on the VR thread when the session has connected; again while a hung capture still ends, it keeps that thread.</summary>
    public void Start()
    {
        lock (_gate)
        {
            _wanted = true;
            Interlocked.Exchange(ref _dirty, 1);
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = ThreadName };
                _thread.Start();
            }
        }

        _wake.Set();
    }

    /// <summary>Returns at once; the thread ends after the capture it may be in (a hung UI thread holds PrintWindow).</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _wanted = false;
            _tooLarge = false;
        }

        _wake.Set();
    }

    /// <summary>The UI thread's signal (<see cref="RenderSignal"/>) with the window's look; costs a lock and a wake.</summary>
    public void MarkDirty(VrFrameStyle style)
    {
        lock (_gate)
        {
            _style = style;
        }

        Interlocked.Exchange(ref _dirty, 1);
        _wake.Set();
    }

    /// <summary>The newest picture, or null; VR thread. Give it back with <see cref="Return"/> once SteamVR has copied it.</summary>
    public VrFrame? Take() => Interlocked.Exchange(ref _slot, null);

    public void Return(VrFrame frame)
    {
        if (_free.Count < MaxFree)
        {
            _free.Enqueue(frame);
        }
    }

    private VrFrame Rent(int width, int height)
    {
        while (_free.TryDequeue(out var frame))
        {
            if (frame.Width == width && frame.Height == height)
            {
                return frame;
            }
        }

        return new VrFrame(width, height);
    }

    private void Run()
    {
        var clock = Stopwatch.StartNew();
        double Now() => clock.Elapsed.TotalMilliseconds;

        var (interval, trailing, poll, window) = (MinInterval.TotalMilliseconds, TrailingDelay.TotalMilliseconds, CursorPoll.TotalMilliseconds, _statsWindow.TotalMilliseconds);
        double? lastCapture = null;
        double? trailingDue = null;
        var dirty = false;
        var lastCursor = cursor();
        var nextCursor = Now() + poll;
        var nextStats = Now() + window;
        var failureLogged = false;

        while (true)
        {
            lock (_gate)
            {
                if (!_wanted)
                {
                    try
                    {
                        // Under the lock: a Start now waits, so no second thread captures while this one frees.
                        release();
                    }
                    finally
                    {
                        _thread = null;
                    }

                    return;
                }
            }

            var now = Now();
            if (Interlocked.Exchange(ref _dirty, 0) == 1)
            {
                (dirty, trailingDue) = (true, now + trailing);
            }

            if (now >= nextCursor)
            {
                nextCursor = now + poll;
                if (cursor() is var spot && spot != lastCursor)
                {
                    (lastCursor, dirty) = (spot, true);
                }
            }

            var forced = _forced;
            dirty |= forced;
            var due = dirty || now >= trailingDue;
            if (due && (lastCapture is not { } last || now - last >= interval))
            {
                if (now >= trailingDue)
                {
                    trailingDue = null;
                }

                (dirty, lastCapture) = (false, now);
                if (pause.Active)
                {
                    Stats.Count("cloak");
                    dirty = true;
                }
                else
                {
                    dirty = Shoot(forced, ref failureLogged);
                }
            }

            if (now >= nextStats)
            {
                nextStats = now + window;
                failureLogged = false;
                log(Stats.Drain());
            }

            var wait = Math.Min(nextCursor, nextStats) - now;
            if (dirty && lastCapture is { } previous)
            {
                wait = Math.Min(wait, previous + interval - now);
            }

            if (trailingDue is { } t)
            {
                wait = Math.Min(wait, t - now);
            }

            _wake.WaitOne(TimeSpan.FromMilliseconds(Math.Clamp(wait, 1, poll)));
        }
    }

    /// <summary>One capture; true when it has to be taken again (the window was cloaked).</summary>
    private bool Shoot(bool forced, ref bool failureLogged)
    {
        VrFrameStyle style;
        lock (_gate)
        {
            style = _style;
        }

        VrShot shot;
        try
        {
            shot = capture(new VrCaptureRequest(style, forced, Rent));
        }
        catch (Exception error)
        {
            // A boundary (Win32, A6): the thread lives on, the next change tries again. One line per window, not 15 a second.
            Stats.Count("failed");
            if (!failureLogged)
            {
                failureLogged = true;
                log($"capture failed: {error}");
            }

            return false;
        }

        Stats.Count("captures");
        if (shot.PrintMilliseconds > 0)
        {
            Stats.Add("printWindow", shot.PrintMilliseconds);
            Stats.Add("prep", shot.PrepMilliseconds);
            if (shot.PrintMilliseconds > LongWait.TotalMilliseconds)
            {
                log(Invariant($"PrintWindow waited {shot.PrintMilliseconds:F1} ms"));
            }
        }

        _tooLarge = shot.Kind is VrShotKind.TooLarge;
        switch (shot.Kind)
        {
            case VrShotKind.Captured when shot.Frame is { } frame:
                Stats.Size(frame.Width, frame.Height);
                if (Interlocked.Exchange(ref _slot, frame) is { } waiting)
                {
                    Return(waiting);
                    Stats.Count("replaced");
                }

                return false;
            case VrShotKind.Cloaked:
                Stats.Count("cloak");
                return true;
            case VrShotKind.Unchanged:
                Stats.Count("unchanged");
                return false;
            case VrShotKind.TooLarge:
                Stats.Count("tooLarge");
                return false;
            default:
                Stats.Count("failed");
                return false;
        }
    }
}

/// <summary>
/// Counts and times of the feed per window (K5): the capture thread and the VR thread both add, the capture thread drains one
/// line per window, also at rest, so the log shows the rest (captures=0) and the memory (working set, GDI) over time.
/// </summary>
internal sealed class VrFeedStats
{
    private static readonly string[] Counted = ["captures", "uploads", "flips", "unchanged", "cloak", "replaced", "tooLarge", "failed"];
    private static readonly string[] Timed = ["printWindow", "prep", "upload"];

    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _counts = [];
    private readonly Dictionary<string, List<double>> _times = [];
    private (int Width, int Height)? _size;

    public void Count(string name, int by = 1)
    {
        lock (_gate)
        {
            _counts[name] = _counts.GetValueOrDefault(name) + by;
        }
    }

    public void Add(string name, double milliseconds)
    {
        lock (_gate)
        {
            if (!_times.TryGetValue(name, out var list))
            {
                _times[name] = list = [];
            }

            list.Add(milliseconds);
        }
    }

    public void Size(int width, int height)
    {
        lock (_gate)
        {
            _size = (width, height);
        }
    }

    /// <summary>One line, then the window starts again.</summary>
    public string Drain()
    {
        string counts, times, size;
        lock (_gate)
        {
            counts = string.Join(" ", Counted.Select(c => Invariant($"{c}={_counts.GetValueOrDefault(c)}")));
            times = string.Join(" ", Timed.Select(t => $"{t}={Spread(_times.GetValueOrDefault(t))}"));
            size = _size is var (w, h) ? Invariant($"{w}×{h}") : "-";
            _counts.Clear();
            _times.Clear();
        }

        using var process = Process.GetCurrentProcess();
        return Invariant($"feed: {counts} {times} (median/p95/max ms) ws={process.WorkingSet64 / 1048576.0:F1} MB gdi={NativeMethods.GdiObjectCount()} size={size}");
    }

    private static string Spread(List<double>? values) => values is { Count: > 0 }
        ? Invariant($"{FrameStatistics.Percentile(values, 0.5):F1}/{FrameStatistics.Percentile(values, 0.95):F1}/{values.Max():F1}")
        : "-";
}
