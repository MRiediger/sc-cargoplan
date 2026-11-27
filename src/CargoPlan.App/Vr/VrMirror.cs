using System.Windows;
using System.Windows.Interop;

namespace CargoPlan.App.Vr;

/// <summary>
/// One window mirrored into the headset (T14.2, D131): the session, the feed on that window and the dirty signal, made on
/// the first <see cref="On"/>, so VR off loads no OpenVR, starts no thread and hooks nothing (invariant 3). The overlay
/// with <c>--vr-dev</c> and the measuring window of <c>--measure-frames vr</c> (E4) use it alike. UI thread only.
/// </summary>
/// <param name="report">The session's reports; called on the VR thread, so it must queue (Dispatcher.InvokeAsync).</param>
internal sealed class VrMirror(Window window, Action<VrReport> report, VrScanPause pause) : IDisposable
{
    private OpenVrSession? _session;
    private RenderSignal? _signal;

    /// <summary>The feed once VR was on; for the developer's forced mode (K7).</summary>
    public VrFeed? Feed { get; private set; }

    /// <summary>After the window has its handle (the first frame).</summary>
    public void On()
    {
        if (_session is null)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var capture = new WindowCapture(hwnd);
            var feed = Feed = new VrFeed(
                () => WindowCapture.CursorOver(hwnd), capture.Capture, capture.Release, pause, line => AppLog.Write(OpenVrSession.LogContext, line));
            _session = new OpenVrSession(new OpenVrApi(), report, VrOffset.Default, feed);
            _signal = new RenderSignal(window.Dispatcher, () => feed.MarkDirty(VrFrameStyle.Of(window)));
        }

        Feed!.MarkDirty(VrFrameStyle.Of(window));
        _signal!.Attach();
        _session.Start();
    }

    public void Off()
    {
        _signal?.Detach();
        _session?.Stop();
    }

    /// <summary>App exit: turns VR off and waits up to 3 s for the VR thread (OpenVrSession.Dispose).</summary>
    public void Dispose()
    {
        _signal?.Detach();
        _session?.Dispose();
    }
}
