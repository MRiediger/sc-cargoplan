using Valve.VR;

namespace CargoPlan.App.Vr;

/// <summary>
/// The two overlays of the double buffer (D131, T14.2): width, opacity and transform on both, so either can show. A frame
/// goes into the hidden one; only after its VREvent_ImageLoaded does it show and the other hide, because SetOverlayRaw is
/// asynchronous and one overlay alone flickers on every texture change (S14.0). While a load is open the host isn't
/// <see cref="Ready"/>, and the feed's newest frame waits (a newer one replaces it). VR thread only.
/// </summary>
internal sealed class VrOverlayHost(IOpenVr vr)
{
    public const string KeyA = "cargoplan.vr.a";
    public const string KeyB = "cargoplan.vr.b";
    public const string Name = "SC-CargoPlan";

    // The hidden one takes the next frame; A first, so the first frame shows in A.
    private ulong _back;
    private ulong _front;
    private bool _frontShown;
    private bool _loading;

    /// <summary>No load open: the next frame may go up.</summary>
    public bool Ready => !_loading;

    /// <summary>Creates both overlays; on a refusal destroys what it made and returns the refusal (KeyInUse: a second instance).</summary>
    public EVROverlayError Create()
    {
        var error = vr.CreateOverlay(KeyA, Name, out _back);
        if (error != EVROverlayError.None)
        {
            _back = 0;
            return error;
        }

        error = vr.CreateOverlay(KeyB, Name, out _front);
        if (error != EVROverlayError.None)
        {
            _front = 0;
            Destroy();
        }

        return error;
    }

    public void Apply(VrOffset offset, HmdMatrix34_t transform)
    {
        foreach (var handle in (ulong[])[_back, _front])
        {
            Check("SetOverlayWidthInMeters", vr.SetWidth(handle, (float)offset.WidthMeters));
            Check("SetOverlayAlpha", vr.SetAlpha(handle, (float)offset.Opacity));
            Check("SetOverlayTransformAbsolute", vr.SetTransform(handle, transform));
        }
    }

    /// <summary>Hands the frame to the hidden overlay; SteamVR copies it within the call, so the caller may reuse it.</summary>
    public EVROverlayError Upload(VrFrame frame)
    {
        var error = vr.SetRaw(_back, frame.Rgba, frame.Width, frame.Height);
        Check("SetOverlayRaw", error);
        _loading = error == EVROverlayError.None;
        return error;
    }

    /// <summary>Drains both overlays' queues; returns how many flips it made (0 or 1).</summary>
    public int Poll()
    {
        var flips = 0;
        while (vr.PollOverlay(_back) is { } type)
        {
            if (type is EVREventType.VREvent_ImageLoaded && _loading)
            {
                Flip();
                flips++;

                // The roles have changed: what is left on this queue belongs to the overlay now in front.
                break;
            }

            if (type is EVREventType.VREvent_ImageFailed && _loading)
            {
                _loading = false;
                AppLog.Write(OpenVrSession.LogContext, "SteamVR couldn't load a picture (ImageFailed)");
            }
        }

        // The overlay in front has nothing to wait for; its queue is drained so nothing piles up.
        while (vr.PollOverlay(_front) is not null)
        {
        }

        return flips;
    }

    public void Destroy()
    {
        foreach (var handle in (ulong[])[_back, _front])
        {
            if (handle != 0)
            {
                Check("DestroyOverlay", vr.DestroyOverlay(handle));
            }
        }

        (_back, _front, _frontShown, _loading) = (0, 0, false, false);
    }

    // The new one first, then the old one away: the headset is never without a picture.
    private void Flip()
    {
        Check("ShowOverlay", vr.Show(_back));
        if (_frontShown)
        {
            Check("HideOverlay", vr.Hide(_front));
        }

        (_back, _front, _frontShown, _loading) = (_front, _back, true, false);
    }

    private static void Check(string call, EVROverlayError error)
    {
        if (error != EVROverlayError.None)
        {
            AppLog.Write(OpenVrSession.LogContext, $"{call}: {error}");
        }
    }
}
