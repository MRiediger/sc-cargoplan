using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Routing;

namespace CargoPlan.App.Routing;

/// <summary>
/// The Plan tab's map (T13.7, D128): the scene's graph over the layout of the system's nodes. One
/// <see cref="DrawingVisual"/> draws edges, nodes, rings and the route; <see cref="TextBlock"/>s in the legend's style carry
/// the labels and the route's numbers. A new scene builds the labels anew; zoom and pan only project (K4): the drawing is
/// redrawn from the cached layout and the labels move, text and lines keep their size.
/// Wheel = zoom about the pointer (0.5× to 6×), drag on free room = pan (it takes the left button, so free room here
/// doesn't move the window), double click on free room = fit, click on a node = <see cref="FocusCommand"/>; with a
/// station in focus a click on a lit edge = <see cref="AddEdgeCommand"/>, a right click = <see cref="RemoveEdgeCommand"/>
/// (T13.7b), the custom route drawn in the line's colour; a tool tip
/// for the node or edge under the pointer. Locked, the window is click-through and none of this reaches it (D11).
/// </summary>
public sealed class RouteMapView : Canvas
{
    public const double MinZoom = 0.5;

    public const double MaxZoom = 6;

    /// <summary>One notch of the wheel zooms by this factor.</summary>
    public const double ZoomStep = 1.2;

    /// <summary>A press that moves less than this is a click, more is a pan.</summary>
    private const double DragPx = 4;

    // A node takes the pointer this far beyond its rim.
    private const double NodeHitPx = 4;

    private const double DimmedOpacity = 0.25;

    private const double ArrowLength = 7;

    private const double ArrowHalfWidth = 3;

    private const double RouteThickness = 3;

    private const double DraftThickness = 2.5;

    private const double ObservedMarkRadius = 2.5;

    private const double RingGap = 4;

    private const string LabelStyleKey = "LegendTextStyle";

    private const string LineBrushKey = "LegendForegroundBrush";

    private const string RouteBrushKey = "NoticeForegroundBrush";

    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene), typeof(MapScene), typeof(RouteMapView), new PropertyMetadata(null, (d, _) => ((RouteMapView)d).Rebuild()));

    public static readonly DependencyProperty FocusCommandProperty = DependencyProperty.Register(
        nameof(FocusCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty AddEdgeCommandProperty = DependencyProperty.Register(
        nameof(AddEdgeCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty RemoveEdgeCommandProperty = DependencyProperty.Register(
        nameof(RemoveEdgeCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty PaletteProperty = DependencyProperty.Register(
        nameof(Palette), typeof(StopPalette), typeof(RouteMapView), new PropertyMetadata(StopPalettes.OkabeIto, (d, _) => ((RouteMapView)d).Redraw()));

    private static readonly double[] Thickness = [1, 2, 3.5];

    private static readonly DashStyle Dashed = Frozen(new DashStyle([4, 3], 0));

    private readonly DrawingHost _drawing = new();
    private readonly Dictionary<string, TextBlock> _labels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _numbers = new(StringComparer.Ordinal);
    private readonly ToolTip _tip = new() { Placement = PlacementMode.Relative, StaysOpen = true };
    private readonly DispatcherTimer _tipDelay;

    // The edges as drawn, for the hit test (MapGeometry.Nearest).
    private readonly List<(MapEdge Edge, MapPoint A, MapPoint B)> _lines = [];

    private MapLayout? _layout;
    private IReadOnlyList<MapNode>? _laidOut;
    private Size _laidOutFor;
    private MapPoint _pan;
    private Point? _press;
    private bool _panning;
    private object? _hovered;

    public RouteMapView()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;
        Focusable = false;
        Children.Add(_drawing);
        _tip.PlacementTarget = this;
        // The system's hover time, as a tool tip waits. Source: https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.mousehovertime
        _tipDelay = new DispatcherTimer(DispatcherPriority.Input) { Interval = SystemParameters.MouseHoverTime };
        _tipDelay.Tick += (_, _) => OpenTip();
        IsVisibleChanged += (_, e) =>
        {
            // Fit each time the view opens (startprompt 2.5: no state kept).
            if (e.NewValue is true)
            {
                Fit();
            }
            else
            {
                CloseTip();
            }
        };
    }

    public MapScene? Scene
    {
        get => (MapScene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets a node's key on a click (RoutesViewModel.FocusCommand).</summary>
    public ICommand? FocusCommand
    {
        get => (ICommand?)GetValue(FocusCommandProperty);
        set => SetValue(FocusCommandProperty, value);
    }

    /// <summary>Gets an edge on a click while the scene can pick (T13.7b: RoutesViewModel.AddEdgeCommand).</summary>
    public ICommand? AddEdgeCommand
    {
        get => (ICommand?)GetValue(AddEdgeCommandProperty);
        set => SetValue(AddEdgeCommandProperty, value);
    }

    /// <summary>Gets an edge on a right click while the scene can pick (T13.7b: RoutesViewModel.RemoveEdgeCommand).</summary>
    public ICommand? RemoveEdgeCommand
    {
        get => (ICommand?)GetValue(RemoveEdgeCommandProperty);
        set => SetValue(RemoveEdgeCommandProperty, value);
    }

    /// <summary>The legend's stop colours (K4.2): an edge's colour is its highest rank's.</summary>
    public StopPalette Palette
    {
        get => (StopPalette)GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>The zoom about the view's centre; 1 = fit.</summary>
    public double Zoom { get; private set; } = 1;

    internal MapPoint Pan => _pan;

    internal MapLayout? Layout => _layout;

    internal IReadOnlyDictionary<string, TextBlock> Labels => _labels;

    internal IReadOnlyDictionary<string, TextBlock> Numbers => _numbers;

    /// <summary>How often the drawing was redrawn: a zoom redraws, it builds nothing.</summary>
    internal int Redraws { get; private set; }

    /// <summary>The tool tip's text for what lies under <paramref name="point"/>: a node first, else the nearest edge; null for free room.</summary>
    internal string? ToolTipAt(Point point) => Hit(point) switch
    {
        MapNode node when Scene is { } scene => scene.NodeToolTip(node),
        MapEdge edge when Scene is { } scene => scene.EdgeToolTip(edge),
        _ => null,
    };

    /// <summary>Back to the layout as made for the view: zoom 1, no pan.</summary>
    public void Fit()
    {
        (Zoom, _pan) = (1, default);
        Redraw();
    }

    /// <summary>One wheel's worth of zoom about <paramref name="at"/>: what lies under the pointer stays there.</summary>
    internal void ZoomAt(Point at, int delta)
    {
        if (_layout is not { } layout || delta == 0)
        {
            return;
        }

        var zoom = Math.Clamp(Zoom * Math.Pow(ZoomStep, delta / (double)Mouse.MouseWheelDeltaForOneLine), MinZoom, MaxZoom);
        var c = layout.Centre;

        // The layout's point under the pointer: at = c + zoom (p − c) + pan, kept for the new zoom.
        var (px, py) = ((at.X - c.X - _pan.X) / Zoom, (at.Y - c.Y - _pan.Y) / Zoom);
        _pan = new MapPoint(at.X - c.X - (zoom * px), at.Y - c.Y - (zoom * py));
        Zoom = zoom;
        Redraw();
    }

    internal void PanBy(Vector by) => Project(Zoom, new MapPoint(_pan.X + by.X, _pan.Y + by.Y));

    /// <summary>Shows the layout at <paramref name="zoom"/> (clamped) and <paramref name="pan"/>: a projection only (K4; the frame measurement drives it).</summary>
    internal void Project(double zoom, MapPoint pan)
    {
        (Zoom, _pan) = (Math.Clamp(zoom, MinZoom, MaxZoom), pan);
        Redraw();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // A new size lays the system out anew for it, then fits: the layout belongs to the view's size.
        Rebuild(sizeInfo.NewSize);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.GetPosition(this), e.Delta);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Ours, not the window's DragMove (OverlayWindow.OnMouseLeftButtonDown): free room here pans.
        e.Handled = true;
        var at = e.GetPosition(this);
        if (e.ClickCount == 2 && Hit(at) is null)
        {
            Fit();
            return;
        }

        (_press, _panning) = (at, false);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var at = e.GetPosition(this);
        if (_press is { } press && e.LeftButton == MouseButtonState.Pressed)
        {
            if (!_panning && (at - press).Length >= DragPx && Hit(press) is not MapNode)
            {
                _panning = true;
                CloseTip();
            }

            if (_panning)
            {
                PanBy(at - press);
                _press = at;
            }

            return;
        }

        Hover(at);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        switch (_press is not null && !_panning ? Hit(e.GetPosition(this)) : null)
        {
            case MapNode node when FocusCommand is { } focus && focus.CanExecute(node.Key):
                focus.Execute(node.Key);
                break;
            case MapEdge edge when Scene is { CanPick: true } && AddEdgeCommand is { } add && add.CanExecute(edge):
                // T13.7b: the edge's next contract into the custom route.
                add.Execute(edge);
                break;
        }

        (_press, _panning) = (null, false);
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);

        // T13.7b: the edge's last contract out of the custom route.
        if (Scene is { CanPick: true } && Hit(e.GetPosition(this)) is MapEdge edge && RemoveEdgeCommand is { } remove && remove.CanExecute(edge))
        {
            remove.Execute(edge);
            e.Handled = true;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CloseTip();
    }

    private void Rebuild() => Rebuild(new Size(ActualWidth, ActualHeight));

    // A new scene or size: the layout when the nodes or the size are new, the labels for the graph's nodes. The zoom stays
    // and the pan follows the new scale, so the part of the map in the middle stays there (gate finding 7b, Martin: the
    // window resized, or the custom route's bar taking room, reset the zoom). Fit only on opening and by double click.
    private void Rebuild(Size size)
    {
        CloseTip();
        if (Scene is not { } scene || size.Width <= 0 || size.Height <= 0)
        {
            ClearLabels();
            _drawing.Clear();
            return;
        }

        if (!ReferenceEquals(scene.Universe, _laidOut) || size != _laidOutFor)
        {
            var style = TryFindResource(LabelStyleKey) as Style;
            var before = _layout;
            _layout = MapLayout.For(scene.Universe, new MapSize(size.Width, size.Height), text => Measure(text, style));
            (_laidOut, _laidOutFor) = (scene.Universe, size);
            var ratio = before is { PixelsPerGm: > 0 } old ? _layout.PixelsPerGm / old.PixelsPerGm : 0;
            _pan = new MapPoint(_pan.X * ratio, _pan.Y * ratio);
        }

        ClearLabels();
        foreach (var node in scene.Graph.Nodes.Where(n => _layout!.Points.ContainsKey(n.Key)))
        {
            _labels[node.Key] = Label(node.Label, LabelStyleKey);
        }

        foreach (var (key, number) in scene.Graph.Numbers)
        {
            var block = Label(number, LabelStyleKey);
            block.FontWeight = FontWeights.Bold;
            block.SetResourceReference(TextBlock.ForegroundProperty, RouteBrushKey);
            _numbers[key] = block;
        }

        Redraw();
    }

    private TextBlock Label(string text, string styleKey)
    {
        var block = new TextBlock { Text = text, IsHitTestVisible = false, TextWrapping = TextWrapping.NoWrap };
        block.SetResourceReference(StyleProperty, styleKey);
        Children.Add(block);
        return block;
    }

    private void ClearLabels()
    {
        foreach (var block in _labels.Values.Concat(_numbers.Values))
        {
            Children.Remove(block);
        }

        _labels.Clear();
        _numbers.Clear();
    }

    private static MapSize Measure(string text, Style? style)
    {
        var block = new TextBlock { Text = text, Style = style, TextWrapping = TextWrapping.NoWrap };
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new MapSize(Math.Ceiling(block.DesiredSize.Width), Math.Ceiling(block.DesiredSize.Height));
    }

    private MapPoint At(string key) => _layout!.Project(_layout.Points[key], Zoom, _pan);

    // Zoom and pan: draw anew from the layout, move the labels; nothing is built.
    private void Redraw()
    {
        _lines.Clear();
        if (Scene is not { } scene || _layout is not { } layout)
        {
            _drawing.Clear();
            return;
        }

        Redraws++;
        var graph = scene.Graph;
        var line = TryFindResource(LineBrushKey) as Brush ?? Brushes.White;
        var route = TryFindResource(RouteBrushKey) as Brush ?? Brushes.Orange;
        var kinds = graph.Nodes.ToDictionary(n => n.Key, n => n.Kind, StringComparer.Ordinal);
        using (var dc = _drawing.Visual.RenderOpen())
        {
            // Dimmed first, then the lit ones over them, then the route over everything.
            foreach (var edge in graph.Edges.OrderBy(e => graph.IsLit(e)))
            {
                if (!layout.Points.ContainsKey(edge.From) || !layout.Points.ContainsKey(edge.To))
                {
                    continue;
                }

                var (a, b) = MapGeometry.Line(At(edge.From), At(edge.To), MapLayout.Radius(kinds[edge.From]), MapLayout.Radius(kinds[edge.To]), edge.TwoWay);
                _lines.Add((edge, a, b));
                var brush = Frozen(new SolidColorBrush(Palette.ColorOf((int)edge.TopRank)));
                var pen = Frozen(new Pen(brush, Thickness[edge.Strength - 1]) { DashStyle = edge.Observed ? DashStyles.Solid : Dashed });
                var lit = graph.IsLit(edge);
                if (!lit)
                {
                    dc.PushOpacity(DimmedOpacity);
                }

                dc.DrawLine(pen, P(a), P(b));
                Arrow(dc, a, b, brush);
                if (edge.Observed)
                {
                    dc.DrawEllipse(brush, null, new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), ObservedMarkRadius, ObservedMarkRadius);
                }

                if (!lit)
                {
                    dc.Pop();
                }
            }

            // The custom route being clicked (T13.7b): in the line's colour, under the active route.
            var draftPen = Frozen(new Pen(line, DraftThickness));
            foreach (var (from, to) in graph.DraftLegs.Where(l => layout.Points.ContainsKey(l.From) && layout.Points.ContainsKey(l.To)))
            {
                var (a, b) = MapGeometry.Line(At(from), At(to), MapLayout.Radius(kinds[from]), MapLayout.Radius(kinds[to]), false);
                dc.DrawLine(draftPen, P(a), P(b));
                Arrow(dc, a, b, line);
            }

            var routePen = Frozen(new Pen(route, RouteThickness));
            foreach (var (from, to) in graph.Legs.Where(l => layout.Points.ContainsKey(l.From) && layout.Points.ContainsKey(l.To)))
            {
                var (a, b) = MapGeometry.Line(At(from), At(to), MapLayout.Radius(kinds[from]), MapLayout.Radius(kinds[to]), false);
                dc.DrawLine(routePen, P(a), P(b));
                Arrow(dc, a, b, route);
            }

            var ringPen = Frozen(new Pen(line, 1.5));
            var focusPen = Frozen(new Pen(line, 2.5));
            foreach (var node in graph.Nodes.Where(n => layout.Points.ContainsKey(n.Key)))
            {
                var at = P(At(node.Key));
                var r = MapLayout.Radius(node.Kind);
                dc.DrawEllipse(node.Kind == MapNodeKind.Surface ? null : line, node.Kind == MapNodeKind.Surface ? ringPen : null, at, r, r);
                if (node.Key == scene.Hub)
                {
                    dc.DrawEllipse(null, ringPen, at, r + RingGap, r + RingGap);
                }

                if (node.Key == graph.Focus)
                {
                    // The start a click chose: a second, bolder ring in the line's colour (the route's colour is the route's).
                    dc.DrawEllipse(null, focusPen, at, r + (2 * RingGap), r + (2 * RingGap));
                }
            }
        }

        var shown = layout.ShownLabels(Zoom).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, block) in _labels)
        {
            var box = layout.Labels[key].Box(At(key));
            SetLeft(block, box.X);
            SetTop(block, box.Y);
            block.Visibility = shown.Contains(key) ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var (key, block) in _numbers.Where(n => layout.Points.ContainsKey(n.Key)))
        {
            // Above left of the node, clear of its label's usual place outwards.
            var at = At(key);
            var r = MapLayout.Radius(kinds.GetValueOrDefault(key, MapNodeKind.Station)) + RingGap;
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            SetLeft(block, at.X - r - block.DesiredSize.Width);
            SetTop(block, at.Y - r - block.DesiredSize.Height);
        }
    }

    // The arrow's head at b, pointing from a; only at the most zoom (gate finding 7b, Martin: below it, too much). The
    // way back is the line beside (two-way offset), the route's order its numbers.
    private void Arrow(DrawingContext dc, MapPoint a, MapPoint b, Brush brush)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (Zoom < MaxZoom || length < ArrowLength)
        {
            return;
        }

        var (ux, uy) = (dx / length, dy / length);
        var (bx, by) = (b.X - (ux * ArrowLength), b.Y - (uy * ArrowLength));
        var head = new StreamGeometry();
        using (var g = head.Open())
        {
            g.BeginFigure(P(b), true, true);
            g.LineTo(new Point(bx - (uy * ArrowHalfWidth), by + (ux * ArrowHalfWidth)), false, false);
            g.LineTo(new Point(bx + (uy * ArrowHalfWidth), by - (ux * ArrowHalfWidth)), false, false);
        }

        head.Freeze();
        dc.DrawGeometry(brush, null, head);
    }

    // A node under the pointer (its disc or its shown label), else the nearest edge within MapGeometry.HitPx.
    private object? Hit(Point point)
    {
        if (Scene is not { } scene || _layout is not { } layout)
        {
            return null;
        }

        var p = new MapPoint(point.X, point.Y);
        var shown = layout.ShownLabels(Zoom).ToHashSet(StringComparer.Ordinal);
        foreach (var node in scene.Graph.Nodes.Where(n => layout.Points.ContainsKey(n.Key)))
        {
            var at = At(node.Key);
            var onDisc = Math.Sqrt(Math.Pow(p.X - at.X, 2) + Math.Pow(p.Y - at.Y, 2)) <= MapLayout.Radius(node.Kind) + NodeHitPx;
            var box = layout.Labels[node.Key].Box(at);
            var onLabel = shown.Contains(node.Key) && p.X >= box.X && p.X <= box.Right && p.Y >= box.Y && p.Y <= box.Bottom;
            if (onDisc || onLabel)
            {
                return node;
            }
        }

        return MapGeometry.Nearest(p, [.. _lines.Select(l => (l.A, l.B))], MapGeometry.HitPx) is { } index ? _lines[index].Edge : null;
    }

    private void Hover(Point at)
    {
        var hit = Hit(at);
        if (Equals(hit, _hovered))
        {
            return;
        }

        CloseTip();
        _hovered = hit;
        if (hit is not null)
        {
            _tipDelay.Start();
        }
    }

    private void OpenTip()
    {
        _tipDelay.Stop();
        if (_hovered is null || !IsMouseOver)
        {
            return;
        }

        var at = Mouse.GetPosition(this);
        _tip.Content = ToolTipAt(at);
        (_tip.HorizontalOffset, _tip.VerticalOffset) = (at.X + 16, at.Y + 16);
        _tip.IsOpen = _tip.Content is not null;
    }

    private void CloseTip()
    {
        _tipDelay.Stop();
        _tip.IsOpen = false;
        _hovered = null;
    }

    private static Point P(MapPoint p) => new(p.X, p.Y);

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    /// <summary>The one drawing of the map, as a child of the canvas; it takes no pointer, the canvas does.</summary>
    private sealed class DrawingHost : FrameworkElement
    {
        public DrawingHost()
        {
            IsHitTestVisible = false;
            AddVisualChild(Visual);
        }

        public DrawingVisual Visual { get; } = new();

        protected override int VisualChildrenCount => 1;

        public void Clear() => Visual.RenderOpen().Close();

        protected override Visual GetVisualChild(int index) => Visual;
    }
}
