using System.IO;
using System.Runtime.InteropServices;
using Valve.VR;

namespace CargoPlan.App.Vr;

/// <summary>
/// The OpenVR calls the VR mode makes (T14.1, D131), as one seam: <see cref="OpenVrApi"/> goes to SteamVR, the tests'
/// fake answers without it. Called from the VR thread only. Transforms and poses are in the seated universe.
/// </summary>
internal interface IOpenVr
{
    /// <summary>Whether openvr_api.dll next to the exe loads at all; before the first call that needs it.</summary>
    bool TryLoad();

    EVRInitError Init(EVRApplicationType type);

    void Shutdown();

    EVROverlayError CreateOverlay(string key, string name, out ulong handle);

    EVROverlayError DestroyOverlay(ulong handle);

    EVROverlayError SetWidth(ulong handle, float meters);

    EVROverlayError SetAlpha(ulong handle, float alpha);

    EVROverlayError SetTransform(ulong handle, HmdMatrix34_t transform);

    /// <summary>Hands a picture to SteamVR, 4 bytes per pixel in RGBA; it loads asynchronously (VREvent_ImageLoaded).</summary>
    EVROverlayError SetRaw(ulong handle, byte[] rgba, int width, int height);

    EVROverlayError Show(ulong handle);

    EVROverlayError Hide(ulong handle);

    /// <summary>The next event on the system's queue (Quit and the like), or null when it is empty.</summary>
    EVREventType? PollSystem();

    /// <summary>The next event on an overlay's own queue (ImageLoaded and the like, S14.0), or null when it is empty.</summary>
    EVREventType? PollOverlay(ulong handle);

    /// <summary>The headset's pose, or null while it isn't valid.</summary>
    HmdMatrix34_t? HeadPose();

    void AcknowledgeQuit();
}

/// <summary>
/// <see cref="IOpenVr"/> through Valve's binding v2.15.6 (<c>Vr/OpenVR/openvr_api.cs</c>, the source of every signature
/// here, A3). Nothing in it runs before the VR mode is turned on, so without it openvr_api.dll never loads (invariant 3).
/// </summary>
internal sealed class OpenVrApi : IOpenVr
{
    public const string LibraryFile = "openvr_api.dll";

    private static readonly uint EventSize = (uint)Marshal.SizeOf<VREvent_t>();

    // The probe loads the file the binding's [DllImport("openvr_api")] finds next to the exe, then lets go of it.
    // Source: https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.nativelibrary.tryload
    public bool TryLoad()
    {
        if (!NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, LibraryFile), out var handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }

    public EVRInitError Init(EVRApplicationType type)
    {
        var error = EVRInitError.None;
        OpenVR.Init(ref error, type);
        return error;
    }

    public void Shutdown() => OpenVR.Shutdown();

    public EVROverlayError CreateOverlay(string key, string name, out ulong handle)
    {
        ulong created = 0;
        var error = OpenVR.Overlay.CreateOverlay(key, name, ref created);
        handle = created;
        return error;
    }

    public EVROverlayError DestroyOverlay(ulong handle) => OpenVR.Overlay.DestroyOverlay(handle);

    public EVROverlayError SetWidth(ulong handle, float meters) => OpenVR.Overlay.SetOverlayWidthInMeters(handle, meters);

    public EVROverlayError SetAlpha(ulong handle, float alpha) => OpenVR.Overlay.SetOverlayAlpha(handle, alpha);

    public EVROverlayError SetTransform(ulong handle, HmdMatrix34_t transform) =>
        OpenVR.Overlay.SetOverlayTransformAbsolute(handle, ETrackingUniverseOrigin.TrackingUniverseSeated, ref transform);

    public EVROverlayError SetRaw(ulong handle, byte[] rgba, int width, int height)
    {
        // SteamVR copies the bytes within the call (S14.0: read back byte-identical); pinned only for its length.
        var pinned = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try
        {
            return OpenVR.Overlay.SetOverlayRaw(handle, pinned.AddrOfPinnedObject(), (uint)width, (uint)height, 4);
        }
        finally
        {
            pinned.Free();
        }
    }

    public EVROverlayError Show(ulong handle) => OpenVR.Overlay.ShowOverlay(handle);

    public EVROverlayError Hide(ulong handle) => OpenVR.Overlay.HideOverlay(handle);

    public EVREventType? PollSystem()
    {
        var e = default(VREvent_t);
        return OpenVR.System.PollNextEvent(ref e, EventSize) ? (EVREventType)e.eventType : null;
    }

    public EVREventType? PollOverlay(ulong handle)
    {
        var e = default(VREvent_t);
        return OpenVR.Overlay.PollNextOverlayEvent(handle, ref e, EventSize) ? (EVREventType)e.eventType : null;
    }

    public HmdMatrix34_t? HeadPose()
    {
        var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseSeated, 0, poses);
        var head = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];
        return head.bPoseIsValid ? head.mDeviceToAbsoluteTracking : null;
    }

    public void AcknowledgeQuit() => OpenVR.System.AcknowledgeQuit_Exiting();
}
