using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Paints a <see cref="ShipScene"/> as <see cref="SceneProjector"/> projects it for the current camera: frame, racks
/// back to front, rack numbers on top. Only redraws when the scene, the size, the camera, zoom or pan change; no
/// timer, no animation (D14). Mouse: left drag turns and tilts the camera, right drag pans, wheel zooms around the
/// cursor, double-click resets, hover shows the placement. Left clicks are handled here, so the window's DragMove
/// only works outside the view (D15).
/// </summary>
public sealed class ShipViewControl : FrameworkElement
{
    private const double FitMargin = 8;
    private const double MinLabelSize = 12;
    private const double LabelSizePerCell = 0.9;
    private const double WheelZoomStep = 1.2;
    private const double OrbitDegreesPerPixel = 0.5;

    private static readonly Pen FramePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Frame), 1) { DashStyle = DashStyles.Dash });
    private static readonly Pen SegmentPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.RackEdge), 0.5));
    private static readonly Pen RackPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.RackEdge), 1));
    private static readonly Pen ViolationPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Violation), 2.5));
    private static readonly Pen LabelOutlinePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.LabelOutline), 3) { LineJoin = PenLineJoin.Round });
    private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(MissionPalette.LabelFill));

    // AffectsRender: a new scene invalidates the drawing by itself.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkpropertymetadataoptions
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(ShipScene),
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ShipViewControl)d).OnSceneChanged()));

    // Opened by hand instead of ToolTipService: the text changes with the segment under the cursor.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/controls/how-to-position-a-tooltip
    private readonly ToolTip _toolTip;

    private ViewNavigation _navigation = ViewNavigation.Default;
    private Drag? _drag;

    // The scene as the current camera sees it; null after a new scene or camera until the next use.
    private ProjectedScene? _projected;

    public ShipViewControl()
    {
        _toolTip = new ToolTip { PlacementTarget = this, Placement = PlacementMode.Mouse };

        // Zoomed or panned racks must not paint over the header or the legend.
        ClipToBounds = true;
    }

    public ShipScene? Scene
    {
        get => (ShipScene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>
    /// False while the overlay is locked: wheel, both drags and hover do nothing. Click-through alone isn't enough, the
    /// wheel can go to the focused window instead of the one under the cursor (D15).
    /// </summary>
    public bool IsNavigationEnabled
    {
        get;
        set
        {
            field = value;
            if (!value)
            {
                ShowToolTip(null);
                ReleaseMouseCapture();
            }
        }
    } = true;

    protected override void OnRender(DrawingContext dc)
    {
        // An almost transparent background: a layered window passes clicks on fully transparent pixels through.
        dc.DrawRectangle((Brush)FindResource("HitTestBackgroundBrush"), null, new Rect(RenderSize));
        if (Projected() is not { } scene)
        {
            return;
        }

        var transform = CurrentTransform(scene);

        foreach (var line in scene.Frame)
        {
            dc.DrawLine(FramePen, transform.Apply(line.From), transform.Apply(line.To));
        }

        // Each rack completely, back to front, so the racks in front cover the ones behind.
        foreach (var rack in scene.Racks)
        {
            foreach (var strip in rack.Strips)
            {
                dc.DrawGeometry(Fill(strip), SegmentPen, Polygon(strip.Polygon, transform));
            }

            var edge = rack.IsViolated ? ViolationPen : RackPen;
            foreach (var face in rack.Faces)
            {
                dc.DrawGeometry(null, edge, Polygon(face.Polygon, transform));
            }
        }

        // Numbers last: always visible, even where a rack in front covers the labelled face.
        var size = Math.Max(MinLabelSize, LabelSizePerCell * transform.Scale);
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var rack in scene.Racks)
        {
            FormattedText text = new(
                rack.Number.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                LabelBrush,
                pixelsPerDip);
            var centre = transform.Apply(rack.LabelCentre);
            var glyphs = text.BuildGeometry(new Point(centre.X - text.Width / 2, centre.Y - text.Height / 2));

            // Outline first, fill on top: the outline only shows outside the glyphs.
            dc.DrawGeometry(null, LabelOutlinePen, glyphs);
            dc.DrawGeometry(LabelBrush, null, glyphs);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Handled marks the underlying MouseDown handled for the whole route, so the window's DragMove doesn't run.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.uielement.mouseleftbuttondown
        e.Handled = true;
        if (!IsNavigationEnabled)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            Navigate(ViewNavigation.Default);
            return;
        }

        StartDrag(e, DragMode.Orbit);
    }

    // Source: https://learn.microsoft.com/dotnet/api/system.windows.uielement.onmouserightbuttondown
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (IsNavigationEnabled)
        {
            StartDrag(e, DragMode.Pan);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => EndDrag(e, DragMode.Orbit);

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) => EndDrag(e, DragMode.Pan);

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        _drag = null;
        base.OnLostMouseCapture(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!IsNavigationEnabled || Projected() is not { } scene)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_drag is { } drag)
        {
            var delta = position - drag.From;
            _drag = drag with { From = position };

            // Like grabbing the ship: dragging right turns its near side right, dragging down tilts its top towards you.
            Navigate(drag.Mode == DragMode.Orbit
                ? _navigation.OrbitBy(-delta.X * OrbitDegreesPerPixel, delta.Y * OrbitDegreesPerPixel)
                : _navigation.PanBy(delta));
            return;
        }

        ShowToolTip(SceneHitTest.ToolTipAt(scene, CurrentTransform(scene).Invert(position)));
    }

    protected override void OnMouseLeave(MouseEventArgs e) => ShowToolTip(null);

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (!IsNavigationEnabled)
        {
            return;
        }

        // One notch (120) = one step; finer wheels send smaller deltas.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.input.mouse.mousewheeldeltaforoneline
        var factor = Math.Pow(WheelZoomStep, (double)e.Delta / Mouse.MouseWheelDeltaForOneLine);
        ShowToolTip(null);
        Navigate(_navigation.ZoomAt(e.GetPosition(this), factor));
    }

    private ProjectedScene? Projected() => Scene is { } scene ? _projected ??= SceneProjector.Project(scene, _navigation.Camera) : null;

    // The fit follows the projected extent, so the ship stays in view while the camera turns.
    private ViewTransform CurrentTransform(ProjectedScene scene) => _navigation.On(ViewTransform.Fit(scene.Extent, RenderSize, FitMargin));

    private void OnSceneChanged()
    {
        _projected = null;
        ShowToolTip(null);
    }

    private void Navigate(ViewNavigation navigation)
    {
        if (navigation.Camera != _navigation.Camera)
        {
            _projected = null;
        }

        _navigation = navigation;
        InvalidateVisual();
    }

    private void StartDrag(MouseButtonEventArgs e, DragMode mode)
    {
        // One drag at a time: the other button doesn't take over a running drag.
        if (_drag is not null)
        {
            return;
        }

        ShowToolTip(null);
        _drag = new Drag(e.GetPosition(this), mode);
        CaptureMouse();
    }

    private void EndDrag(MouseButtonEventArgs e, DragMode mode)
    {
        if (_drag?.Mode == mode)
        {
            e.Handled = true;
            ReleaseMouseCapture();
        }
    }

    private void ShowToolTip(string? text)
    {
        if (Equals(_toolTip.Content, text))
        {
            return;
        }

        // Close and reopen, so the tool tip moves to the cursor (PlacementMode.Mouse places it on opening).
        _toolTip.IsOpen = false;
        _toolTip.Content = text;
        _toolTip.IsOpen = text is not null;
    }

    private static Brush Fill(SceneStrip strip) => strip.Fill switch
    {
        SegmentFill.Mission => MissionPalette.Brush(strip.PaletteIndex!.Value),
        SegmentFill.Delivered => MissionPalette.DeliveredBrush,
        _ => MissionPalette.FreeBrush,
    };

    // StreamGeometry: the lightweight geometry for drawing only, frozen after filling.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.streamgeometrycontext
    private static StreamGeometry Polygon(IReadOnlyList<Point> points, ViewTransform transform)
    {
        StreamGeometry geometry = new();
        using (var context = geometry.Open())
        {
            context.BeginFigure(transform.Apply(points[0]), isFilled: true, isClosed: true);
            context.PolyLineTo([.. points.Skip(1).Select(transform.Apply)], isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private enum DragMode
    {
        Orbit,
        Pan,
    }

    private readonly record struct Drag(Point From, DragMode Mode);
}
