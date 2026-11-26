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
/// click on an edge = <see cref="EdgeCommand"/> (its panel of missions, T13.7c), on free room = <see cref="FreeCommand"/>, a
/// right click on an edge = <see cref="RemoveEdgeCommand"/> (T13.7b). A plan (gate finding 7a/7b): its contracts solid, its extras dashed, its order dotted, its stations in
/// numbered pills; the active route in the status line's colour, the custom route in the line's; a tool tip
/// for the node or edge under the pointer. Locked, the window is click-through and none of this reaches it (D11).
/// </summary>
public sealed class RouteMapView : Canvas
{
    public const double MinZoom = 0.5;

    public const double MaxZoom = 6;

    /// <summary>One notch of the wheel zooms by this factor.</summary>
    public const double ZoomStep = 1.2;

    /// <summary>
    /// Arrow heads show from here on (gate finding 7b, Martin): half the wheel's notches between all out and all in. The
    /// wheel zooms by a factor, so half the notches is the geometric mean, √(0.5 · 6) ≈ 1.73×.
    /// </summary>
    public static readonly double ArrowsFromZoom = Math.Sqrt(MinZoom * MaxZoom);

    /// <summary>A press that moves less than this is a click, more is a pan.</summary>
    private const double DragPx = 4;

    // A node takes the pointer this far beyond its rim.
    private const double NodeHitPx = 4;

    private const double DimmedOpacity = 0.25;

    /// <summary>An arrow head's length along its line (gate finding 7b: a little larger, was 7).</summary>
    public const double ArrowLength = 10;

    private const double ArrowHalfWidth = 4.5;

    // A plan's contracts (gate finding 7a/7b): solid, the active route's in its colour, the draft's in the line's.
    private const double RouteThickness = 3;

    // The active route's extras: dashed, thinner than its contracts, thicker than its order.
    private const double ExtraThickness = 2.5;

    // A plan's order: dots this thick, this far apart, each in a dark halo of HaloPx, so they show on the contract
    // they lie on (Martin's condition 1, e.g. Everus → HUR-L1). At 1.5 px in 5 the halo swallowed the dot (picture at 6×).
    private const double DotThickness = 2;

    private const double DotPeriodPx = 6;

    private const double HaloPx = 1;

    private const double BadgePaddingPx = 3;

    private const double ObservedMarkRadius = 2.5;

    private const double RingGap = 4;

    private const string LabelStyleKey = "LegendTextStyle";

    private const string LineBrushKey = "LegendForegroundBrush";

    private const string RouteBrushKey = "NoticeForegroundBrush";

    private const string HaloBrushKey = "MapHaloBrush";

    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene), typeof(MapScene), typeof(RouteMapView), new PropertyMetadata(null, (d, _) => ((RouteMapView)d).Rebuild()));

    public static readonly DependencyProperty FocusCommandProperty = DependencyProperty.Register(
        nameof(FocusCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty EdgeCommandProperty = DependencyProperty.Register(
        nameof(EdgeCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty FreeCommandProperty = DependencyProperty.Register(
        nameof(FreeCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty RemoveEdgeCommandProperty = DependencyProperty.Register(
        nameof(RemoveEdgeCommand), typeof(ICommand), typeof(RouteMapView));

    public static readonly DependencyProperty PaletteProperty = DependencyProperty.Register(
        nameof(Palette), typeof(StopPalette), typeof(RouteMapView), new PropertyMetadata(StopPalettes.OkabeIto, (d, _) => ((RouteMapView)d).Redraw()));

    private static readonly double[] Thickness = [1, 2, 3.5];

    private static readonly DashStyle Dashed = Frozen(new DashStyle([4, 3], 0));

    // A dash style's lengths are in the pen's thickness: 2 × 2.5 px dash, 2 × 2.5 px gap.
    private static readonly DashStyle ExtraDashes = Frozen(new DashStyle([2, 2], 0));

    private readonly DrawingHost _drawing = new();
    private readonly Dictionary<string, TextBlock> _labels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _badges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _draftBadges = new(StringComparer.Ordinal);
    private readonly ToolTip _tip = new() { Placement = PlacementMode.Relative, StaysOpen = true };
    private readonly DispatcherTimer _tipDelay;

    // The edges and the active route's extras as drawn, for the hit test (MapGeometry.Nearest): a MapEdge or a MapExtraEdge.
    private readonly List<(object Target, MapPoint A, MapPoint B)> _lines = [];

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

    /// <summary>Gets an edge on a click (T13.7c: RoutesViewModel.OpenEdgeCommand, its panel of missions).</summary>
    public ICommand? EdgeCommand
    {
        get => (ICommand?)GetValue(EdgeCommandProperty);
        set => SetValue(EdgeCommandProperty, value);
    }

    /// <summary>A click on free room, no drag (T13.7c: RoutesViewModel.ClosePanelCommand).</summary>
    public ICommand? FreeCommand
    {
        get => (ICommand?)GetValue(FreeCommandProperty);
        set => SetValue(FreeCommandProperty, value);
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

    /// <summary>The active route's numbers by node, orange pills above left of the node (gate finding 7a/7b).</summary>
    internal IReadOnlyDictionary<string, Border> Badges => _badges;

    /// <summary>The draft's numbers by node, white pills above right, clear of the route's (Martin's condition 2).</summary>
    internal IReadOnlyDictionary<string, Border> DraftBadges => _draftBadges;

    /// <summary>How often the drawing was redrawn: a zoom redraws, it builds nothing.</summary>
    internal int Redraws { get; private set; }

    /// <summary>The tool tip's text for what lies under <paramref name="point"/>: a node first, else the nearest edge; null for free room.</summary>
    internal string? ToolTipAt(Point point) => Hit(point) switch
    {
        MapNode node when Scene is { } scene => scene.NodeToolTip(node),
        MapEdge edge when Scene is { } scene => scene.EdgeToolTip(edge),
        MapExtraEdge extra when Scene is { ExtraToolTip: { } tip } => tip(extra),
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
        var clicked = _press is not null && !_panning;
        switch (clicked ? Hit(e.GetPosition(this)) : null)
        {
            case MapNode node when FocusCommand is { } focus && focus.CanExecute(node.Key):
                focus.Execute(node.Key);
                break;
            case MapEdge edge when EdgeCommand is { } open && open.CanExecute(edge):
                // T13.7c: the edge's panel with its missions, whatever is in focus.
                open.Execute(edge);
                break;
            case null when clicked && FreeCommand is { } free && free.CanExecute(null):
                free.Execute(null);
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

        foreach (var (key, number) in scene.Graph.Active.Numbers)
        {
            _badges[key] = Badge(number, RouteBrushKey);
        }

        foreach (var (key, number) in scene.Graph.Draft.Numbers)
        {
            _draftBadges[key] = Badge(number, LineBrushKey);
        }

        Redraw();
    }

    // A station's number in a pill of the plan's colour, the number dark in it (gate finding 7a/7b).
    private Border Badge(string number, string backgroundKey)
    {
        var block = new TextBlock { Text = number, FontWeight = FontWeights.Bold, IsHitTestVisible = false, TextWrapping = TextWrapping.NoWrap };
        block.SetResourceReference(StyleProperty, LabelStyleKey);
        block.SetResourceReference(TextBlock.ForegroundProperty, HaloBrushKey);
        var badge = new Border { Child = block, Padding = new Thickness(BadgePaddingPx, 0, BadgePaddingPx, 0), IsHitTestVisible = false };
        badge.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        Children.Add(badge);
        return badge;
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
        foreach (var element in _labels.Values.Cast<FrameworkElement>().Concat(_badges.Values).Concat(_draftBadges.Values))
        {
            Children.Remove(element);
        }

        _labels.Clear();
        _badges.Clear();
        _draftBadges.Clear();
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
        var halo = TryFindResource(HaloBrushKey) as Brush ?? Brushes.Black;
        var kinds = graph.Nodes.ToDictionary(n => n.Key, n => n.Kind, StringComparer.Ordinal);
        using (var dc = _drawing.Visual.RenderOpen())
        {
            // Dimmed first, then the lit ones over them, then the plans over everything.
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

            // The plans (gate finding 7a/7b): the draft (T13.7b) in the line's colour under the active route in its own; the
            // contracts solid on their edge's line, the extras dashed, then the order dotted over all of them.
            bool Drawn((string From, string To) l) => layout.Points.ContainsKey(l.From) && layout.Points.ContainsKey(l.To);
            (MapPoint A, MapPoint B) Segment((string From, string To) l, bool onEdge) =>
                MapGeometry.Line(At(l.From), At(l.To), MapLayout.Radius(kinds[l.From]), MapLayout.Radius(kinds[l.To]), onEdge && graph.Edge(l.From, l.To) is { TwoWay: true });

            foreach (var (lines, brush) in new[] { (graph.Draft, line), (graph.Active, route) })
            {
                var pen = Frozen(new Pen(brush, RouteThickness));
                foreach (var (a, b) in lines.Edges.Where(Drawn).Select(l => Segment(l, onEdge: true)))
                {
                    dc.DrawLine(pen, P(a), P(b));
                    Arrow(dc, a, b, brush);
                }
            }

            var extraPen = Frozen(new Pen(route, ExtraThickness) { DashStyle = ExtraDashes });
            foreach (var extra in graph.Extras.Where(x => Drawn((x.From, x.To))))
            {
                var (a, b) = Segment((extra.From, extra.To), onEdge: true);
                if (graph.Edge(extra.From, extra.To) is null)
                {
                    // On an edge of the filter its tool tip names the extra; else the extra takes the pointer itself.
                    _lines.Add((extra, a, b));
                }

                dc.DrawLine(extraPen, P(a), P(b));
                Arrow(dc, a, b, route);
            }

            foreach (var (lines, brush) in new[] { (graph.Draft, line), (graph.Active, route) })
            {
                var (haloPen, dotPen) = (Dotted(halo, DotThickness + (2 * HaloPx)), Dotted(brush, DotThickness));
                foreach (var (a, b) in lines.Legs.Where(Drawn).Select(l => Segment(l, onEdge: false)))
                {
                    dc.DrawLine(haloPen, P(a), P(b));
                    dc.DrawLine(dotPen, P(a), P(b));
                }
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

        // The route's pills above left of the node, the draft's above right (Martin's condition 2): clear of each other
        // and of the label's usual place outwards.
        foreach (var (badges, left) in new[] { (_badges, true), (_draftBadges, false) })
        {
            foreach (var (key, badge) in badges.Where(n => layout.Points.ContainsKey(n.Key)))
            {
                var at = At(key);
                var r = MapLayout.Radius(kinds.GetValueOrDefault(key, MapNodeKind.Station)) + RingGap;
                badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                badge.CornerRadius = new CornerRadius(badge.DesiredSize.Height / 2);
                SetLeft(badge, left ? at.X - r - badge.DesiredSize.Width : at.X + r);
                SetTop(badge, at.Y - r - badge.DesiredSize.Height);
            }
        }
    }

    // Round dots DotPeriodPx apart whatever the pen's thickness: a dash style's lengths are in the thickness, so the halo's
    // dots and the line's lie on the same centres. Source: https://learn.microsoft.com/dotnet/api/system.windows.media.dashstyle.dashes
    private static Pen Dotted(Brush brush, double thickness) => Frozen(new Pen(brush, thickness)
    {
        DashStyle = new DashStyle([0, DotPeriodPx / thickness], 0),
        DashCap = PenLineCap.Round,
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
    });

    // The arrow's head in the middle of a → b, pointing to b; from ArrowsFromZoom on (gate finding 7b, Martin: below it,
    // too much). The way back is the line beside (two-way offset), the route's order its numbers. It covers an observed
    // edge's mark at the middle; the solid line still says observed.
    private void Arrow(DrawingContext dc, MapPoint a, MapPoint b, Brush brush)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (Zoom < ArrowsFromZoom || length < ArrowLength)
        {
            return;
        }

        // In the middle of the line (gate finding 7b, Martin): the tip half a head ahead of it, the base half behind.
        var (ux, uy) = (dx / length, dy / length);
        var (mx, my) = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var tip = new Point(mx + (ux * ArrowLength / 2), my + (uy * ArrowLength / 2));
        var (bx, by) = (mx - (ux * ArrowLength / 2), my - (uy * ArrowLength / 2));
        var head = new StreamGeometry();
        using (var g = head.Open())
        {
            g.BeginFigure(tip, true, true);
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

        return MapGeometry.Nearest(p, [.. _lines.Select(l => (l.A, l.B))], MapGeometry.HitPx) is { } index ? _lines[index].Target : null;
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
