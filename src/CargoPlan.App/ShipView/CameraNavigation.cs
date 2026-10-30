using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.Settings;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// What a <see cref="CameraNavigation"/> is applied on (D83): the fit at the current orientation, and the distances the
/// limits come from, at the ship's default orientation, so neither the angle nor the cargo moves them (holo pump bugfix).
/// </summary>
/// <param name="ReferenceDistance">The fit of every rack and the marker at the default orientation (<see cref="CameraFit.ShipPoints"/>).</param>
/// <param name="WholeDistance">The whole scene at the default orientation (<see cref="CameraFit.WholePoints"/>, K5.2d).</param>
public sealed record CameraBasis(Point3D FitTarget, double FitDistance, double ReferenceDistance, double WholeDistance)
{
    /// <param name="top">DIPs kept free above the margin for the numbers above a long ship's racks (D61).</param>
    public static CameraBasis For(ShipScene scene, IsoProjection orientation, Size viewport, double margin, double top = 0)
    {
        Point3D[] fit = [.. CameraFit.FitPoints(scene)];
        Point3D[] ship = [.. CameraFit.ShipPoints(scene)];
        Point3D[] whole = [.. CameraFit.WholePoints(scene)];
        var reference = IsoProjection.DefaultFor(scene.Bounds);
        var target = CameraFit.Centre(fit);
        return new CameraBasis(
            target,
            CameraFit.Distance(orientation, target, fit, viewport, margin, top),
            CameraFit.Distance(reference, CameraFit.Centre(ship), ship, viewport, margin, top),
            CameraFit.Distance(reference, CameraFit.Centre(whole), whole, viewport, margin, top));
    }
}

/// <summary>
/// How far the player can zoom (D76 in perspective, D83): in to <paramref name="MaxPxPerCell"/> in the target's plane, or
/// the reference fit where that is nearer (a small ship in a large view); out to twice the reference fit, or the whole
/// scene where that is further. A fit is never clamped.
/// </summary>
/// <param name="MaxPxPerCell">The preference <c>shipView.maxZoomPxPerCell</c>: a cell's size in the target's plane.</param>
public sealed record DistanceLimits(double MaxPxPerCell)
{
    /// <summary>Half the fit's scale before D83: twice its distance.</summary>
    public const double FarShareOfFit = 2;

    public static DistanceLimits Default { get; } = new(AppearanceRules.DefaultMaxZoom);

    /// <summary>The distance at which a cell in the target's plane is <paramref name="pxPerCell"/> wide: <c>W / (2 · px · tan(FOV/2))</c>.</summary>
    public double DistanceFor(double pxPerCell, Size viewport) => viewport.Width / (2 * pxPerCell * Math.Tan(CameraMath.FieldOfView / 2 * Math.PI / 180));

    public double Near(CameraBasis basis, Size viewport) => Math.Min(DistanceFor(MaxPxPerCell, viewport), basis.ReferenceDistance);

    public double Far(CameraBasis basis) => Math.Max(FarShareOfFit * basis.ReferenceDistance, basis.WholeDistance);

    public double Clamp(double distance, CameraBasis basis, Size viewport) => Math.Clamp(distance, Near(basis, viewport), Far(basis));
}

/// <summary>Where the player put the camera: distance and target, both fixed to the ship (D83).</summary>
public sealed record CameraPlacement(double Distance, Point3D Target);

/// <summary>
/// The player's camera in the perspective view (D83): the orientation, and a placement once zoomed, panned or turned; no
/// placement is the fit, which follows window, cargo and hologram (D61, D76). Saved per ship (K1.2).
/// </summary>
public sealed record CameraNavigation(IsoProjection Orientation, CameraPlacement? Placement = null)
{
    public static CameraNavigation Default { get; } = new(IsoProjection.Default);

    /// <summary>The ship's own default orientation (<see cref="IsoProjection.DefaultFor"/>), fitted.</summary>
    public static CameraNavigation DefaultFor(ShipBox bounds) => new(IsoProjection.DefaultFor(bounds));

    /// <summary>The saved view (K1.2); a placement only with both its values, otherwise the fit.</summary>
    public static CameraNavigation From(ShipCamera camera) => new(
        new IsoProjection(camera.Yaw, camera.Pitch),
        camera is { Distance: { } distance, Target: { } target } ? new CameraPlacement(distance, new Point3D(target.X, target.Y, target.Z)) : null);

    /// <summary>What settings.json keeps of this view (K1.2).</summary>
    public ShipCamera ToCamera() => new(
        Orientation.Yaw,
        Orientation.Pitch,
        Distance: Placement?.Distance,
        Target: Placement is { Target: var t } ? new CameraTarget(t.X, t.Y, t.Z) : null);

    public CameraMath On(CameraBasis basis, DistanceLimits limits, Size viewport) => Placement is { } placement
        ? new CameraMath(Orientation, limits.Clamp(placement.Distance, basis, viewport), placement.Target, viewport)
        : new CameraMath(Orientation, basis.FitDistance, basis.FitTarget, viewport);

    /// <summary>The zoom on top of the fit (D63, the hologram's edge levels): 1 at the fit.</summary>
    public double ZoomOn(CameraBasis basis, DistanceLimits limits, Size viewport) => basis.FitDistance / On(basis, limits, viewport).Distance;

    /// <summary>
    /// Zooms by <paramref name="factor"/>, clamped, so the point of the target's plane under <paramref name="cursor"/>
    /// stays put: the target moves towards it by the share the distance shrinks, within that plane.
    /// </summary>
    public CameraNavigation ZoomAt(Point cursor, double factor, CameraBasis basis, DistanceLimits limits, Size viewport)
    {
        var current = On(basis, limits, viewport);
        var distance = limits.Clamp(current.Distance / factor, basis, viewport);
        if (distance == current.Distance)
        {
            return this;
        }

        var ray = current.Unproject(cursor);
        var look = current.LookDirection;
        var point = ray.Origin + Vector3D.DotProduct(current.Target - ray.Origin, look) / Vector3D.DotProduct(ray.Direction, look) * ray.Direction;
        return this with { Placement = new CameraPlacement(distance, point - (point - current.Target) * (distance / current.Distance)) };
    }

    /// <summary>Moves the picture by <paramref name="delta"/> DIPs: the target's plane follows the mouse.</summary>
    public CameraNavigation PanBy(Vector delta, CameraBasis basis, DistanceLimits limits, Size viewport)
    {
        var current = On(basis, limits, viewport);
        var cells = 1 / current.PxPerCellAtTarget;
        var target = current.Target - delta.X * cells * current.RightDirection + delta.Y * cells * current.UpDirection;
        return this with { Placement = new CameraPlacement(current.Distance, target) };
    }

    /// <summary>The placement within <paramref name="limits"/>: a lower preference applies at once.</summary>
    public CameraNavigation Clamped(CameraBasis basis, DistanceLimits limits, Size viewport) =>
        Placement is { } placement && limits.Clamp(placement.Distance, basis, viewport) is var distance && distance != placement.Distance
            ? this with { Placement = placement with { Distance = distance } }
            : this;

    /// <summary>
    /// Turns the camera by <paramref name="yaw"/> and tilts it by <paramref name="pitch"/>, in degrees. From the fit, the
    /// fit on <paramref name="basis"/> becomes the placement, as panning makes it: refitting at every angle made the
    /// picture pump while turning (K5.5). Without a basis the fit stays a fit.
    /// </summary>
    public CameraNavigation OrbitBy(double yaw, double pitch, CameraBasis? basis = null) => this with
    {
        Orientation = Orientation.OrbitBy(yaw, pitch),
        Placement = Placement ?? (basis is null ? null : new CameraPlacement(basis.FitDistance, basis.FitTarget)),
    };
}
