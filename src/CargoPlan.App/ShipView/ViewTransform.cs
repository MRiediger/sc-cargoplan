using System.Windows;
using CargoPlan.App.Settings;
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
/// What a <see cref="ViewNavigation"/> is applied on (D76): the fit, the screen point in the middle of the fit's area
/// and the ship-fixed anchor, all for the current camera, size and scene.
/// </summary>
/// <param name="Centre">Where the fit puts the middle of its extent: the viewport's middle below the room on top.</param>
/// <param name="Anchor"><see cref="ProjectedScene.Anchor"/>: the projected centre of the ship, whatever is loaded.</param>
/// <param name="WholeScale">The scale at which the whole scene fits (<see cref="ProjectedScene.Extent"/>): a true-size
/// outline reaches far beyond the fit (K5.2c, K5.2d). In the ship view at the ship's default camera, like
/// <paramref name="ReferenceScale"/>.</param>
/// <param name="ReferenceScale">The fit the zoom limits are taken from: in the ship view the fit of the ship's racks and
/// marker at its default camera (<see cref="ProjectedScene.ShipExtent"/>), so neither the angle nor the cargo moves the
/// limits (holo pump bugfix, D76).</param>
public sealed record ViewBasis(ViewTransform Fit, Point Centre, Point Anchor, double WholeScale, double ReferenceScale)
{
    /// <summary>
    /// The basis of <see cref="ViewTransform.Fit"/> with these arguments; without <paramref name="whole"/> the fit is the
    /// whole. The limits are taken from this fit.
    /// </summary>
    public static ViewBasis Of(Rect extent, Point anchor, Size viewport, double margin, double top = 0, Rect? whole = null)
    {
        var fit = ViewTransform.Fit(extent, viewport, margin, top);
        var wholeScale = whole is { } scene ? ViewTransform.Fit(scene, viewport, margin, top).Scale : fit.Scale;
        return new ViewBasis(fit, fit.Apply(new Point(extent.X + extent.Width / 2, extent.Y + extent.Height / 2)), anchor, wholeScale, fit.Scale);
    }

    /// <summary>
    /// The ship view's basis for <paramref name="current"/>, the scene as its camera sees it: the fit of its fit extent and
    /// its anchor. The limits come from <paramref name="reference"/>, the same ship at its default camera: half the fit
    /// at the current angle let the scale pump while turning, where that fit was more than twice the framing (Hull A
    /// +24 % from the bow), at the zoom-out limit at every angle.
    /// </summary>
    /// <param name="top">Pixels kept free above the margin for the rack numbers of a long ship (D61).</param>
    public static ViewBasis For(ProjectedScene current, ProjectedScene reference, Size viewport, double margin, double top) =>
        Of(current.FitExtent, current.Anchor, viewport, margin, top) with
        {
            WholeScale = ViewTransform.Fit(reference.Extent, viewport, margin, top).Scale,
            ReferenceScale = ViewTransform.Fit(reference.ShipExtent, viewport, margin, top).Scale,
        };
}

/// <summary>
/// How far the player can zoom (D76), in pixels per cell (<see cref="ViewTransform.Scale"/>, DIPs): out to half the fit
/// as before, or further where the whole scene needs it (K5.2d), in to <paramref name="MaxPxPerCell"/> whatever the fit,
/// so a large ship zooms in as far as a small one. Both from <see cref="ViewBasis.ReferenceScale"/>, the fit at the ship's
/// default camera, so only the player changes the scale; that fit is always in range, a fit at another angle is never
/// clamped (<see cref="ViewNavigation.On"/>).
/// </summary>
/// <param name="MaxPxPerCell">The preference <c>shipView.maxZoomPxPerCell</c>.</param>
public sealed record ZoomLimits(double MaxPxPerCell)
{
    /// <summary>The lower limit's share of the fit, relative as before D76 (Martin, K1.1).</summary>
    public const double MinShareOfFit = 0.5;

    public static ZoomLimits Default { get; } = new(AppearanceRules.DefaultMaxZoom);

    /// <summary>Half the fit, or the whole scene where that is smaller: a true-size outline seen in full (Martin, K5.2d).</summary>
    public double Lower(ViewBasis basis) => Math.Min(basis.ReferenceScale * MinShareOfFit, basis.WholeScale);

    /// <summary>The preference, or the fit where that is larger: a small ship in a large overlay.</summary>
    public double Upper(ViewBasis basis) => Math.Max(MaxPxPerCell, basis.ReferenceScale);

    public double Clamp(double pxPerCell, ViewBasis basis) => Math.Clamp(pxPerCell, Lower(basis), Upper(basis));
}

/// <summary>
/// A view the player framed (D76): its size and where its middle lies, both fixed to the ship, so a sync that loads
/// another rack, a notice that shortens the view or another overlay size at the next start leave it where it was.
/// </summary>
/// <param name="PxPerCell">Pixels per cell (<see cref="ViewTransform.Scale"/>), clamped by <see cref="ZoomLimits"/> when drawn.</param>
/// <param name="Pan">The view's middle (<see cref="ViewBasis.Centre"/>) minus the anchor, in projected units, cells in the default view.</param>
public sealed record Framing(double PxPerCell, Vector Pan)
{
    /// <summary>The fit as a framing: its scale, and the pan that puts its picture where the fit does.</summary>
    public static Framing OfFit(ViewBasis basis) => new(basis.Fit.Scale, basis.Fit.Invert(basis.Centre) - basis.Anchor);

    public ViewTransform On(ViewBasis basis, double pxPerCell) => new(pxPerCell, (Vector)basis.Centre - ((Vector)basis.Anchor + Pan) * pxPerCell);
}

/// <summary>
/// The player's camera, and the framing on top of it; no framing is the fit, which follows window, camera and cargo
/// (D61), so the default (the ship's camera, the fit) always shows the ship. Saved per ship since K1.2 (D76).
/// </summary>
public sealed record ViewNavigation(IsoProjection Camera, Framing? Framing = null)
{
    public static ViewNavigation Default { get; } = new(IsoProjection.Default);

    /// <summary>The ship's own default camera (<see cref="IsoProjection.DefaultFor"/>), fitted.</summary>
    public static ViewNavigation DefaultFor(ShipBox bounds) => new(IsoProjection.DefaultFor(bounds));

    /// <summary>The saved view (K1.2); a framing only with all three of its values, otherwise the fit.</summary>
    public static ViewNavigation From(ShipCamera camera) => new(
        new IsoProjection(camera.Yaw, camera.Pitch),
        camera is { PxPerCell: { } pxPerCell, PanX: { } x, PanY: { } y } ? new Framing(pxPerCell, new Vector(x, y)) : null);

    /// <summary>What settings.json keeps of this view (K1.2).</summary>
    public ShipCamera ToCamera() => new(Camera.Yaw, Camera.Pitch, Framing?.PxPerCell, Framing?.Pan.X, Framing?.Pan.Y);

    public ViewTransform On(ViewBasis basis, ZoomLimits limits) =>
        Framing is { } framing ? framing.On(basis, limits.Clamp(framing.PxPerCell, basis)) : basis.Fit;

    /// <summary>The zoom on top of the fit (D63, the hologram's edge levels): 1 at the fit.</summary>
    public double ZoomOn(ViewBasis basis, ZoomLimits limits) => On(basis, limits).Scale / basis.Fit.Scale;

    /// <summary>Zooms by <paramref name="factor"/>, clamped, so the point under <paramref name="cursor"/> stays put.</summary>
    public ViewNavigation ZoomAt(Point cursor, double factor, ViewBasis basis, ZoomLimits limits)
    {
        var current = On(basis, limits);
        var scale = limits.Clamp(current.Scale * factor, basis);
        if (scale == current.Scale)
        {
            return this;
        }

        // The ship point under the cursor lands on the cursor again: centre − (anchor + pan − point) · scale = cursor.
        var point = current.Invert(cursor);
        return this with { Framing = new Framing(scale, (Vector)point - (Vector)basis.Anchor - (cursor - basis.Centre) / scale) };
    }

    /// <summary>Moves the picture by <paramref name="delta"/> pixels; from the fit, the framing starts at the fit.</summary>
    public ViewNavigation PanBy(Vector delta, ViewBasis basis, ZoomLimits limits)
    {
        var scale = On(basis, limits).Scale;
        var framing = Framing ?? Framing.OfFit(basis);
        return this with { Framing = new Framing(scale, framing.Pan - delta / scale) };
    }

    /// <summary>The framing within <paramref name="limits"/>, around the view's middle: a lower preference applies at once.</summary>
    public ViewNavigation Clamped(ViewBasis basis, ZoomLimits limits) =>
        Framing is { } framing && limits.Clamp(framing.PxPerCell, basis) is var scale && scale != framing.PxPerCell
            ? this with { Framing = framing with { PxPerCell = scale } }
            : this;

    /// <summary>
    /// Turns the camera by <paramref name="yaw"/> and tilts it by <paramref name="pitch"/>, in degrees. From the fit, the
    /// fit on <paramref name="basis"/> becomes the framing, as panning makes it: refitting at every angle made a
    /// hologram's size pump while turning (Martin, K5.5). Without a basis the fit stays a fit.
    /// </summary>
    public ViewNavigation OrbitBy(double yaw, double pitch, ViewBasis? basis = null) => this with
    {
        Camera = Camera.OrbitBy(yaw, pitch),
        Framing = Framing ?? (basis is null ? null : Framing.OfFit(basis)),
    };
}
