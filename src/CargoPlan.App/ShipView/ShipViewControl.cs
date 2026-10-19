using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Paints a <see cref="ShipScene"/> as <see cref="SceneProjector"/> projects it for the current camera: frame, rack
/// edges, the boxes of each rack back to front, red and cyan edges, rack numbers on top. Only redraws when the scene, the size, the camera, zoom or pan change; no
/// timer, no animation (D14). Mouse (rules in <see cref="DragGesture"/>): left drag turns and tilts the camera, a click
/// on cargo ticks its stop off or back (D16), right drag on cargo moves it to another rack (D20), right drag elsewhere
/// pans, wheel zooms around the cursor, double-click on free or empty space resets, hover shows the crate. Left
/// clicks are handled here, so the window's DragMove only works outside the view (D15).
/// </summary>
public sealed class ShipViewControl : FrameworkElement
{
    private const double FitMargin = 8;
    private const double MinLabelSize = 12;
    private const double LabelSizePerCell = 0.9;
    private const double WheelZoomStep = 1.2;
    private const double OrbitDegreesPerPixel = 0.5;

    // The dragged cargo stays in place, faded, so the rack shows what it holds without it (D20).
    private const double DraggedOpacity = 0.35;

    private static readonly Pen FramePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Frame), 1) { DashStyle = DashStyles.Dash });
    private static readonly Pen CellPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.RackEdge), 0.5));
    private static readonly Pen FreeCellPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.FreeEdge), 0.5));
    private static readonly Pen RackPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.RackEdge), 1));
    private static readonly Pen ViolationPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Violation), 2.5));
    private static readonly Pen DragTargetPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.DragTarget), 2.5));
    private static readonly Pen LabelOutlinePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.LabelOutline), 3) { LineJoin = PenLineJoin.Round });
    private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(MissionPalette.LabelFill));

    // AffectsRender: a new scene invalidates the drawing by itself.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkpropertymetadataoptions
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(ShipScene),
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ShipViewControl)d).OnSceneChanged()));

    /// <summary>Runs with the clicked cargo's destination as parameter.</summary>
    public static readonly DependencyProperty ToggleStopCommandProperty = DependencyProperty.Register(
        nameof(ToggleStopCommand),
        typeof(ICommand),
        typeof(ShipViewControl));

    /// <summary>Runs with a <see cref="Gesture.Move"/> when cargo is dropped onto another rack.</summary>
    public static readonly DependencyProperty MovePlacementCommandProperty = DependencyProperty.Register(
        nameof(MovePlacementCommand),
        typeof(ICommand),
        typeof(ShipViewControl));

    // Opened by hand instead of ToolTipService: the text changes with the crate under the cursor.
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

    public ICommand? ToggleStopCommand
    {
        get => (ICommand?)GetValue(ToggleStopCommandProperty);
        set => SetValue(ToggleStopCommandProperty, value);
    }

    public ICommand? MovePlacementCommand
    {
        get => (ICommand?)GetValue(MovePlacementCommandProperty);
        set => SetValue(MovePlacementCommandProperty, value);
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

        // Only once the drag has left the drag rectangle: a right click alone changes nothing on screen.
        var (dragged, target) = _drag is { IsMoving: true } drag ? (drag.Dragged, drag.Target) : (null, null);

        // Plain rack edges first: cargo in front covers them like any other part of the ship (D21).
        foreach (var rack in scene.Racks.Where(r => !IsHighlighted(r, target)))
        {
            DrawEdges(dc, rack, RackPen, transform);
        }

        // Rack by rack back to front, each rack's boxes back to front: the boxes in front cover the ones behind (D21).
        foreach (var box in scene.Racks.SelectMany(r => r.Boxes))
        {
            // Value equality: an equal twin fades too; MovePlacement moves either, they are interchangeable.
            var fade = dragged is not null && box.Box.Placement == dragged;
            if (fade)
            {
                dc.PushOpacity(DraggedOpacity);
            }

            var (fill, pen) = box.Box.Fill == CellFill.Free ? (null, FreeCellPen) : (Fill(box.Box), CellPen);
            foreach (var face in box.Faces)
            {
                dc.DrawGeometry(fill, pen, Polygon(face.Polygon, transform));
            }

            if (fade)
            {
                dc.Pop();
            }
        }

        // Red and cyan on top, like the numbers: a violation or the drop target must never hide behind cargo.
        foreach (var rack in scene.Racks.Where(r => IsHighlighted(r, target)))
        {
            DrawEdges(dc, rack, rack.RackId == target ? DragTargetPen : ViolationPen, transform);
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

        // Reset only off the cargo: there the second click of a double-click is a click of its own and ticks back.
        if (e.ClickCount == 2 && DestinationAt(e.GetPosition(this)) is null)
        {
            Navigate(ViewNavigation.Default);
            return;
        }

        StartDrag(e, MouseButton.Left);
    }

    // Source: https://learn.microsoft.com/dotnet/api/system.windows.uielement.onmouserightbuttondown
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (IsNavigationEnabled)
        {
            StartDrag(e, MouseButton.Right);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => EndDrag(e, MouseButton.Left);

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) => EndDrag(e, MouseButton.Right);

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        // A faded placement or a highlighted target must not stay on screen.
        if (_drag is { IsMoving: true, Dragged: not null })
        {
            InvalidateVisual();
        }

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
            // Inside the drag rectangle it may still be a click: the ship stands still until the drag leaves it.
            if (!drag.IsMoving && IsWithinDragRectangle(drag.From, position))
            {
                return;
            }

            var delta = position - drag.From;
            var gesture = DragGesture.Resolve(drag.Button, drag.Down, moved: true, HitAt(position));
            var target = (gesture as Gesture.Move)?.TargetRackId;
            _drag = drag with { From = position, IsMoving = true, Target = target };

            switch (gesture)
            {
                // Like grabbing the ship: dragging right turns its near side right, dragging down tilts its top towards you.
                case Gesture.Orbit:
                    Navigate(_navigation.OrbitBy(-delta.X * OrbitDegreesPerPixel, delta.Y * OrbitDegreesPerPixel));
                    break;
                case Gesture.Pan:
                    Navigate(_navigation.PanBy(delta));
                    break;

                // Moving cargo: the ship stands still; redraw only when the fade starts or the target changes.
                case Gesture.Move or Gesture.None when !drag.IsMoving || target != drag.Target:
                    InvalidateVisual();
                    break;
            }

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

    /// <summary>
    /// Still within the system's drag rectangle around the press (SM_CXDRAG/SM_CYDRAG): a click so far, not a drag.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.minimumhorizontaldragdistance
    /// </summary>
    private static bool IsWithinDragRectangle(Point start, Point end) =>
        Math.Abs(end.X - start.X) <= SystemParameters.MinimumHorizontalDragDistance
        && Math.Abs(end.Y - start.Y) <= SystemParameters.MinimumVerticalDragDistance;

    private string? DestinationAt(Point position) =>
        Projected() is { } scene ? SceneHitTest.DestinationAt(scene, CurrentTransform(scene).Invert(position)) : null;

    private SceneHit? HitAt(Point position) =>
        Projected() is { } scene ? SceneHitTest.HitAt(scene, CurrentTransform(scene).Invert(position)) : null;

    private ProjectedScene? Projected() => Scene is { } scene ? _projected ??= SceneProjector.Project(scene, _navigation.Camera) : null;

    // The fit follows the projected extent, so the ship stays in view while the camera turns.
    private ViewTransform CurrentTransform(ProjectedScene scene) => _navigation.On(ViewTransform.Fit(scene.Extent, RenderSize, FitMargin));

    private void OnSceneChanged()
    {
        _projected = null;
        ShowToolTip(null);

        // A drag holds a placement of the old plan; MovePlacement would not find it in the new one.
        ReleaseMouseCapture();
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

    private void StartDrag(MouseButtonEventArgs e, MouseButton button)
    {
        // One drag at a time: the other button doesn't take over a running drag.
        if (_drag is not null)
        {
            return;
        }

        ShowToolTip(null);
        var position = e.GetPosition(this);
        var down = HitAt(position);
        _drag = new Drag(button, down, DragGesture.DraggedPlacement(button, down), position, IsMoving: false, Target: null);
        CaptureMouse();
    }

    private void EndDrag(MouseButtonEventArgs e, MouseButton button)
    {
        if (_drag is not { } drag || drag.Button != button)
        {
            return;
        }

        e.Handled = true;

        // Resolve before releasing the capture, which clears the drag; run the command after, so the new plan is drawn
        // without the fade.
        var gesture = DragGesture.Resolve(button, drag.Down, drag.IsMoving, HitAt(e.GetPosition(this)));
        ReleaseMouseCapture();
        switch (gesture)
        {
            case Gesture.Click click:
                Run(ToggleStopCommand, click.Destination);
                break;
            case Gesture.Move move:
                Run(MovePlacementCommand, move);
                break;
        }
    }

    private static void Run(ICommand? command, object parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
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

    private static bool IsHighlighted(ProjectedRack rack, string? target) => rack.RackId == target || rack.IsViolated;

    private static void DrawEdges(DrawingContext dc, ProjectedRack rack, Pen pen, ViewTransform transform)
    {
        foreach (var face in rack.Faces)
        {
            dc.DrawGeometry(null, pen, Polygon(face.Polygon, transform));
        }
    }

    /// <summary>Cargo only; free cells have no fill.</summary>
    private static Brush Fill(SceneBox box) =>
        box.Fill == CellFill.Delivered ? MissionPalette.DeliveredBrush : MissionPalette.Brush(box.PaletteIndex!.Value);

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

    /// <param name="Down">What lay under the cursor when the button went down; with the button it decides the gesture.</param>
    /// <param name="Dragged">The placement a right drag picked up (<see cref="DragGesture.DraggedPlacement"/>), drawn faded.</param>
    /// <param name="From">Where the button went down until the drag moves, then the last position.</param>
    /// <param name="IsMoving">Left the drag rectangle; until then, releasing the button is a click.</param>
    /// <param name="Target">The rack <paramref name="Dragged"/> would drop into now, highlighted; null if none.</param>
    private readonly record struct Drag(MouseButton Button, SceneHit? Down, Placement? Dragged, Point From, bool IsMoving, string? Target);
}
