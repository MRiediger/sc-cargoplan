using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>From projected ship units to screen pixels: scale, then offset.</summary>
public sealed record ViewTransform(double Scale, Vector Offset)
{
    // Keeps Invert defined for a viewport that has no size yet.
    private const double MinScale = 1e-6;

    public Point Apply(Point point) => new(point.X * Scale + Offset.X, point.Y * Scale + Offset.Y);

    public Point Invert(Point screen) => new((screen.X - Offset.X) / Scale, (screen.Y - Offset.Y) / Scale);

    /// <summary>
    /// The largest scale that shows all of <paramref name="extent"/> with <paramref name="margin"/> on every side,
    /// centred. A viewport smaller than four margins shrinks the margin instead of losing the picture.
    /// </summary>
    /// <param name="top">Pixels kept free above the margin: room for the rack numbers above a long ship's racks (D61).</param>
    public static ViewTransform Fit(Rect extent, Size viewport, double margin, double top = 0)
    {
        var height = Math.Max(viewport.Height - top, 0);
        var edge = Math.Min(margin, Math.Min(viewport.Width, height) / 4);
        var scale = Math.Max(
            Math.Min((viewport.Width - 2 * edge) / extent.Width, (height - 2 * edge) / extent.Height),
            MinScale);

        Vector centre = new(viewport.Width / 2, height / 2 + top);
        Vector extentCentre = new(extent.X + extent.Width / 2, extent.Y + extent.Height / 2);
        return new ViewTransform(scale, centre - extentCentre * scale);
    }
}

/// <summary>
/// The player's camera, and zoom and pan on top of the fit. Kept apart from the fit so resizing the window or turning
/// the camera refits, and the default (default camera, zoom 1, no pan) always shows the whole ship. Not persisted (A4).
/// </summary>
public sealed record ViewNavigation(double Zoom, Vector Pan, IsoProjection Camera)
{
    public const double MinZoom = 0.5;
    public const double MaxZoom = 4;

    public static ViewNavigation Default { get; } = new(1, new Vector(0, 0), IsoProjection.Default);

    /// <summary>Zoom 1, no pan, the ship's own default camera (<see cref="IsoProjection.DefaultFor"/>).</summary>
    public static ViewNavigation DefaultFor(ShipBox bounds) => Default with { Camera = IsoProjection.DefaultFor(bounds) };

    /// <summary>Screen = fit, then zoom around the screen origin, then pan.</summary>
    public ViewTransform On(ViewTransform fit) => new(fit.Scale * Zoom, fit.Offset * Zoom + Pan);

    /// <summary>Zooms by <paramref name="factor"/>, clamped, so the point under <paramref name="cursor"/> stays put.</summary>
    public ViewNavigation ZoomAt(Point cursor, double factor)
    {
        var zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (zoom == Zoom)
        {
            return this;
        }

        var ratio = zoom / Zoom;
        Vector at = (Vector)cursor;
        return this with { Zoom = zoom, Pan = at - (at - Pan) * ratio };
    }

    public ViewNavigation PanBy(Vector delta) => this with { Pan = Pan + delta };

    /// <summary>Turns the camera by <paramref name="yaw"/> and tilts it by <paramref name="pitch"/>, in degrees.</summary>
    public ViewNavigation OrbitBy(double yaw, double pitch) => this with { Camera = Camera.OrbitBy(yaw, pitch) };
}
