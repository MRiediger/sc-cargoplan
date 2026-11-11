using System.Diagnostics;
using System.Globalization;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;
using SkiaSharp;

namespace CargoPlan.App.Scanning;

/// <summary>
/// Reads the contract on the screen (T10.1b): the one path for the probe now and the scan hotkey in T10.3. Holds the
/// process's one OCR engine, loaded and warmed on a background thread from <see cref="StartWarmup"/>. A scan finds the game
/// window, captures the scan region and reads it on a pool thread; one scan at a time, a second is ignored.
/// A scan before the warm-up ends waits for it, at most <see cref="WarmupLimit"/> (plan decisions 1, K2).
/// No interface: one implementation; the delegates are there for the tests (A4).
/// </summary>
/// <param name="load">Loads the engine; <see cref="OcrEngine.Load()"/> in the app.</param>
/// <param name="notice">The scan's notice line; null clears it. Called from any thread: the caller marshals.</param>
/// <param name="find">Finds the game window; <see cref="GameWindow.Find()"/> in the app.</param>
/// <param name="warmupLimit">How long a scan waits for the warm-up; <see cref="WarmupLimit"/> in the app.</param>
/// <param name="clear">Takes our windows off the region for the capture (D100); disposed even when the capture throws.</param>
/// <param name="capture">Copies the region; <see cref="ScreenCapture.Region"/> in the app.</param>
internal sealed class ContractScanner(
    Func<OcrEngine> load,
    Action<string?> notice,
    Func<GameWindow?>? find = null,
    TimeSpan? warmupLimit = null,
    Func<PixelRect, IDisposable?>? clear = null,
    CaptureRegion? capture = null)
{
    /// <summary>Ten times the expected 1–2 s (K2): a warm-up that takes longer hangs, a scan mustn't wait for ever.</summary>
    public static readonly TimeSpan WarmupLimit = TimeSpan.FromSeconds(15);

    public const string WarmingNotice = "Contract scan: OCR is still warming up …";

    private const long Megabyte = 1024 * 1024;

    private readonly Func<GameWindow?> _find = find ?? GameWindow.Find;
    private readonly TimeSpan _limit = warmupLimit ?? WarmupLimit;
    private readonly CaptureRegion _capture = capture ?? ((PixelRect region, out int error) => ScreenCapture.Region(region, out error));

    // Completed by the warm-up, which never throws: a scan before StartWarmup (a key before the first frame) waits on it
    // like one during the warm-up. Continuations off the completing thread, so a waiting scan never runs on the warm-up's.
    // Source: https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskcreationoptions#fields (RunContinuationsAsynchronously)
    private readonly TaskCompletionSource<ScanContractSource?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _unavailable;
    private int _started;
    private int _busy;

    /// <summary>Whether the engine is loaded and warm; false while warming and after a failed load.</summary>
    public bool IsReady => _ready.Task is { IsCompletedSuccessfully: true, Result: not null };

    /// <summary>Loads and warms the engine on a pool thread; call once, after the overlay's first frame (plan decision 2).</summary>
    public void StartWarmup()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            throw new InvalidOperationException("The OCR warm-up has already started.");
        }

        _ = Task.Run(() => _ready.SetResult(Warm()));
    }

    /// <summary>
    /// One scan, from now to the raw text. <paramref name="inspect"/> sees the captured region before it is read (the probe
    /// saves it as a PNG); it must not keep the bitmap.
    /// </summary>
    public async Task<ScanAttempt> ScanAsync(Action<SKBitmap>? inspect = null)
    {
        var warmup = _ready.Task;
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            AppLog.Write("Scan", "ignored: another scan is running");
            return new ScanAttempt(ScanAttemptOutcome.Busy, "", TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero);
        }

        try
        {
            var clock = Stopwatch.StartNew();
            if (!warmup.IsCompleted)
            {
                Notice(WarmingNotice);
            }

            var finished = await Task.WhenAny(warmup, Task.Delay(_limit)).ConfigureAwait(false);
            var wait = clock.Elapsed;
            if (finished != warmup)
            {
                Notice($"Contract scan unavailable: OCR did not start within {_limit.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s.");
                return new ScanAttempt(ScanAttemptOutcome.Unavailable, "", wait, TimeSpan.Zero, null, clock.Elapsed);
            }

            if (await warmup.ConfigureAwait(false) is not { } source)
            {
                // Again, in case the warming notice came after the warm-up's own.
                Notice(_unavailable);
                return new ScanAttempt(ScanAttemptOutcome.Unavailable, "", wait, TimeSpan.Zero, null, clock.Elapsed);
            }

            return await Task.Run(() => ScanNow(source, inspect, clock, wait)).ConfigureAwait(false);
        }
        finally
        {
            // Freed whatever happened, so a hung warm-up never leaves the hotkey dead until a restart (K2).
            Volatile.Write(ref _busy, 0);
        }
    }

    private ScanAttempt ScanNow(ScanContractSource source, Action<SKBitmap>? inspect, Stopwatch clock, TimeSpan wait)
    {
        var captureClock = Stopwatch.StartNew();
        if (_find() is not { } game)
        {
            AppLog.Write("Scan", "Star Citizen's window not found");
            return new ScanAttempt(ScanAttemptOutcome.GameNotFound, "", wait, captureClock.Elapsed, null, clock.Elapsed);
        }

        // Our windows over the region go away for the copy and come back in Dispose, also when the copy throws (D100).
        CapturedRegion? copied;
        int error;
        using (clear?.Invoke(game.ScanRegionOnScreen))
        {
            copied = _capture(game.ScanRegionOnScreen, out error);
        }

        using var captured = copied;
        var capture = captureClock.Elapsed;
        if (captured is null)
        {
            AppLog.Write("Scan", $"capturing {game.ScanRegionOnScreen} failed, Win32 error {error}");
            return new ScanAttempt(ScanAttemptOutcome.CaptureFailed, "", wait, capture, null, clock.Elapsed, game, error);
        }

        inspect?.Invoke(captured.Bitmap);
        var result = source.ReadRegion(captured.Bitmap);
        var outcome = result.Outcome == ScanOutcome.Read ? ScanAttemptOutcome.Read : ScanAttemptOutcome.NoContractInFrame;
        return new ScanAttempt(outcome, result.RawText, wait, capture, result.Timings, clock.Elapsed, game);
    }

    // Any failure, not only missing files: the native runtime loads here, and a failure in an unobserved task would vanish.
    // It is logged in full, the scan says so and stays off; the overlay runs on (plan decision 3, D82).
    private ScanContractSource? Warm()
    {
        using var process = Process.GetCurrentProcess();
        var sinceStart = DateTime.Now - process.StartTime;
        var (workingBefore, privateBefore) = (process.WorkingSet64, process.PrivateMemorySize64);
        var clock = Stopwatch.StartNew();
        try
        {
            var engine = load();
            var loaded = clock.Elapsed;
            engine.Warmup();
            var total = clock.Elapsed;
            process.Refresh();
            AppLog.Write("OCR ready", Inv(
                $"started {sinceStart.TotalMilliseconds:0} ms after the process, load {loaded.TotalMilliseconds:0} ms, warm-up read {(total - loaded).TotalMilliseconds:0} ms, total {total.TotalMilliseconds:0} ms; ")
                + Inv($"working set {workingBefore / Megabyte} → {process.WorkingSet64 / Megabyte} MB, private bytes {privateBefore / Megabyte} → {process.PrivateMemorySize64 / Megabyte} MB"));
            Notice(null);
            return new ScanContractSource(engine);
        }
        catch (Exception ex)
        {
            AppLog.Write("OCR unavailable", ex);
            _unavailable = $"Contract scan unavailable: {ex.Message}";
            Notice(_unavailable);
            return null;
        }
    }

    private void Notice(string? text)
    {
        if (text is not null)
        {
            AppLog.Write("Notice", text);
        }

        notice(text);
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
