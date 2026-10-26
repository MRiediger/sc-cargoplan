using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class ViewTransformTests
{
    private static readonly Rect Extent = new(-6, -5, 8, 9);
    private const double Margin = 8;

    private static void ShouldBeInside(Point p, Size viewport)
    {
        p.X.Should().BeInRange(-1e-9, viewport.Width + 1e-9);
        p.Y.Should().BeInRange(-1e-9, viewport.Height + 1e-9);
    }

    [Fact]
    public void Fit_centres_the_extent_in_the_viewport()
    {
        Size viewport = new(400, 300);

        var fit = ViewTransform.Fit(Extent, viewport, Margin);
        var centre = fit.Apply(new Point(Extent.X + Extent.Width / 2, Extent.Y + Extent.Height / 2));

        centre.X.Should().BeApproximately(200, 1e-9);
        centre.Y.Should().BeApproximately(150, 1e-9);
    }

    [Fact]
    public void Fit_uses_the_tighter_side_and_keeps_the_margin()
    {
        // Height decides: (300 - 2·8) / 9 is smaller than (400 - 2·8) / 8.
        var fit = ViewTransform.Fit(Extent, new Size(400, 300), Margin);

        fit.Scale.Should().BeApproximately((300 - 2 * Margin) / Extent.Height, 1e-9);
        fit.Apply(Extent.TopLeft).Y.Should().BeApproximately(Margin, 1e-9);
    }

    [Theory]
    [InlineData(400, 300)]
    [InlineData(220, 60)]
    [InlineData(30, 20)]
    public void Fit_keeps_every_corner_inside_the_viewport(double width, double height)
    {
        Size viewport = new(width, height);

        var fit = ViewTransform.Fit(Extent, viewport, Margin);

        fit.Scale.Should().BePositive();
        ShouldBeInside(fit.Apply(Extent.TopLeft), viewport);
        ShouldBeInside(fit.Apply(Extent.BottomRight), viewport);
    }

    [Fact]
    public void Fit_with_room_on_top_keeps_that_room_free_above_the_extent()
    {
        // Rack numbers standing above the racks of a long ship need it (D61).
        Size viewport = new(400, 300);

        var fit = ViewTransform.Fit(Extent, viewport, Margin, top: 30);

        fit.Apply(Extent.TopLeft).Y.Should().BeApproximately(Margin + 30, 1e-9);
        fit.Scale.Should().BeApproximately((300 - 30 - 2 * Margin) / Extent.Height, 1e-9);
        ShouldBeInside(fit.Apply(Extent.BottomRight), viewport);
    }

    [Fact]
    public void Fit_without_room_on_top_is_the_plain_fit()
    {
        ViewTransform.Fit(Extent, new Size(400, 300), Margin, top: 0).Should().Be(ViewTransform.Fit(Extent, new Size(400, 300), Margin));
    }

    [Fact]
    public void Inverting_undoes_applying()
    {
        var fit = ViewTransform.Fit(Extent, new Size(400, 300), Margin);
        Point p = new(1.5, -2.25);

        var back = fit.Invert(fit.Apply(p));

        back.X.Should().BeApproximately(p.X, 1e-9);
        back.Y.Should().BeApproximately(p.Y, 1e-9);
    }

    // The player's view (D76): pixels per cell and a pan in cells from a ship-fixed anchor, not from the fit.

    private static readonly Size Viewport = new(400, 300);

    // Not the extent's centre: the anchor is the ship's, the extent is the fit's (the loaded racks on a long ship).
    private static readonly Point Anchor = new(1, -2);

    private static readonly ZoomLimits Limits = new(160);

    private static ViewBasis Basis(Rect? extent = null, Size? viewport = null) =>
        ViewBasis.Of(extent ?? Extent, Anchor, viewport ?? Viewport, Margin);

    private static void ShouldBeClose(Point actual, Point expected)
    {
        actual.X.Should().BeApproximately(expected.X, 1e-9);
        actual.Y.Should().BeApproximately(expected.Y, 1e-9);
    }

    private static void ShouldBeTheSame(ViewTransform actual, ViewTransform expected)
    {
        actual.Scale.Should().BeApproximately(expected.Scale, 1e-9);
        actual.Offset.X.Should().BeApproximately(expected.Offset.X, 1e-9);
        actual.Offset.Y.Should().BeApproximately(expected.Offset.Y, 1e-9);
    }

    /// <summary>Zoomed in twice from the fit and panned: a view the player framed.</summary>
    private static ViewNavigation Framed(ViewBasis basis) =>
        ViewNavigation.Default.ZoomAt(new Point(120, 80), 2, basis, Limits).PanBy(new Vector(30, -10), basis, Limits);

    [Fact]
    public void The_basis_centre_is_where_the_fit_puts_the_middle_of_its_extent()
    {
        ShouldBeClose(Basis().Centre, new Point(200, 150));
        ShouldBeClose(ViewBasis.Of(Extent, Anchor, Viewport, Margin, top: 30).Centre, new Point(200, 165));
    }

    [Fact]
    public void Default_navigation_is_the_fit_itself()
    {
        var basis = Basis();

        ViewNavigation.Default.On(basis, Limits).Should().Be(basis.Fit);
    }

    [Fact]
    public void The_fit_as_a_framing_draws_the_fit_again()
    {
        // The fit is a framing with a computed pan, not pan 0: the anchor need not be the extent's centre.
        var basis = Basis();

        var framing = Framing.OfFit(basis);

        framing.PxPerCell.Should().BeApproximately(basis.Fit.Scale, 1e-9);
        framing.Pan.Should().NotBe(new Vector(0, 0));
        ShouldBeTheSame(new ViewNavigation(IsoProjection.Default, framing).On(basis, Limits), basis.Fit);
    }

    [Fact]
    public void Zooming_keeps_the_point_under_the_cursor_in_place()
    {
        var basis = Basis();
        var navigation = ViewNavigation.Default.PanBy(new Vector(30, -10), basis, Limits);
        Point cursor = new(120, 80);
        var shipPoint = navigation.On(basis, Limits).Invert(cursor);

        var zoomed = navigation.ZoomAt(cursor, 1.2, basis, Limits);

        ShouldBeClose(zoomed.On(basis, Limits).Apply(shipPoint), cursor);
        zoomed.Framing!.PxPerCell.Should().BeApproximately(basis.Fit.Scale * 1.2, 1e-9);
    }

    [Theory]
    [InlineData(400, 300)]
    [InlineData(1200, 900)]
    public void Zooming_in_stops_at_the_maximum_in_pixels_per_cell_whatever_the_fit(double width, double height)
    {
        // Absolute (D76): a large ship's small fit no longer caps how far the player gets in.
        var basis = Basis(viewport: new Size(width, height));

        ViewNavigation.Default.ZoomAt(new Point(50, 50), 1000, basis, Limits).Framing!.PxPerCell.Should().Be(160);
    }

    [Fact]
    public void Zooming_out_stops_at_half_the_fit()
    {
        // Relative as before (Martin, K1.1): the Hull A zooms out as far as it did.
        var basis = Basis();

        ViewNavigation.Default.ZoomAt(new Point(50, 50), 0.01, basis, Limits).Framing!.PxPerCell
            .Should().BeApproximately(basis.Fit.Scale * ZoomLimits.MinShareOfFit, 1e-9);
    }

    [Fact]
    public void Zooming_out_reaches_the_whole_scene_where_half_the_fit_would_not()
    {
        // A true-size outline around a small hold (K5.2c): the fit shows the racks, zooming out shows the whole hull (K5.2d).
        Rect whole = new(Extent.X - 2 * Extent.Width, Extent.Y - 2 * Extent.Height, 5 * Extent.Width, 5 * Extent.Height);
        var basis = ViewBasis.Of(Extent, Anchor, Viewport, Margin, whole: whole);
        var wholeScale = ViewTransform.Fit(whole, Viewport, Margin).Scale;
        wholeScale.Should().BeLessThan(basis.Fit.Scale * ZoomLimits.MinShareOfFit);

        ViewNavigation.Default.ZoomAt(new Point(50, 50), 0.01, basis, Limits).Framing!.PxPerCell
            .Should().BeApproximately(wholeScale, 1e-9);
    }

    [Fact]
    public void A_whole_scene_within_half_the_fit_leaves_the_lower_limit_at_half_the_fit()
    {
        // The Hull A, the Hull C, a hologram or the wedge: the scene is hardly larger than the fit (D76 as before).
        var basis = ViewBasis.Of(Extent, Anchor, Viewport, Margin, whole: Extent);

        Limits.Lower(basis).Should().BeApproximately(basis.Fit.Scale * ZoomLimits.MinShareOfFit, 1e-9);
    }

    [Fact]
    public void A_fit_above_the_maximum_stays_the_upper_limit()
    {
        // A small ship in a large overlay: the fit is always reachable, zooming in from it does nothing.
        var basis = Basis(viewport: new Size(1600, 1200));
        ZoomLimits limits = new(40);
        basis.Fit.Scale.Should().BeGreaterThan(40);

        limits.Upper(basis).Should().Be(basis.Fit.Scale);
        ViewNavigation.Default.ZoomAt(new Point(50, 50), 2, basis, limits).Should().Be(ViewNavigation.Default);
        ViewNavigation.Default.ZoomAt(new Point(50, 50), 0.5, basis, limits).Framing!.PxPerCell.Should().BeApproximately(basis.Fit.Scale / 2, 1e-9);
    }

    [Fact]
    public void Zooming_at_the_limit_does_not_move_the_view()
    {
        var basis = Basis();
        var atMax = ViewNavigation.Default.ZoomAt(new Point(50, 50), 1000, basis, Limits);

        atMax.ZoomAt(new Point(300, 10), 2, basis, Limits).Should().Be(atMax);
    }

    [Fact]
    public void A_lower_maximum_shrinks_a_framed_view_around_its_centre_at_once()
    {
        // The preference applies at once (D30): the transform clamps, Clamped writes it back.
        var basis = Basis();
        var navigation = Framed(basis);
        var middle = navigation.On(basis, Limits).Invert(basis.Centre);
        ZoomLimits lower = new(40);

        navigation.On(basis, lower).Scale.Should().Be(40);
        ShouldBeClose(navigation.On(basis, lower).Invert(basis.Centre), middle);
        navigation.Clamped(basis, lower).Framing.Should().Be(navigation.Framing! with { PxPerCell = 40 });
        navigation.Clamped(basis, Limits).Should().Be(navigation);
    }

    [Fact]
    public void Panning_moves_the_picture_by_the_pixels_dragged()
    {
        var basis = Basis();
        var navigation = Framed(basis);
        Point shipPoint = new(0.5, 1.5);
        var before = navigation.On(basis, Limits).Apply(shipPoint);

        var after = navigation.PanBy(new Vector(25, 40), basis, Limits).On(basis, Limits).Apply(shipPoint);

        ShouldBeClose(after, before + new Vector(25, 40));
    }

    [Fact]
    public void Panning_adds_up()
    {
        var basis = Basis();
        var navigation = Framed(basis);

        var twice = navigation.PanBy(new Vector(10, 5), basis, Limits).PanBy(new Vector(-3, 2), basis, Limits);
        var once = navigation.PanBy(new Vector(7, 7), basis, Limits);

        twice.Framing!.Pan.X.Should().BeApproximately(once.Framing!.Pan.X, 1e-9);
        twice.Framing.Pan.Y.Should().BeApproximately(once.Framing.Pan.Y, 1e-9);
    }

    [Fact]
    public void Pan_is_in_cells_so_another_window_size_keeps_the_same_point_in_the_middle()
    {
        // K1.2: a restart with another overlay size shows the same rack in the middle at the same size.
        var navigation = Framed(Basis());
        var small = Basis();
        var large = Basis(viewport: new Size(800, 500));

        var inSmall = navigation.On(small, Limits);
        var inLarge = navigation.On(large, Limits);

        inLarge.Scale.Should().Be(inSmall.Scale);
        ShouldBeClose(inSmall.Invert(small.Centre), Anchor + navigation.Framing!.Pan);
        ShouldBeClose(inLarge.Invert(large.Centre), Anchor + navigation.Framing.Pan);
    }

    [Fact]
    public void A_framed_view_hangs_on_the_anchor_not_on_the_fit_extent()
    {
        // Martin at the approval: a sync that loads a rack further out changes the fit extent; the view must not move.
        var navigation = Framed(Basis());
        Rect wider = new(-6, -5, 30, 12);

        ShouldBeTheSame(navigation.On(Basis(wider), Limits), navigation.On(Basis(), Limits));
    }

    [Fact]
    public void The_zoom_on_top_of_the_fit_is_the_view_s_scale_over_the_fit_s()
    {
        // What the hologram's edge levels read (D63).
        var basis = Basis();

        ViewNavigation.Default.ZoomOn(basis, Limits).Should().Be(1);
        Framed(basis).ZoomOn(basis, Limits).Should().BeApproximately(2, 1e-9);
    }

    [Fact]
    public void Default_navigation_looks_from_the_default_camera_and_fits()
    {
        ViewNavigation.Default.Camera.Should().Be(IsoProjection.Default);
        ViewNavigation.Default.Framing.Should().BeNull();
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("caterpillar")]
    public void Default_navigation_for_a_ship_looks_from_its_default_camera(string shipId)
    {
        var bounds = ShipLoader.Load(shipId).Bounds;

        ViewNavigation.DefaultFor(bounds).Should().Be(new ViewNavigation(IsoProjection.DefaultFor(bounds)));
    }

    [Fact]
    public void Orbiting_turns_the_camera_and_keeps_the_framing()
    {
        var navigation = Framed(Basis());

        var orbited = navigation.OrbitBy(30, -10);

        orbited.Camera.Should().Be(navigation.Camera.OrbitBy(30, -10));
        orbited.Framing.Should().Be(navigation.Framing);
    }

    // Since K5.5 orbiting holds the scale: refitting at every angle made a hologram's size pump while turning (Hull C
    // 39 % over a turn). The fit becomes the framing at the first step, as panning makes it.
    [Fact]
    public void Orbiting_the_fit_fixes_its_scale()
    {
        var basis = Basis();

        var orbited = ViewNavigation.Default.OrbitBy(30, -10, basis);

        orbited.Framing.Should().Be(Framing.OfFit(basis));
        orbited.Camera.Should().Be(ViewNavigation.Default.Camera.OrbitBy(30, -10));
    }

    [Fact]
    public void The_first_orbit_step_draws_the_fit_it_starts_from()
    {
        var basis = Basis();

        var orbited = ViewNavigation.Default.OrbitBy(30, -10, basis);

        ShouldBeTheSame(orbited.On(basis, Limits), basis.Fit);
    }

    [Fact]
    public void Further_orbit_steps_keep_the_scale_whatever_the_fit_would_be()
    {
        // Another angle projects the ship wider: its fit is smaller, the framing stays.
        var start = Basis();
        var wider = Basis(new Rect(Extent.X, Extent.Y, 2 * Extent.Width, Extent.Height));

        var orbited = ViewNavigation.Default.OrbitBy(10, 0, start).OrbitBy(10, 0, wider).OrbitBy(10, 0, wider);

        orbited.Framing!.PxPerCell.Should().Be(start.Fit.Scale);
        wider.Fit.Scale.Should().BeLessThan(start.Fit.Scale);
    }

    [Fact]
    public void Zooming_and_panning_keep_the_camera()
    {
        var basis = Basis();
        var orbited = ViewNavigation.Default.OrbitBy(30, -10);

        orbited.ZoomAt(new Point(50, 50), 2, basis, Limits).PanBy(new Vector(10, 5), basis, Limits).Camera.Should().Be(orbited.Camera);
    }
}
