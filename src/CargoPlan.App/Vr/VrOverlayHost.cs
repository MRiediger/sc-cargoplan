using Valve.VR;

namespace CargoPlan.App.Vr;

/// <summary>
/// The two overlays of the double buffer (D131): width, opacity and transform on both, so either can show. T14.1 shows a
/// test picture in A once it has loaded; B stays hidden until the feed flips between them (T14.2). VR thread only.
/// </summary>
internal sealed class VrOverlayHost(IOpenVr vr)
{
    public const string KeyA = "cargoplan.vr.a";
    public const string KeyB = "cargoplan.vr.b";
    public const string Name = "SC-CargoPlan";

    private const int TestWidth = 512;
    private const int TestHeight = 256;
    private const int TestCorner = 64;
    private const int TestBand = 64;
    private const byte HalfAlpha = 0x80;

    private ulong _a;
    private ulong _b;
    private bool _loading;

    /// <summary>Creates both overlays; on a refusal destroys what it made and returns the refusal (KeyInUse: a second instance).</summary>
    public EVROverlayError Create()
    {
        var error = vr.CreateOverlay(KeyA, Name, out _a);
        if (error != EVROverlayError.None)
        {
            _a = 0;
            return error;
        }

        error = vr.CreateOverlay(KeyB, Name, out _b);
        if (error != EVROverlayError.None)
        {
            _b = 0;
            Destroy();
        }

        return error;
    }

    public void Apply(VrOffset offset, HmdMatrix34_t transform)
    {
        foreach (var handle in (ulong[])[_a, _b])
        {
            Check("SetOverlayWidthInMeters", vr.SetWidth(handle, (float)offset.WidthMeters));
            Check("SetOverlayAlpha", vr.SetAlpha(handle, (float)offset.Opacity));
            Check("SetOverlayTransformAbsolute", vr.SetTransform(handle, transform));
        }
    }

    /// <summary>Hands the test picture to A; it shows when SteamVR has loaded it (<see cref="Poll"/>).</summary>
    public void ShowTestImage()
    {
        var (rgba, width, height) = TestImage();
        var error = vr.SetRaw(_a, rgba, width, height);
        Check("SetOverlayRaw", error);
        _loading = error == EVROverlayError.None;
    }

    /// <summary>Drains both overlays' queues: A shows after its ImageLoaded, never before (S14.0: SetOverlayRaw is asynchronous).</summary>
    public void Poll()
    {
        while (vr.PollOverlay(_a) is { } type)
        {
            if (type is EVREventType.VREvent_ImageLoaded && _loading)
            {
                _loading = false;
                Check("ShowOverlay", vr.Show(_a));
                AppLog.Write(OpenVrSession.LogContext, "test picture loaded and shown");
            }
            else if (type is EVREventType.VREvent_ImageFailed)
            {
                _loading = false;
                AppLog.Write(OpenVrSession.LogContext, "SteamVR couldn't load the test picture (ImageFailed)");
            }
        }

        // B gets pictures only with the feed (T14.2); its queue is drained so nothing piles up.
        while (vr.PollOverlay(_b) is not null)
        {
        }
    }

    public void Destroy()
    {
        foreach (var handle in (ulong[])[_a, _b])
        {
            if (handle != 0)
            {
                Check("DestroyOverlay", vr.DestroyOverlay(handle));
            }
        }

        (_a, _b, _loading) = (0, 0, false);
    }

    /// <summary>
    /// 512 × 256 in RGBA: a corner of its own colour each (red top left, green top right, blue bottom left, white bottom
    /// right), so a mirrored, flipped or channel-swapped picture shows (S14.0 G1); a half-transparent black band across the
    /// middle; clear elsewhere.
    /// </summary>
    internal static (byte[] Rgba, int Width, int Height) TestImage()
    {
        var rgba = new byte[TestWidth * TestHeight * 4];
        for (var y = 0; y < TestHeight; y++)
        {
            for (var x = 0; x < TestWidth; x++)
            {
                var (left, top) = (x < TestCorner, y < TestCorner);
                var (right, bottom) = (x >= TestWidth - TestCorner, y >= TestHeight - TestCorner);
                (byte R, byte G, byte B, byte A) pixel = (left, right, top, bottom) switch
                {
                    (true, _, true, _) => (255, 0, 0, 255),
                    (_, true, true, _) => (0, 255, 0, 255),
                    (true, _, _, true) => (0, 0, 255, 255),
                    (_, true, _, true) => (255, 255, 255, 255),
                    _ when Math.Abs(y - (TestHeight / 2)) < TestBand / 2 => (0, 0, 0, HalfAlpha),
                    _ => (0, 0, 0, 0),
                };
                var i = ((y * TestWidth) + x) * 4;
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = pixel;
            }
        }

        return (rgba, TestWidth, TestHeight);
    }

    private static void Check(string call, EVROverlayError error)
    {
        if (error != EVROverlayError.None)
        {
            AppLog.Write(OpenVrSession.LogContext, $"{call}: {error}");
        }
    }
}
