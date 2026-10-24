using System.Windows;
using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The 3D camera behind the ship view (D63): an orthographic camera that sees the scene exactly as
/// <see cref="IsoProjection"/> and <see cref="ViewTransform"/> draw it, so hologram and racks stay one picture while
/// the player turns, zooms and pans. World units are cells, like the scene.
/// </summary>
/// <param name="Width">The viewing box's width in cells (world units), spanning the viewport's width.</param>
public sealed record HoloCamera(Point3D Position, Vector3D LookDirection, Vector3D UpDirection, double Width)
{
    /// <summary>How far behind the screen's centre the camera stands, in cells; the far plane is twice that.</summary>
    public const double Distance = 2000;

    /// <summary>
    /// Look along the view direction, up against the screen's down axis (<see cref="IsoProjection.Unproject"/>), so
    /// look × up is the screen's right axis as WPF builds it; the centre of the viewport where the transform puts it;
    /// one cell at right angles to the view is <see cref="IsoProjection.Stretch"/> × scale pixels.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.orthographiccamera.width
    /// </summary>
    public static HoloCamera For(IsoProjection camera, ViewTransform transform, Size viewport)
    {
        var (dx, dy, dz) = camera.ViewDirection;
        Vector3D look = new(dx, dy, dz);
        var (downX, downY, downZ) = camera.Unproject(new Point(0, 1));
        var (originX, originY, originZ) = camera.Unproject(new Point(0, 0));
        Vector3D down = new(downX - originX, downY - originY, downZ - originZ);
        down.Normalize();

        var (cx, cy, cz) = camera.Unproject(transform.Invert(new Point(viewport.Width / 2, viewport.Height / 2)));
        return new HoloCamera(
            new Point3D(cx, cy, cz) - Distance * look,
            look,
            -down,
            viewport.Width / (IsoProjection.Stretch * transform.Scale));
    }
}
