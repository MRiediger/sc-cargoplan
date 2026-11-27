using Valve.VR;

namespace CargoPlan.App.Vr;

/// <summary>
/// Where the overlay hangs relative to the head at the recenter (D131): the yaw is the user's, <b>positive = right of the
/// view</b>; distance, height and width in metres, opacity 0–1. T14.3a fills it from settings.json.
/// </summary>
internal sealed record VrOffset(double YawDegrees, double PitchDegrees, double DistanceMeters, double HeightMeters, double WidthMeters, double Opacity)
{
    /// <summary>The spike's values (S14.0, D131): 30° right, level, 1.2 m away, at eye height, 0.5 m wide, opaque.</summary>
    public static VrOffset Default { get; } = new(30, 0, 1.2, 0, 0.5, 1.0);
}

/// <summary>A placement and the two yaws it came from, for the log (OpenVR sense: positive = left).</summary>
internal readonly record struct VrPlace(double HeadYawDegrees, double OverlayYawDegrees, HmdMatrix34_t Transform);

/// <summary>Head pose → overlay transform (D131), pure maths: testable without SteamVR.</summary>
internal static class VrPlacement
{
    /// <summary>A head at the origin looking down −Z: the placement when the headset's pose isn't valid.</summary>
    public static HmdMatrix34_t Identity => new() { m0 = 1, m5 = 1, m10 = 1 };

    /// <summary>
    /// The OpenVR yaw of a pose in degrees: 0 = looking down −Z, positive = turned left about +Y. The forward axis is the
    /// pose's −Z column; for R = Ry(θ) its third column is (sin θ, 0, cos θ), so θ = atan2(m2, m10) (S14.0 G4).
    /// </summary>
    public static double HeadYawDegrees(HmdMatrix34_t pose) => Math.Atan2(pose.m2, pose.m10) * 180 / Math.PI;

    /// <summary>
    /// <c>overlayYaw = headYaw − userYaw</c> (the user's yaw turns right, OpenVR's left);
    /// <c>R = Ry(overlayYaw) · Rx(pitch)</c>, <c>t = Ry(overlayYaw) · (0, height, −distance) + head position</c>.
    /// </summary>
    public static VrPlace Place(HmdMatrix34_t head, VrOffset offset)
    {
        var headYaw = HeadYawDegrees(head);
        var overlayYaw = headYaw - offset.YawDegrees;
        var (sy, cy) = Math.SinCos(overlayYaw * Math.PI / 180);
        var (sp, cp) = Math.SinCos(offset.PitchDegrees * Math.PI / 180);

        // Ry = [[cy,0,sy],[0,1,0],[-sy,0,cy]], Rx = [[1,0,0],[0,cp,-sp],[0,sp,cp]].
        var transform = new HmdMatrix34_t
        {
            m0 = (float)cy,
            m1 = (float)(sy * sp),
            m2 = (float)(sy * cp),
            m3 = (float)((sy * -offset.DistanceMeters) + head.m3),
            m4 = 0,
            m5 = (float)cp,
            m6 = (float)-sp,
            m7 = (float)(offset.HeightMeters + head.m7),
            m8 = (float)-sy,
            m9 = (float)(cy * sp),
            m10 = (float)(cy * cp),
            m11 = (float)((cy * -offset.DistanceMeters) + head.m11),
        };
        return new VrPlace(headYaw, overlayYaw, transform);
    }
}
