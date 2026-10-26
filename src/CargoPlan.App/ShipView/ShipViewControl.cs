using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CargoPlan.App.Settings;
using CargoPlan.App.Win32;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Paints a <see cref="ShipScene"/> as <see cref="SceneProjector"/> projects it for the current camera: frame, rack
/// edges, the boxes of each rack back to front, red and cyan edges, the ghost of a dragged crate, rack numbers on top.
/// Only redraws when the scene, the size, the camera, zoom, pan or a drag's target change; no timer, no animation (D14).
/// Mouse (rules in <see cref="DragGesture"/>): left drag turns and tilts the camera, a click on cargo ticks its stop off
/// or back (D16), right click on a crate selects it or takes it out and on free space clears the selection, right drag
/// on a crate puts it (and the selection it belongs to) on the cell under the cursor (D25), right drag elsewhere pans, wheel
/// zooms around the cursor, double-click on free or empty space resets, hover shows the crate. A line or header of the
/// legend starts its drag here too (<see cref="StartLegendDrag"/>). Left clicks are handled here, so the window's DragMove only
/// works outside the view (D15). While the mouse is captured, a poll of the cursor drives the drag once per frame: with
/// Star Citizen in front no mouse moves arrive (D45).
/// </summary>
public sealed class ShipViewControl : FrameworkElement
{
    private const double FitMargin = 8;
    private const double WheelZoomStep = 1.2;
    private const double OrbitDegreesPerPixel = 0.5;

    // The dragged cargo stays in place, faded, so the rack shows what it holds without it (D20, D25).
    private const double DraggedOpacity = 0.35;

    private const double CrateEdgeWidth = 0.5;

    // A long ship's empty racks stay visible behind the loaded ones the fit shows, but don't compete with them (D61).
    // Crates of other missions while a crate is selected take the same value (T7.4d, D68).
    private const double EmptyRackOpacity = 0.4;

    // On a long ship the rack number stands this many pixels above its rack (D61).
    private const double LabelGap = 4;

    private static readonly Pen FramePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Frame), 1) { DashStyle = DashStyles.Dash });
    private static readonly Pen FreeCellPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.FreeEdge), CrateEdgeWidth));
    private static readonly Pen RackPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.RackEdge), 1));

    // The bow wedge (D52): faces with a trace of fill and rack edges, dashed lines alone were hard to see in the game.
    private static readonly Brush BowWedgeFill = Frozen(new SolidColorBrush(MissionPalette.BowWedgeFill));

    // The ship outline (D62): a backdrop, so a thin line in the rack numbers' colour, toned down below the racks' edges.
    private const double OutlineWidth = 1;
    private const double OutlineOpacity = 0.6;

    // Its label lies on its floor; built at this size, then scaled to the floor, so only the proportions count.
    private const string BowLabel = "Bow";
    private const double BowLabelEmSize = 100;
    private static readonly Pen ViolationPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Violation), MissionPalette.EdgeWidth));
    private static readonly Pen DragTargetPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.DragTarget), MissionPalette.EdgeWidth));
    // Under those two on crates and ghosts (K4.2, P6): round joins, so the wider dark line has no spikes at sharp corners.
    // The selection's edge and contour are settings since K4.4 (SelectionPen, SelectionOutlinePen).
    private static readonly Pen EdgeUnderPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.EdgeUnderStroke), MissionPalette.EdgeUnderStrokeWidth)
    {
        LineJoin = PenLineJoin.Round,
    });
    private static readonly Pen MissionFramePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.MissionFrame), 1.5));

    // Theme values (D31), set in OverlayWindow.xaml by DynamicResource: a new value redraws at once (live preview, T4.2).
    // The defaults draw opaque crates without edges or numbers, so a missing resource shows instead of crashing.

    /// <summary>Alpha of the crate fills; their colours are the stop colours of <see cref="StopPalette"/> (D29, K4.2).</summary>
    public static readonly DependencyProperty CrateFillAlphaProperty = ThemeProperty(nameof(CrateFillAlpha), typeof(byte), byte.MaxValue);

    /// <summary>
    /// The stop colours (K4.2), bound to the view model, not built into the scene: a new palette repaints at once and the
    /// scene with its selection stays (D25).
    /// </summary>
    public static readonly DependencyProperty StopPaletteProperty = ThemeProperty(nameof(StopPalette), typeof(StopPalette), StopPalettes.OkabeIto);

    public static readonly DependencyProperty CrateEdgeBrushProperty = ThemeProperty(nameof(CrateEdgeBrush), typeof(Brush), null);

    public static readonly DependencyProperty RackNumberBrushProperty = ThemeProperty(nameof(RackNumberBrush), typeof(Brush), null);

    /// <summary>Pixels at every zoom; see <see cref="RackNumberSize"/> (D32).</summary>
    public static readonly DependencyProperty RackNumberFontSizeProperty = ThemeProperty(nameof(RackNumberFontSize), typeof(double), RackNumberSize.MinSize);

    /// <summary>The outline of the rack numbers (D15), the crates' halo (D28); null = none.</summary>
    public static readonly DependencyProperty RackNumberHaloBrushProperty = ThemeProperty(nameof(RackNumberHaloBrush), typeof(Brush), null);

    public static readonly DependencyProperty RackNumberHaloSizeProperty = ThemeProperty(nameof(RackNumberHaloSize), typeof(double), 0.0);

    /// <summary>The selected crates' edge (D25, K4.4); null = selected crates show no edge.</summary>
    public static readonly DependencyProperty SelectionPenProperty = ThemeProperty(nameof(SelectionPen), typeof(Pen), null);

    /// <summary>The dark contour under that edge (K4.2, K4.4); null = none.</summary>
    public static readonly DependencyProperty SelectionOutlinePenProperty = ThemeProperty(nameof(SelectionOutlinePen), typeof(Pen), null);

    // AffectsRender: a new scene invalidates the drawing by itself.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkpropertymetadataoptions
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(ShipScene),
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((ShipViewControl)d).OnSceneChanged((ShipScene?)e.OldValue)));

    /// <summary>Runs with the clicked cargo's destination as parameter.</summary>
    public static readonly DependencyProperty ToggleStopCommandProperty = DependencyProperty.Register(
        nameof(ToggleStopCommand),
        typeof(ICommand),
        typeof(ShipViewControl));

    /// <summary>Runs with a <see cref="Gesture.MoveCrates"/> when a crate is dropped onto a cell, also where Core refuses it.</summary>
    public static readonly DependencyProperty MoveCratesCommandProperty = DependencyProperty.Register(
        nameof(MoveCratesCommand),
        typeof(ICommand),
        typeof(ShipViewControl));

    /// <summary>Runs with a <see cref="Gesture.MoveLegend"/> when a legend line or a mission's header is dropped onto a rack.</summary>
    public static readonly DependencyProperty MoveLegendCommandProperty = DependencyProperty.Register(
        nameof(MoveLegendCommand),
        typeof(ICommand),
        typeof(ShipViewControl));

    /// <summary>
    /// The crates picked by right click (D25), two-way: the view picks, the view model selects what split and merge
    /// made (D27). Bound to the scene it was made in, so a new plan empties it by itself.
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkpropertymetadataoptions
    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection),
        typeof(CrateSelection),
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(
            CrateSelection.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    // Read-only: only this control knows camera, zoom and pan; the hologram backdrop binds to it (D63).
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/properties/read-only-dependency-properties
    private static readonly DependencyPropertyKey ViewPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(View),
        typeof(ShipViewState),
        typeof(ShipViewControl),
        new PropertyMetadata(null));

    /// <summary>
    /// What the view shows right now: camera, transform, zoom, size. Set whenever one of them changes, before the view
    /// redraws, so the backdrop's 3D picture changes in the same frame as the racks (D63).
    /// </summary>
    public static readonly DependencyProperty ViewProperty = ViewPropertyKey.DependencyProperty;

    /// <summary>
    /// The view as the player left it on the scene's ship (K1.2), two-way like <see cref="Selection"/>: the view writes it on
    /// every turn, zoom and pan, null on the reset; the view model brings another ship's with the ship. null = the ship's
    /// default camera, fitted (D61).
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkpropertymetadataoptions
    public static readonly DependencyProperty CameraProperty = DependencyProperty.Register(
        nameof(Camera),
        typeof(ShipCamera),
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShipViewControl)d).OnCameraChanged()));

    /// <summary>How far the wheel zooms in, pixels per cell (D76); a lower value shrinks a framed view at once.</summary>
    public static readonly DependencyProperty MaxPxPerCellProperty = DependencyProperty.Register(
        nameof(MaxPxPerCell),
        typeof(double),
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(AppearanceRules.DefaultMaxZoom, (d, _) => ((ShipViewControl)d).OnMaxPxPerCellChanged()));

    // Opened by hand instead of ToolTipService: the text changes with the crate under the cursor.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/controls/how-to-position-a-tooltip
    private readonly ToolTip _toolTip;

    private ViewNavigation _navigation = ViewNavigation.Default;
    private Drag? _drag;

    // The frame the poll last ran in: Rendering can come more than once per frame (D45).
    private TimeSpan _lastPollTime;

    // The drag's button read as down at the start: only then does "not down" mean released rather than a failed call (D45).
    private bool _buttonArmed;

    // Wheel delta of the running drag not yet a full notch: finer wheels send less than 120 per event.
    private int _wheelRest;

    // Brushes and pens from the theme properties; null after a change until the next render.
    private Paint? _paint;

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

    public ICommand? MoveCratesCommand
    {
        get => (ICommand?)GetValue(MoveCratesCommandProperty);
        set => SetValue(MoveCratesCommandProperty, value);
    }

    public ICommand? MoveLegendCommand
    {
        get => (ICommand?)GetValue(MoveLegendCommandProperty);
        set => SetValue(MoveLegendCommandProperty, value);
    }

    public CrateSelection Selection
    {
        get => (CrateSelection)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public ShipViewState? View => (ShipViewState?)GetValue(ViewProperty);

    public ShipCamera? Camera
    {
        get => (ShipCamera?)GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public double MaxPxPerCell
    {
        get => (double)GetValue(MaxPxPerCellProperty);
        set => SetValue(MaxPxPerCellProperty, value);
    }

    private ZoomLimits Limits => new(MaxPxPerCell);

    public byte CrateFillAlpha
    {
        get => (byte)GetValue(CrateFillAlphaProperty);
        set => SetValue(CrateFillAlphaProperty, value);
    }

    public StopPalette StopPalette
    {
        get => (StopPalette)GetValue(StopPaletteProperty);
        set => SetValue(StopPaletteProperty, value);
    }

    public Brush? CrateEdgeBrush
    {
        get => (Brush?)GetValue(CrateEdgeBrushProperty);
        set => SetValue(CrateEdgeBrushProperty, value);
    }

    public Brush? RackNumberBrush
    {
        get => (Brush?)GetValue(RackNumberBrushProperty);
        set => SetValue(RackNumberBrushProperty, value);
    }

    public double RackNumberFontSize
    {
        get => (double)GetValue(RackNumberFontSizeProperty);
        set => SetValue(RackNumberFontSizeProperty, value);
    }

    public Brush? RackNumberHaloBrush
    {
        get => (Brush?)GetValue(RackNumberHaloBrushProperty);
        set => SetValue(RackNumberHaloBrushProperty, value);
    }

    public double RackNumberHaloSize
    {
        get => (double)GetValue(RackNumberHaloSizeProperty);
        set => SetValue(RackNumberHaloSizeProperty, value);
    }

    public Pen? SelectionPen
    {
        get => (Pen?)GetValue(SelectionPenProperty);
        set => SetValue(SelectionPenProperty, value);
    }

    public Pen? SelectionOutlinePen
    {
        get => (Pen?)GetValue(SelectionOutlinePenProperty);
        set => SetValue(SelectionOutlinePenProperty, value);
    }

    /// <summary>
    /// False while the overlay is locked: wheel, drags and hover do nothing. Click-through alone isn't enough, the
    /// wheel can go to the focused window instead of the one under the cursor (D15). The selection stays: at the
    /// elevator the lock is the normal state and the selection what is needed there (K6.2, D25 addendum).
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

                // A running drag ends without a move: releasing the capture clears it (OnLostMouseCapture). A drag that
                // never got the capture gets no LostMouseCapture, so it ends here, or a later button up would run it.
                ReleaseMouseCapture();
                if (_drag is not null)
                {
                    _drag = null;
                    InvalidateVisual();
                }
            }
        }
    } = true;

    /// <summary>A drag (move, selection or legend line) runs; the lock ends it without a move (K6.2).</summary>
    public bool IsDragging => _drag is not null;

    /// <summary>
    /// Starts the right drag of a legend line or a mission's header (D25, B6): the window's legend calls it on a right
    /// press with the crates on board it holds, the drag runs here with the mouse captured, and a release over a rack runs
    /// <see cref="MoveLegendCommand"/>. Ignored without crates, while locked or while another drag runs.
    /// </summary>
    public void StartLegendDrag(IReadOnlyList<Crate> crates, MouseButtonEventArgs e)
    {
        if (!IsNavigationEnabled || _drag is not null || Scene is null || crates.Count == 0)
        {
            return;
        }

        e.Handled = true;
        ShowToolTip(null);
        _drag = new Drag(MouseButton.Right, null, crates, IsLegend: true, e.GetPosition(this), IsMoving: false, Gesture: null, Key: null, Outcome: null);
        CaptureForDrag(MouseButton.Right);
    }

    protected override void OnRender(DrawingContext dc)
    {
        // An almost transparent background: a layered window passes clicks on fully transparent pixels through.
        dc.DrawRectangle((Brush)FindResource("HitTestBackgroundBrush"), null, new Rect(RenderSize));
        if (Projected() is not { } scene || Scene is not { } source)
        {
            return;
        }

        var transform = CurrentTransform(scene);
        var paint = _paint ??= Paint.From(StopPalette, CrateFillAlpha, CrateEdgeBrush, RackNumberHaloBrush, RackNumberHaloSize, RackNumberBrush);

        // Screen pixels like every pen here: points are transformed before drawing, nothing scales the drawing itself.
        // One size for the rack numbers and the bow label.
        var numberSize = RackNumberSize.For(RackNumberFontSize, transform.Scale, source.Racks);

        // The outline is a backdrop (D62): under frame, racks and grid from every side, so it never needs a draw order.
        if (scene.Outline is { } outline)
        {
            var geometry = Polygon(outline, transform);
            dc.DrawGeometry(BowWedgeFill, paint.OutlineHalo, geometry);
            dc.DrawGeometry(null, paint.OutlineLine, geometry);
        }

        foreach (var line in scene.Frame)
        {
            dc.DrawLine(FramePen, transform.Apply(line.From), transform.Apply(line.To));
        }

        // Only once the drag has left the drag rectangle: a right click alone changes nothing on screen.
        var drag = _drag is { IsMoving: true } moving ? moving : (Drag?)null;

        // The target rack's edge: always for a legend drag, for crates only while the preview leaves the layout to the drop (D65).
        var target = drag?.Gesture switch
        {
            Gesture.MoveLegend legend => legend.TargetRackId,
            Gesture.MoveCrates crates when drag.Value.Outcome?.Stage == MoveStage.Pending => crates.RackId,
            _ => null,
        };

        // Every crate that changes place fades where it is; its ghost shows where it goes (D65).
        var changed = drag?.Outcome is { } preview ? CrateDrop.Changed(source.Plan, preview) : [];
        HashSet<Crate> fading = drag is { } held ? [.. held.Dragged, .. changed.Select(c => c.Before)] : [];

        // A selection frames its missions and dims every other crate (T7.4d, D68).
        var selected = Selection.In(source);
        var focus = SelectionFocus.Missions(selected);

        // The bow wedge lies beyond every rack: behind all of them seen from the stern, in front seen from the bow (D52).
        if (scene.BowWedge is { InFront: false } behind)
        {
            DrawBowWedge(dc, behind, transform, paint, numberSize);
        }

        // Plain rack edges first: cargo in front covers them like any other part of the ship (D21).
        var isLong = IsoProjection.IsLong(source.Bounds);
        foreach (var rack in scene.Racks.Where(r => !IsHighlighted(r, target)))
        {
            Dimmed(dc, isLong && !rack.IsLoaded, () => DrawEdges(dc, rack, RackPen, transform));
        }

        // Rack by rack back to front, each rack's boxes back to front: the boxes in front cover the ones behind (D21).
        // Free cells only as far as the screen size allows (D61).
        foreach (var rack in scene.Racks)
        {
            Dimmed(dc, isLong && !rack.IsLoaded, () =>
            {
                foreach (var box in rack.Boxes.Where(b => b.Box.Fill != CellFill.Free || FreeCellGrid.Draws(b.Box, transform.Scale)))
                {
                    var fade = box.Box.Crate is { } crate && fading.Contains(crate);
                    if (fade)
                    {
                        dc.PushOpacity(DraggedOpacity);
                    }

                    var (fill, pen) = box.Box.Fill == CellFill.Free ? (null, FreeCellPen) : (paint.Fill(box.Box), paint.CrateEdge);
                    Dimmed(dc, SelectionFocus.Dims(box.Box.Crate, focus), () =>
                    {
                        foreach (var face in box.Faces)
                        {
                            dc.DrawGeometry(fill, pen, Polygon(face.Polygon, transform));
                        }
                    });

                    if (fade)
                    {
                        dc.Pop();
                    }
                }
            });
        }

        if (scene.BowWedge is { InFront: true } inFront)
        {
            DrawBowWedge(dc, inFront, transform, paint, numberSize);
        }

        // Red and cyan on top, like the numbers: a violation or the drop target must never hide behind cargo. A faulty
        // crate (overlap, nothing below it) gets its own red outline, so the player sees which one (D24).
        foreach (var box in scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Box.IsViolated))
        {
            foreach (var face in box.Faces)
            {
                DrawEdged(dc, null, ViolationPen, Polygon(face.Polygon, transform));
            }
        }

        foreach (var rack in scene.Racks.Where(r => IsHighlighted(r, target)))
        {
            DrawEdges(dc, rack, rack.RackId == target ? DragTargetPen : ViolationPen, transform);
        }

        // The selected missions' crates, then the selected crates on top: edges only, the fill stays the stop colour (D25).
        foreach (var box in scene.Racks.SelectMany(r => r.Boxes).Where(b => SelectionFocus.Frames(b.Box.Crate, focus)))
        {
            foreach (var face in box.Faces)
            {
                dc.DrawGeometry(null, MissionFramePen, Polygon(face.Polygon, transform));
            }
        }

        if (SelectionPen is { } selectionPen)
        {
            var outlinePen = SelectionOutlinePen;
            foreach (var box in scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Box.Crate is { } crate && selected.Contains(crate)))
            {
                foreach (var face in box.Faces)
                {
                    var geometry = Polygon(face.Polygon, transform);
                    if (outlinePen is not null)
                    {
                        dc.DrawGeometry(null, outlinePen, geometry);
                    }

                    dc.DrawGeometry(null, selectionPen, geometry);
                }
            }
        }

        // The ghosts: cyan where Core puts every crate that changes place, dragged or moved to make room; red at the cells
        // aimed at where Core refuses (D25, D65). A refused selection reaching past every rack shows the crate held only.
        foreach (var after in changed.Select(c => c.After))
        {
            DrawGhost(dc, source.Ship, after, MissionPalette.DropBrush, DragTargetPen, transform);
        }

        if (drag?.Gesture is Gesture.MoveCrates move && drag.Value.Outcome?.Stage == MoveStage.Refused)
        {
            foreach (var crateMove in CrateDrop.Moves(source.Ship, move) ?? [new CrateMove(move.Anchor, move.RackId, move.Position)])
            {
                DrawGhost(dc, source.Ship, crateMove.Crate with { RackId = crateMove.RackId, Position = crateMove.Position, Rotated = crateMove.Rotated }, MissionPalette.RefusedDropBrush, ViolationPen, transform);
            }
        }

        // Numbers last: always visible, even where a rack in front covers the labelled face.
        if (RackNumberBrush is not { } labelBrush)
        {
            return;
        }

        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var rack in scene.Racks)
        {
            FormattedText text = new(
                rack.Number.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                numberSize,
                labelBrush,
                pixelsPerDip);
            // Above its rack the number stands on its baseline (D61); on its face it is centred.
            var centre = transform.Apply(rack.LabelCentre);
            var top = rack.LabelAbove ? centre.Y - LabelGap - text.Baseline : centre.Y - text.Height / 2;
            DrawLabel(dc, text.BuildGeometry(new Point(centre.X - text.Width / 2, top)), labelBrush, paint);
        }
    }

    private void DrawGhost(DrawingContext dc, Ship ship, Crate crate, Brush fill, Pen pen, ViewTransform transform)
    {
        var rack = ship.Racks.Single(r => r.Id == crate.RackId);
        foreach (var face in SceneProjector.ProjectBox(rack.CellBox(crate), _navigation.Camera))
        {
            DrawEdged(dc, fill, pen, Polygon(face.Polygon, transform));
        }
    }

    /// <summary>A red, cyan or magenta edge over its dark under-stroke (K4.2, P6), so it reads on every stop colour.</summary>
    private static void DrawEdged(DrawingContext dc, Brush? fill, Pen pen, Geometry geometry)
    {
        dc.DrawGeometry(fill, EdgeUnderPen, geometry);
        dc.DrawGeometry(null, pen, geometry);
    }

    private static void Dimmed(DrawingContext dc, bool dimmed, Action draw)
    {
        if (dimmed)
        {
            dc.PushOpacity(EmptyRackOpacity);
        }

        draw();
        if (dimmed)
        {
            dc.Pop();
        }
    }

    // Outline first, fill on top: the outline only shows outside the glyphs.
    private static void DrawLabel(DrawingContext dc, Geometry glyphs, Brush brush, Paint paint)
    {
        if (paint.LabelOutline is { } outline)
        {
            dc.DrawGeometry(null, outline, glyphs);
        }

        dc.DrawGeometry(brush, null, glyphs);
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
            SetCurrentValue(CameraProperty, null);
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
        // Every end of a drag comes here (release, lock, new scene, another window): the one place the poll stops.
        CompositionTarget.Rendering -= OnRendering;

        // Faded cargo, a highlighted target or a ghost and its reason must not stay on screen.
        if (_drag is { IsMoving: true } drag && drag.Dragged.Count > 0)
        {
            InvalidateVisual();
            ShowToolTip(null);
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
        if (_drag is not null)
        {
            UpdateGesture(position);
            return;
        }

        ShowToolTip(SceneHitTest.ToolTipAt(scene, CurrentTransform(scene), RenderSize, position));
    }

    /// <summary>
    /// Moves the running drag to <paramref name="positionInControl"/>; returns at once where it hasn't moved. Called by
    /// MouseMove and, while the mouse is captured, once per frame from the cursor (<see cref="OnRendering"/>, D45).
    /// </summary>
    private void UpdateGesture(Point positionInControl)
    {
        // From is the press point until the drag moves, then the last position: every gesture below sets it.
        if (!IsNavigationEnabled || Projected() is null || _drag is not { } drag || positionInControl == drag.From)
        {
            return;
        }

        // Inside the drag rectangle it may still be a click: the ship stands still until the drag leaves it.
        if (!drag.IsMoving && IsWithinDragRectangle(drag.From, positionInControl))
        {
            return;
        }

        var delta = positionInControl - drag.From;
        var gesture = Resolve(drag, positionInControl, moved: true);

        // Core resolves the drop only when what it depends on changes (E2), not on every mouse move.
        var key = PreviewKey(gesture);
        var outcome = Equals(key, drag.Key) ? drag.Outcome : Preview(gesture);
        _drag = drag with { From = positionInControl, IsMoving = true, Gesture = gesture, Key = key, Outcome = outcome };

        switch (gesture)
        {
            // Like grabbing the ship: dragging right turns its near side right, dragging down tilts its top towards you.
            // The scale holds while turning (K5.5): the fit becomes the framing at the first step.
            case Gesture.Orbit:
                Navigate(_navigation.OrbitBy(-delta.X * OrbitDegreesPerPixel, delta.Y * OrbitDegreesPerPixel, Projected() is { } turned ? Basis(turned) : null));
                break;
            case Gesture.Pan when Projected() is { } scene:
                Navigate(_navigation.PanBy(delta, Basis(scene), Limits));
                break;

            // Moving cargo: the ship stands still; redraw only when the fade starts or the target or its reason change.
            case Gesture.MoveCrates or Gesture.MoveLegend or Gesture.None
                when !drag.IsMoving || !Equals(TargetOf(gesture), TargetOf(drag.Gesture)) || outcome != drag.Outcome:
                InvalidateVisual();
                ShowToolTip(outcome is { } shown && TargetRack(gesture) is { } rack ? CrateDrop.Text(shown, rack) : null);
                break;
        }
    }

    /// <summary>
    /// Turns the crate <paramref name="drag"/> holds and shows the preview at once, where the cursor is: the wheel needs no
    /// mouse move. A turn counts as moving, so releasing on the crate's own cell turns it there.
    /// </summary>
    private void Turn(Drag drag)
    {
        var turned = drag with { Turned = !drag.Turned, IsMoving = true };
        var gesture = Resolve(turned, drag.From, moved: true);
        var key = PreviewKey(gesture);
        var outcome = Equals(key, drag.Key) ? drag.Outcome : Preview(gesture);
        _drag = turned with { Gesture = gesture, Key = key, Outcome = outcome };
        InvalidateVisual();
        ShowToolTip(outcome is { } shown && TargetRack(gesture) is { } rack ? CrateDrop.Text(shown, rack) : null);
    }

    /// <summary>
    /// What the preview of <paramref name="gesture"/> depends on: the crate drag's key, the legend drag's crates and rack
    /// (the drag's own list, so equal while it runs); null for anything else.
    /// </summary>
    private object? PreviewKey(Gesture gesture) => gesture switch
    {
        Gesture.MoveCrates move when Scene is { } scene => CrateDrop.KeyOf(scene.Ship, move),
        Gesture.MoveLegend move => move,
        _ => null,
    };

    /// <summary>Core's outcome of dropping here, as far as the preview resolves (<see cref="CrateDrop.PreviewStage"/>); null for no move.</summary>
    private MoveOutcome? Preview(Gesture gesture) => (gesture, Scene) switch
    {
        (Gesture.MoveCrates move, { } scene) => CrateDrop.Outcome(scene.Plan, scene.Ship, move, CrateDrop.PreviewStage),
        (Gesture.MoveLegend move, { } scene) => CrateDrop.LegendOutcome(scene.Plan, scene.Ship, move, CrateDrop.PreviewStage),
        _ => null,
    };

    private Rack? TargetRack(Gesture gesture) =>
        Scene is { } scene && gesture switch { Gesture.MoveCrates move => move.RackId, Gesture.MoveLegend move => move.TargetRackId, _ => null } is { } rackId
            ? scene.Ship.Racks.Single(r => r.Id == rackId)
            : null;

    /// <summary>
    /// The poll while the mouse is captured: the cursor drives the drag, and a button released without an up ends it like
    /// the up (D45). Not Mouse.GetPosition or Mouse.LeftButton: WPF answers both from the last mouse message it got.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.compositiontarget.rendering
    /// </summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        // Rendering also comes when the visual tree forces an extra update; the frames differ in RenderingTime.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.renderingeventargs.renderingtime
        if (e is not RenderingEventArgs args || args.RenderingTime == _lastPollTime || _drag is not { } drag)
        {
            return;
        }

        _lastPollTime = args.RenderingTime;
        if (NativeMethods.GetCursorPosition() is not { } screen)
        {
            return;
        }

        var position = PointFromScreen(screen);
        if (_buttonArmed && !NativeMethods.IsMouseButtonDown(drag.Button))
        {
            FinishDrag(drag, position);
        }
        else
        {
            UpdateGesture(position);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        // A drag keeps its reason on screen while the cursor leaves the view, e.g. coming from the legend.
        if (_drag is null)
        {
            ShowToolTip(null);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (!IsNavigationEnabled)
        {
            return;
        }

        // A crate dragged alone: each notch turns it 90° (D71) instead of zooming. With Star Citizen in front the wheel
        // still arrives while the mouse is captured (spike K2: 38 of 38 notches), unlike mouse moves (D45).
        if (_drag is { } drag && DragGesture.CanTurn(drag.Dragged, drag.IsLegend))
        {
            _wheelRest += e.Delta;
            var notches = _wheelRest / Mouse.MouseWheelDeltaForOneLine;
            _wheelRest -= notches * Mouse.MouseWheelDeltaForOneLine;
            if (notches % 2 != 0)
            {
                Turn(drag);
            }

            return;
        }

        // One notch (120) = one step; finer wheels send smaller deltas. A factor per notch: the same feel at 10 and at
        // 100 pixels per cell (D76).
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.input.mouse.mousewheeldeltaforoneline
        if (Projected() is not { } scene)
        {
            return;
        }

        var factor = Math.Pow(WheelZoomStep, (double)e.Delta / Mouse.MouseWheelDeltaForOneLine);
        ShowToolTip(null);
        Navigate(_navigation.ZoomAt(e.GetPosition(this), factor, Basis(scene), Limits));
    }

    /// <summary>
    /// Still within the system's drag rectangle around the press (SM_CXDRAG/SM_CYDRAG): a click so far, not a drag.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.minimumhorizontaldragdistance
    /// </summary>
    private static bool IsWithinDragRectangle(Point start, Point end) =>
        Math.Abs(end.X - start.X) <= SystemParameters.MinimumHorizontalDragDistance
        && Math.Abs(end.Y - start.Y) <= SystemParameters.MinimumVerticalDragDistance;

    /// <summary>What a drag aims at, to redraw only when it changes; a new gesture record is made on every move.</summary>
    private static object? TargetOf(Gesture? gesture) => gesture switch
    {
        Gesture.MoveCrates move => (move.RackId, move.Position, move.Turn),
        Gesture.MoveLegend move => move.TargetRackId,
        _ => null,
    };

    // Only within the view (K1.4, D33): drawn with ClipToBounds, so a rack beyond its edge is out of sight.
    private string? DestinationAt(Point position) =>
        Projected() is { } scene ? SceneHitTest.DestinationAt(scene, CurrentTransform(scene), RenderSize, position) : null;

    private SceneHit? HitAt(Point position) =>
        Projected() is { } scene ? SceneHitTest.HitAt(scene, CurrentTransform(scene), RenderSize, position) : null;

    /// <summary>The gesture of <paramref name="drag"/> with the cursor at <paramref name="position"/>.</summary>
    private Gesture Resolve(Drag drag, Point position, bool moved)
    {
        var current = HitAt(position);
        return drag.IsLegend
            ? DragGesture.ResolveLegend(drag.Dragged, moved, current)
            : DragGesture.Resolve(drag.Button, drag.Down, moved, current, drag.Dragged.Count > 0 ? DropAt(position, current, drag.Dragged) : null, Selection.In(Scene), drag.Turned);
    }

    /// <summary>The cell under the cursor for dragged crates (<see cref="DropTarget"/>); they are air, so a crate can go a cell further.</summary>
    private Drop? DropAt(Point position, SceneHit? current, IReadOnlyList<Crate> dragged)
    {
        if (current is null || Scene is not { } scene || Projected() is not { } projected)
        {
            return null;
        }

        var rack = scene.Ship.Racks.Single(r => r.Id == current.RackId);
        Crate[] staying = [.. scene.Plan.CratesForRack(rack.Id).Where(c => !dragged.Contains(c))];
        return DropTarget.Resolve(rack, staying, _navigation.Camera, CurrentTransform(projected).Invert(position));
    }

    private ProjectedScene? Projected() => Scene is { } scene ? _projected ??= SceneProjector.Project(scene, _navigation.Camera) : null;

    private ViewTransform CurrentTransform(ProjectedScene scene) => _navigation.On(Basis(scene), Limits);

    // The fit follows the projected extent, so the ship stays in view while the camera turns; on a long ship it shows the
    // loaded racks and keeps room on top for the numbers standing above them (D61). A framing hangs on the anchor (D76).
    // Zooming out reaches the whole scene, a true-size outline included (K5.2d).
    private ViewBasis Basis(ProjectedScene scene) => ViewBasis.Of(
        scene.FitExtent,
        scene.Anchor,
        RenderSize,
        FitMargin,
        scene.Racks.Any(r => r.LabelAbove) ? RackNumberFontSize + LabelGap + RackNumberHaloSize : 0,
        scene.Extent);

    private void OnMaxPxPerCellChanged()
    {
        if (Projected() is { } scene)
        {
            Navigate(_navigation.Clamped(Basis(scene), Limits));
        }
    }

    private void OnSceneChanged(ShipScene? old)
    {
        _projected = null;
        ShowToolTip(null);

        // Another ship starts from its saved view or its own default camera (D61, K1.2); a tick or a new plan on the same
        // ship keeps the view (D16).
        if (Scene is { } scene && old?.Ship.Id != scene.Ship.Id)
        {
            _navigation = NavigationOf(Camera, scene);
        }

        // A drag holds crates of the old plan; Core would not find them in the new one.
        ReleaseMouseCapture();
        PublishView();
    }

    /// <summary>A player's turn, zoom or pan: shown, and written to <see cref="Camera"/> for the settings (K1.2).</summary>
    private void Navigate(ViewNavigation navigation)
    {
        if (navigation == _navigation)
        {
            return;
        }

        Show(navigation);
        SetCurrentValue(CameraProperty, navigation.ToCamera());
    }

    private void Show(ViewNavigation navigation)
    {
        if (navigation.Camera != _navigation.Camera)
        {
            _projected = null;
        }

        _navigation = navigation;
        PublishView();
        InvalidateVisual();
    }

    // The view's own write comes back here through the binding as the same value; anything else came from outside: the
    // reset, the start, another ship.
    private void OnCameraChanged()
    {
        if (Scene is { } scene && Camera != _navigation.ToCamera())
        {
            Show(NavigationOf(Camera, scene));
        }
    }

    private static ViewNavigation NavigationOf(ShipCamera? camera, ShipScene scene) =>
        camera is null ? ViewNavigation.DefaultFor(scene.Bounds) : ViewNavigation.From(camera);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        PublishView();
    }

    /// <summary>Sets <see cref="View"/> from the current camera, scene and size; nothing before the first layout.</summary>
    private void PublishView()
    {
        if (Projected() is { } scene && RenderSize is { Width: > 0, Height: > 0 })
        {
            var basis = Basis(scene);
            SetValue(ViewPropertyKey, new ShipViewState(_navigation.Camera, _navigation.On(basis, Limits), _navigation.ZoomOn(basis, Limits), RenderSize));
        }
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
        _drag = new Drag(button, down, DragGesture.DraggedCrates(button, down, Selection.In(Scene)), IsLegend: false, position, IsMoving: false, Gesture: null, Key: null, Outcome: null);
        _wheelRest = 0;
        CaptureForDrag(button);
    }

    /// <summary>Captures the mouse for a drag and starts the poll that <see cref="OnLostMouseCapture"/> stops (D45).</summary>
    private void CaptureForDrag(MouseButton button)
    {
        if (!CaptureMouse())
        {
            return;
        }

        _buttonArmed = NativeMethods.IsMouseButtonDown(button);

        // -= first: never two handlers on the static event, which would keep the poll and this control alive.
        CompositionTarget.Rendering -= OnRendering;
        CompositionTarget.Rendering += OnRendering;
    }

    private void EndDrag(MouseButtonEventArgs e, MouseButton button)
    {
        if (_drag is not { } drag || drag.Button != button)
        {
            return;
        }

        e.Handled = true;
        FinishDrag(drag, e.GetPosition(this));
    }

    /// <summary>Ends <paramref name="drag"/> at <paramref name="position"/>: on the button's up, or from the poll where the up didn't come (D45).</summary>
    private void FinishDrag(Drag drag, Point position)
    {
        // Resolve before releasing the capture, which clears the drag; run the command after, so the new plan is drawn
        // without the fade. A refused drop runs too: the view model shows the reason (D24).
        var gesture = Resolve(drag, position, drag.IsMoving);
        ReleaseMouseCapture();
        switch (gesture)
        {
            case Gesture.Click click:
                Run(ToggleStopCommand, click.Destination);
                break;
            case Gesture.MoveCrates move:
                Run(MoveCratesCommand, move);
                break;
            case Gesture.MoveLegend move:
                Run(MoveLegendCommand, move);
                break;
            case Gesture.ToggleSelect toggle when Scene is { } scene:
                Pick(Selection.Toggle(scene, toggle.Crate));
                break;
            case Gesture.ClearSelection:
                Pick(CrateSelection.Empty);
                break;
        }
    }

    /// <summary>
    /// Sets the selection from the view. SetCurrentValue keeps the binding, which carries the value to the view model;
    /// AffectsRender redraws the edges.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.dependencyobject.setcurrentvalue
    /// </summary>
    private void Pick(CrateSelection selection) => SetCurrentValue(SelectionProperty, selection);

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

    // Convex: the faces turned towards the camera never cover each other, so they need no order. The label lies on the
    // floor, so it is drawn with the faces, in their place in the draw order, in the rack numbers' colour and outline.
    private void DrawBowWedge(DrawingContext dc, ProjectedWedge wedge, ViewTransform transform, Paint paint, double numberSize)
    {
        foreach (var face in wedge.Faces)
        {
            dc.DrawGeometry(BowWedgeFill, RackPen, Polygon(face.Polygon, transform));
        }

        if (RackNumberBrush is not { } brush)
        {
            return;
        }

        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        FormattedText text = new(
            BowLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            BowLabelEmSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        // As large on the screen as a rack number (D32, shrinking included), as far as the floor has room.
        var glyphs = FloorLabel.Lay(text.BuildGeometry(new Point(0, 0)), BowLabelEmSize, wedge.Floor, transform, numberSize);
        DrawLabel(dc, glyphs, brush, paint);
    }

    // The rack numbers' size and halo set the room above a long ship's racks, so the fit and with it the view.
    private static DependencyProperty ThemeProperty(string name, Type type, object? defaultValue) => DependencyProperty.Register(
        name,
        type,
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) =>
        {
            var view = (ShipViewControl)d;
            view._paint = null;
            view.PublishView();
        }));

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

    /// <summary>The theme's brushes and pens, built once per change instead of on every render.</summary>
    /// <param name="CrateEdge">null = crates without edges.</param>
    /// <param name="LabelOutline">null = rack numbers without outline.</param>
    /// <param name="OutlineLine">The ship outline's line (D62) in the rack numbers' colour, toned down; null without that colour.</param>
    /// <param name="OutlineHalo">Under that line, as wide as the line plus the halo on both sides (D28); null with the halo off.</param>
    /// <param name="Stops">One brush per ordinal up to where colour and hatch repeat (<see cref="StopPalette.Rounds"/>).</param>
    private sealed record Paint(
        IReadOnlyList<Brush> Stops, SolidColorBrush Delivered, Pen? CrateEdge, Pen? LabelOutline, Pen? OutlineLine, Pen? OutlineHalo)
    {
        public static Paint From(StopPalette palette, byte fillAlpha, Brush? edge, Brush? halo, double haloSize, Brush? line) => new(
            [.. Enumerable.Range(0, palette.Colors.Count * StopPalette.Rounds).Select(i => StopBrushes.Fill(palette, i, fillAlpha))],
            MissionPalette.Fill(MissionPalette.Delivered, fillAlpha),
            edge is null ? null : Frozen(new Pen(edge, CrateEdgeWidth)),
            halo is null ? null : Frozen(new Pen(halo, haloSize) { LineJoin = PenLineJoin.Round }),
            line is null ? null : Frozen(new Pen(Toned(line), OutlineWidth) { LineJoin = PenLineJoin.Round }),
            halo is null ? null : Frozen(new Pen(halo, OutlineWidth + 2 * haloSize) { LineJoin = PenLineJoin.Round }));

        private static Brush Toned(Brush brush)
        {
            var toned = brush.CloneCurrentValue();
            toned.Opacity *= OutlineOpacity;
            return toned;
        }

        /// <summary>Cargo only; free cells have no fill.</summary>
        public Brush Fill(SceneBox box) => box.Fill == CellFill.Delivered ? Delivered : Stops[box.PaletteIndex!.Value % Stops.Count];
    }

    /// <param name="Down">What lay under the cursor when the button went down; with the button it decides the gesture.</param>
    /// <param name="Dragged">
    /// The crates the drag holds, drawn faded: what a right press picked up (<see cref="DragGesture.DraggedCrates"/>),
    /// or a legend line's or header's crates on board; empty for orbit and pan.
    /// </param>
    /// <param name="IsLegend">The drag came from the legend (<see cref="StartLegendDrag"/>), not from the view.</param>
    /// <param name="From">Where the button went down until the drag moves, then the last position.</param>
    /// <param name="IsMoving">Left the drag rectangle; until then, releasing the button is a click.</param>
    /// <param name="Gesture">What the drag means at the last position; drawn as ghost or cyan rack.</param>
    /// <param name="Key">What <see cref="Outcome"/> was resolved for (<see cref="PreviewKey"/>); the same key, the same outcome.</param>
    /// <param name="Outcome">Core's result of dropping at the last position (D65): its ghosts in cyan, a refusal red, its text as tool tip.</param>
    /// <param name="Turned">The wheel turned the crate an odd number of notches (D71); only a crate dragged alone.</param>
    private readonly record struct Drag(
        MouseButton Button,
        SceneHit? Down,
        IReadOnlyList<Crate> Dragged,
        bool IsLegend,
        Point From,
        bool IsMoving,
        Gesture? Gesture,
        object? Key,
        MoveOutcome? Outcome,
        bool Turned = false);
}
