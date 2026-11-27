using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Valve.VR;
using static System.FormattableString;

namespace CargoPlan.App.Vr;

/// <summary>What the session reports: connected or not, and the notice line for <c>NoticeSource.Vr</c> (null clears it).</summary>
internal sealed record VrReport(bool Connected, string? Notice);

/// <summary>
/// The connection to SteamVR (T14.1, D131). A VR thread of its own owns every OpenVR call: it exists from
/// <see cref="Start"/> until the connection ends, so VR off means no thread. It connects as
/// <c>VRApplication_Background</c>, which never starts SteamVR; without SteamVR it reports and ends, with no retry timer
/// (only the next <see cref="Start"/>). <c>VREvent_Quit</c> is acknowledged and shut down on that thread at once, and the
/// report only queued, so a hung UI thread can't hold the quit (S14.0 G6: SteamVR ends a process after ≈ 5 s).
/// </summary>
/// <param name="report">Called on the VR thread; must queue, never wait for the UI thread (Dispatcher.InvokeAsync).</param>
/// <param name="feed">
/// The pictures (T14.2): started when connected, stopped without waiting when the connection ends (its capture may hang
/// with the UI thread, D131). Its newest frame goes up whenever the double buffer is ready; null = nothing to show.
/// </param>
internal sealed class OpenVrSession(IOpenVr vr, Action<VrReport> report, VrOffset offset, VrFeed? feed = null) : IDisposable
{
    public const string LogContext = "VR";
    public const string ThreadName = "CargoPlan VR";

    public const string NotRunningText = "VR: SteamVR is not running. Start SteamVR, then turn VR on again.";
    public const string DllMissingText = "VR: openvr_api.dll is missing or can't be loaded. Reinstall SC-CargoPlan to use VR.";
    public const string SecondInstanceText = "VR: another SC-CargoPlan already shows its overlay in SteamVR. Close it, then turn VR on again.";
    public const string ClosedText = "VR: SteamVR has closed, so VR is off. Turn it on again once SteamVR runs.";
    public const string TooLargeText = "VR: the overlay is larger than 4096 px, which SteamVR can't show. Make it smaller.";

    /// <summary>How often the VR thread looks at the event queues while connected (the spike's 10 ms).</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(3);

    private readonly Lock _gate = new();
    private readonly ConcurrentQueue<bool> _wishes = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly VrOverlayHost _host = new(vr);

    // Under _gate.
    private Thread? _thread;
    private bool _disposed;

    // VR thread only.
    private bool _initialized;
    private bool _connected;
    private bool _toldTooLarge;

    /// <summary>Whether the VR thread exists: from <see cref="Start"/> until the connection has ended.</summary>
    internal bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _thread is not null;
            }
        }
    }

    public static string RefusedText(string error) => $"VR: SteamVR refused the connection ({error}).";

    public void Start() => Wish(true);

    public void Stop() => Wish(false);

    /// <summary>Turns VR off and waits up to 3 s for the VR thread to end (app exit).</summary>
    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            thread = _thread;
            if (thread is not null)
            {
                _wishes.Enqueue(false);
            }
        }

        _wake.Set();
        if (thread is not null && !thread.Join(JoinTimeout))
        {
            // A hung OpenVR call; the thread is a background thread and ends with the process.
            AppLog.Write(LogContext, Invariant($"the VR thread didn't end within {JoinTimeout.TotalSeconds:F0} s"));
            return;
        }

        _wake.Dispose();
    }

    private void Wish(bool on)
    {
        lock (_gate)
        {
            // Off with no thread is off already: no thread for nothing (invariant 3).
            if (_disposed || (!on && _thread is null))
            {
                return;
            }

            _wishes.Enqueue(on);
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = ThreadName };
                _thread.Start();
            }
        }

        _wake.Set();
    }

    private void Run()
    {
        while (true)
        {
            while (_wishes.TryDequeue(out var on))
            {
                if (on && !_connected)
                {
                    Guarded(Connect);
                }
                else if (!on && _connected)
                {
                    Guarded(() => Disconnect(quit: false));
                }
            }

            if (_connected)
            {
                Guarded(Poll);
            }

            lock (_gate)
            {
                if (!_connected && _wishes.IsEmpty)
                {
                    _thread = null;
                    return;
                }
            }

            _wake.WaitOne(PollInterval);
        }
    }

    private void Connect()
    {
        if (!vr.TryLoad())
        {
            AppLog.Write(LogContext, $"{OpenVrApi.LibraryFile} doesn't load");
            report(new VrReport(false, DllMissingText));
            return;
        }

        var watch = Stopwatch.StartNew();
        var error = vr.Init(EVRApplicationType.VRApplication_Background);
        AppLog.Write(LogContext, Invariant($"init as background app: {error} in {watch.Elapsed.TotalMilliseconds:F1} ms"));
        if (error != EVRInitError.None)
        {
            report(new VrReport(false, error is EVRInitError.Init_NoServerForBackgroundApp ? NotRunningText : RefusedText(error.ToString())));
            return;
        }

        _initialized = true;
        var created = _host.Create();
        if (created != EVROverlayError.None)
        {
            AppLog.Write(LogContext, $"CreateOverlay: {created}");
            ShutDown();
            report(new VrReport(false, created is EVROverlayError.KeyInUse ? SecondInstanceText : RefusedText(created.ToString())));
            return;
        }

        Place();
        (_connected, _toldTooLarge) = (true, false);
        report(new VrReport(true, null));
        feed?.Start();
    }

    private void Place()
    {
        var head = vr.HeadPose();
        var place = VrPlacement.Place(head ?? VrPlacement.Identity, offset);
        _host.Apply(offset, place.Transform);
        var t = place.Transform;
        AppLog.Write(LogContext, Invariant(
            $"placed: pose valid={head is not null} headYaw(openvr)={place.HeadYawDegrees:F1}° userYaw={offset.YawDegrees:F1}° → overlayYaw(openvr)={place.OverlayYawDegrees:F1}° at ({t.m3:F3}, {t.m7:F3}, {t.m11:F3})"));
    }

    private void Poll()
    {
        // The system's queue first: a quit wins over a picture that has just loaded.
        while (vr.PollSystem() is { } type)
        {
            if (type is EVREventType.VREvent_Quit)
            {
                Disconnect(quit: true);
                return;
            }
        }

        if (feed is null)
        {
            _host.Poll();
            return;
        }

        feed.Stats.Count("flips", _host.Poll());

        // The feed's state reaches the notice only from here, so nothing of it follows a quit's hint (A13).
        if (feed.TooLarge != _toldTooLarge)
        {
            _toldTooLarge = feed.TooLarge;
            report(new VrReport(true, _toldTooLarge ? TooLargeText : null));
        }

        if (_host.Ready && feed.Take() is { } frame)
        {
            var watch = Stopwatch.StartNew();
            var error = _host.Upload(frame);
            feed.Stats.Add("upload", watch.Elapsed.TotalMilliseconds);
            feed.Stats.Count(error == EVROverlayError.None ? "uploads" : "failed");
            feed.Return(frame);
        }
    }

    private void Disconnect(bool quit)
    {
        var watch = Stopwatch.StartNew();
        if (quit)
        {
            vr.AcknowledgeQuit();
        }

        ShutDown();
        AppLog.Write(LogContext, Invariant($"{(quit ? "VREvent_Quit acknowledged and shut down" : "turned off")} in {watch.Elapsed.TotalMilliseconds:F1} ms"));
        report(new VrReport(false, quit ? ClosedText : null));
    }

    private void ShutDown()
    {
        feed?.Stop();
        _host.Destroy();
        vr.Shutdown();
        (_initialized, _connected) = (false, false);
    }

    /// <summary>
    /// OpenVR is a boundary (A6): a DLL that loads but lacks or breaks an entry point fails on its first call. Our own
    /// English line, not the exception's text, which Windows localises (S14.0 G6).
    /// </summary>
    private void Guarded(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Fail(error, DllMissingText);
        }
        catch (SEHException error)
        {
            Fail(error, RefusedText(nameof(SEHException)));
        }
    }

    private void Fail(Exception error, string notice)
    {
        AppLog.Write(LogContext, error);
        if (_initialized)
        {
            try
            {
                ShutDown();
            }
            catch (Exception again) when (again is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or SEHException)
            {
                AppLog.Write(LogContext, again);
            }
        }

        (_initialized, _connected) = (false, false);
        feed?.Stop();
        report(new VrReport(false, notice));
    }
}
