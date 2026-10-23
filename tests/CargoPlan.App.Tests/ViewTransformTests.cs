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

    [Fact]
    public void Default_navigation_is_the_fit_itself()
    {
        var fit = ViewTransform.Fit(Extent, new Size(400, 300), Margin);

        ViewNavigation.Default.On(fit).Should().Be(fit);
    }

    [Fact]
    public void Zooming_keeps_the_point_under_the_cursor_in_place()
    {
        var fit = ViewTransform.Fit(Extent, new Size(400, 300), Margin);
        var navigation = ViewNavigation.Default.PanBy(new Vector(30, -10));
        Point cursor = new(120, 80);
        var shipPoint = navigation.On(fit).Invert(cursor);

        var zoomed = navigation.ZoomAt(cursor, 1.2);

        var after = zoomed.On(fit).Apply(shipPoint);
        after.X.Should().BeApproximately(cursor.X, 1e-9);
        after.Y.Should().BeApproximately(cursor.Y, 1e-9);
        zoomed.Zoom.Should().BeApproximately(1.2, 1e-9);
    }

    [Theory]
    [InlineData(100, ViewNavigation.MaxZoom)]
    [InlineData(0.01, ViewNavigation.MinZoom)]
    public void Zoom_stays_within_its_limits(double factor, double expected)
    {
        ViewNavigation.Default.ZoomAt(new Point(50, 50), factor).Zoom.Should().Be(expected);
    }

    [Fact]
    public void Zooming_at_the_limit_does_not_move_the_view()
    {
        var atMax = ViewNavigation.Default.ZoomAt(new Point(50, 50), 100);

        atMax.ZoomAt(new Point(300, 10), 2).Should().Be(atMax);
    }

    [Fact]
    public void Panning_adds_up()
    {
        ViewNavigation.Default.PanBy(new Vector(10, 5)).PanBy(new Vector(-3, 2)).Pan.Should().Be(new Vector(7, 7));
    }

    [Fact]
    public void Default_navigation_looks_from_the_default_camera()
    {
        ViewNavigation.Default.Camera.Should().Be(IsoProjection.Default);
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("caterpillar")]
    public void Default_navigation_for_a_ship_looks_from_its_default_camera(string shipId)
    {
        var bounds = ShipLoader.Load(shipId).Bounds;

        ViewNavigation.DefaultFor(bounds).Should().Be(ViewNavigation.Default with { Camera = IsoProjection.DefaultFor(bounds) });
    }

    [Fact]
    public void Orbiting_turns_the_camera_and_keeps_zoom_and_pan()
    {
        var navigation = ViewNavigation.Default.ZoomAt(new Point(50, 50), 2).PanBy(new Vector(10, 5));

        var orbited = navigation.OrbitBy(30, -10);

        orbited.Camera.Should().Be(navigation.Camera.OrbitBy(30, -10));
        orbited.Zoom.Should().Be(navigation.Zoom);
        orbited.Pan.Should().Be(navigation.Pan);
    }

    [Fact]
    public void Zooming_and_panning_keep_the_camera()
    {
        var orbited = ViewNavigation.Default.OrbitBy(30, -10);

        orbited.ZoomAt(new Point(50, 50), 2).PanBy(new Vector(10, 5)).Camera.Should().Be(orbited.Camera);
    }
}
