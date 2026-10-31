using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.Win32;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Shows a <see cref="ShipScene"/> as one perspective 3D scene (D83): crates, bow wedge and hologram in a
/// <see cref="Viewport3D"/> under one camera, set from <see cref="CameraMath"/>. Only changes when the scene, the size,
/// the camera or the hologram change; no timer, no animation (D14).
/// Mouse (rules in <see cref="DragGesture"/>): left drag turns and tilts the camera, a click on cargo ticks its stop off
/// or back (D16), right click on a crate selects it or takes it out and on free space clears the selection, right drag
/// on a crate puts it (and the selection it belongs to) on the cell under the cursor (D25), right drag elsewhere pans, wheel
/// zooms around the cursor, double-click on free or empty space resets, hover shows the crate. A line or header of the
/// legend starts its drag here too (<see cref="StartLegendDrag"/>). Left clicks are handled here, so the window's DragMove only
/// works outside the view (D15). While the mouse is captured, a poll of the cursor drives the drag once per frame: with
/// Star Citizen in front no mouse moves arrive (D45). What lies under the cursor is a ray from the camera the picture was
/// drawn with against the scene's boxes (<see cref="SceneHitTest"/>, T8.3).
/// </summary>
public sealed class ShipViewControl : FrameworkElement
{
    private const double FitMargin = 8;
    private const double WheelZoomStep = 1.2;
    private const double OrbitDegreesPerPixel = 0.5;

    // Theme values (D31), set in OverlayWindow.xaml by DynamicResource. The 3D crates take them from T8.2 on (stop
    // colours, edges, numbers, selection); until then they are kept, unused, so the bindings and the look stay in place.

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
            CrateSelection.Empty,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((ShipViewControl)d).OnLookChanged()));

    /// <summary>The ship's hologram (D63), part of the scene since D83; null = none, the marker shows instead.</summary>
    public static readonly DependencyProperty HoloProperty = HoloLookProperty(nameof(Holo), typeof(PreparedHolo), null);

    /// <summary>The hologram's opacity (settings.json holoOpacity), on its material since D83.</summary>
    public static readonly DependencyProperty HoloOpacityProperty = HoloLookProperty(nameof(HoloOpacity), typeof(double), AppearanceRules.DefaultHoloOpacity);

    /// <summary>The hull in the shaded and flat looks; bound to the settings' holoHullColor.</summary>
    public static readonly DependencyProperty HoloHullColorProperty = HoloLookProperty(
        nameof(HoloHullColor), typeof(Color), (Color)ColorConverter.ConvertFromString(AppearanceRules.DefaultHoloHullColor));

    /// <summary>The edges in the hidden-line look; bound to the settings' holoEdgeColor.</summary>
    public static readonly DependencyProperty HoloEdgeColorProperty = HoloLookProperty(
        nameof(HoloEdgeColor), typeof(Color), (Color)ColorConverter.ConvertFromString(AppearanceRules.DefaultHoloEdgeColor));

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

    // The scene's only visual child, never hit: the view takes every click and finds what lies under it itself (D83).
    private readonly Viewport3D _viewport = new() { IsHitTestVisible = false };
    private readonly PerspectiveCamera _camera = new();
    private readonly ShipScene3D _scene3D = new();

    // Over the scene: violations, the selection, the rack numbers and the "Bow" label (T8.2, D83).
    private readonly DrawingVisual _overlay = new();

    private CameraNavigation _navigation = CameraNavigation.Default;

    // The camera the picture on screen was drawn with (ApplyCamera): the hit test takes this one, never a second.
    private CameraMath? _shown;

    // What the running drag shows: faded crates in the scene, target and ghosts in the overlay (T8.4).
    private DragPreview _preview = DragPreview.None;

    private Drag? _drag;

    // The frame the poll last ran in: Rendering can come more than once per frame (D45).
    private TimeSpan _lastPollTime;

    // The drag's button read as down at the start: only then does "not down" mean released rather than a failed call (D45).
    private bool _buttonArmed;

    // Wheel delta of the running drag not yet a full notch: finer wheels send less than 120 per event.
    private int _wheelRest;

    public ShipViewControl()
    {
        _toolTip = new ToolTip { PlacementTarget = this, Placement = PlacementMode.Mouse };

        // Zoomed or panned racks must not paint over the header or the legend.
        ClipToBounds = true;

        _viewport.Camera = _camera;
        foreach (var visual in _scene3D.Visuals)
        {
            _viewport.Children.Add(visual);
        }

        AddVisualChild(_viewport);
        AddVisualChild(_overlay);
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

    public PreparedHolo? Holo
    {
        get => (PreparedHolo?)GetValue(HoloProperty);
        set => SetValue(HoloProperty, value);
    }

    public double HoloOpacity
    {
        get => (double)GetValue(HoloOpacityProperty);
        set => SetValue(HoloOpacityProperty, value);
    }

    public Color HoloHullColor
    {
        get => (Color)GetValue(HoloHullColorProperty);
        set => SetValue(HoloHullColorProperty, value);
    }

    public Color HoloEdgeColor
    {
        get => (Color)GetValue(HoloEdgeColorProperty);
        set => SetValue(HoloEdgeColorProperty, value);
    }

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

    private DistanceLimits Limits => new(MaxPxPerCell);

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
                    ShowDrag();
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
    public void StartLegendDrag(IReadOnlyList<Crate> crates, MouseButtonEventArgs e) => e.Handled |= StartLegendDrag(crates, e.GetPosition(this));

    /// <summary>The legend drag from <paramref name="position"/> (DIPs in the view); false if ignored. Internal for the view tests (T8.4, R5).</summary>
    internal bool StartLegendDrag(IReadOnlyList<Crate> crates, Point position)
    {
        if (!IsNavigationEnabled || _drag is not null || Scene is null || crates.Count == 0)
        {
            return false;
        }

        ShowToolTip(null);
        _drag = new Drag(MouseButton.Right, null, crates, IsLegend: true, position, IsMoving: false, Gesture: null, Key: null, Outcome: null);
        CaptureForDrag(MouseButton.Right);
        return true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        // An almost transparent background: a layered window passes clicks on fully transparent pixels through. The scene
        // lies above it in the Viewport3D.
        dc.DrawRectangle((Brush)FindResource("HitTestBackgroundBrush"), null, new Rect(RenderSize));
    }

    // Two visual children (AddVisualChild in the constructor), laid out over the whole element; the layout overrides below
    // measure and arrange the viewport. Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkelement.visualchildrencount
    protected override int VisualChildrenCount => 2;

    // The scene, then the overlay on top of it (T8.2): what must never hide behind cargo, and the text.
    protected override Visual GetVisualChild(int index) => index switch
    {
        0 => _viewport,
        1 => _overlay,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        _viewport.Measure(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewport.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Handled marks the underlying MouseDown handled for the whole route, so the window's DragMove doesn't run.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.uielement.mouseleftbuttondown
        // ClickCount: 2 for the second press of a double-click.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.input.mousebuttoneventargs.clickcount
        e.Handled = true;
        Press(MouseButton.Left, e.GetPosition(this), e.ClickCount);
    }

    // Source: https://learn.microsoft.com/dotnet/api/system.windows.uielement.onmouserightbuttondown
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        Press(MouseButton.Right, e.GetPosition(this), e.ClickCount);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => e.Handled |= Release(MouseButton.Left, e.GetPosition(this));

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) => e.Handled |= Release(MouseButton.Right, e.GetPosition(this));

    /// <summary>
    /// A button pressed at <paramref name="position"/> (DIPs in the view); <paramref name="clicks"/> is 2 on a
    /// double-click's second press. Internal for the view tests: the handlers only read the event (T8.3, R4).
    /// </summary>
    internal void Press(MouseButton button, Point position, int clicks)
    {
        if (!IsNavigationEnabled)
        {
            return;
        }

        // Reset only off the cargo: there the second click of a double-click is a click of its own and ticks back (D16).
        if (button == MouseButton.Left && clicks == 2 && DestinationAt(position) is null)
        {
            SetCurrentValue(CameraProperty, null);
            return;
        }

        StartDrag(position, button);
    }

    /// <summary>A button released at <paramref name="position"/>: ends the drag it started; false if it started none.</summary>
    internal bool Release(MouseButton button, Point position)
    {
        if (_drag is not { } drag || drag.Button != button)
        {
            return false;
        }

        FinishDrag(drag, position);
        return true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        // Every end of a drag comes here (release, lock, new scene, another window): the one place the poll stops.
        CompositionTarget.Rendering -= OnRendering;

        // Faded cargo, a highlighted target or a ghost and its reason must not stay on screen.
        var shown = _drag is { IsMoving: true } drag && drag.Dragged.Count > 0;
        _drag = null;
        if (shown)
        {
            ShowDrag();
            ShowToolTip(null);
        }

        base.OnLostMouseCapture(e);
    }

    protected override void OnMouseMove(MouseEventArgs e) => MoveTo(e.GetPosition(this));

    /// <summary>
    /// The mouse at <paramref name="position"/> (DIPs in the view): moves a running drag, else shows the tool tip there.
    /// Internal for the view tests and the drag measurement (T8.4, R5).
    /// </summary>
    internal void MoveTo(Point position)
    {
        if (!IsNavigationEnabled || Scene is null)
        {
            return;
        }

        if (_drag is not null)
        {
            UpdateGesture(position);
            return;
        }

        ShowToolTip(ToolTipAt(position));
    }

    /// <summary>What releasing the running drag would do now, as its preview shows it; null without a drag that moved.</summary>
    internal Gesture? PendingGesture => _drag?.Gesture;

    /// <summary>What the running drag shows now (faded, target, ghosts); <see cref="DragPreview.None"/> without one.</summary>
    internal DragPreview ShownPreview => _preview;

    /// <summary>
    /// Moves the running drag to <paramref name="positionInControl"/>; returns at once where it hasn't moved. Called by
    /// MouseMove and, while the mouse is captured, once per frame from the cursor (<see cref="OnRendering"/>, D45).
    /// </summary>
    private void UpdateGesture(Point positionInControl)
    {
        // From is the press point until the drag moves, then the last position: every gesture below sets it.
        if (!IsNavigationEnabled || Scene is null || _drag is not { } drag || positionInControl == drag.From)
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
            // The distance holds while turning (K5.5): the fit becomes the placement at the first step.
            case Gesture.Orbit:
                Navigate(_navigation.OrbitBy(-delta.X * OrbitDegreesPerPixel, delta.Y * OrbitDegreesPerPixel, Basis()));
                break;
            case Gesture.Pan when Basis() is { } basis:
                Navigate(_navigation.PanBy(delta, basis, Limits, RenderSize));
                break;

            // Moving cargo: the ship stands still; redraw only when the fade starts or the target or its reason change.
            case Gesture.MoveCrates or Gesture.MoveLegend or Gesture.None
                when !drag.IsMoving || !Equals(TargetOf(gesture), TargetOf(drag.Gesture)) || outcome != drag.Outcome:
                ShowDrag();
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
        ShowDrag();
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
        Wheel(e.Delta, e.GetPosition(this));
    }

    /// <summary>The wheel turned by <paramref name="delta"/> with the cursor at <paramref name="position"/>. Internal for the view tests (T8.4, R5).</summary>
    internal void Wheel(int delta, Point position)
    {
        if (!IsNavigationEnabled)
        {
            return;
        }

        // A crate dragged alone: each notch turns it 90° (D71) instead of zooming. With Star Citizen in front the wheel
        // still arrives while the mouse is captured (spike K2: 38 of 38 notches), unlike mouse moves (D45).
        if (_drag is { } drag && DragGesture.CanTurn(drag.Dragged, drag.IsLegend))
        {
            _wheelRest += delta;
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
        if (Basis() is not { } basis)
        {
            return;
        }

        var factor = Math.Pow(WheelZoomStep, (double)delta / Mouse.MouseWheelDeltaForOneLine);
        ShowToolTip(null);
        Navigate(_navigation.ZoomAt(position, factor, basis, Limits, RenderSize));
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

    private string? DestinationAt(Point position) =>
        Scene is { } scene && _shown is { } camera ? SceneHitTest.DestinationAt(scene, camera, position) : null;

    // Internal for the frame measurement's hit-test sweep (Phase 8) and the view tests.
    internal SceneHit? HitAt(Point position) =>
        Scene is { } scene && _shown is { } camera ? SceneHitTest.HitAt(scene, camera, position) : null;

    internal string? ToolTipAt(Point position) =>
        Scene is { } scene && _shown is { } camera ? SceneHitTest.ToolTipAt(scene, camera, position) : null;

    /// <summary>The gesture of <paramref name="drag"/> with the cursor at <paramref name="position"/>.</summary>
    private Gesture Resolve(Drag drag, Point position, bool moved)
    {
        var current = HitAt(position);
        return drag.IsLegend
            ? DragGesture.ResolveLegend(drag.Dragged, moved, current)
            : DragGesture.Resolve(drag.Button, drag.Down, moved, current, drag.Dragged.Count > 0 ? DropAt(position, current, drag.Dragged) : null, Selection.In(Scene), drag.Turned);
    }

    /// <summary>
    /// The cell on the line of sight for dragged crates (<see cref="DropTarget"/>), in the rack the hit test names (the first
    /// rack on the line, D21): the crates dragged are air there, so a crate can go a cell further.
    /// </summary>
    private Drop? DropAt(Point position, SceneHit? current, IReadOnlyList<Crate> dragged)
    {
        if (current is null || Scene is not { } scene || _shown is not { } camera)
        {
            return null;
        }

        var rack = scene.Ship.Racks.Single(r => r.Id == current.RackId);
        Crate[] staying = [.. scene.Plan.CratesForRack(rack.Id).Where(c => !dragged.Contains(c))];
        return DropTarget.Resolve(rack, staying, camera.Unproject(position));
    }

    // The fit follows the scene, so the ship stays in view while the camera turns; on a long ship it shows the loaded
    // racks (D61). A placement is fixed to the ship (D76, D83). The limits come from the ship at its default
    // orientation, so neither turning nor cargo changes the distance (holo pump bugfix). Null before the first layout.
    private CameraBasis? Basis() => Scene is { } scene && RenderSize is { Width: > 0, Height: > 0 }
        ? CameraBasis.For(scene, _navigation.Orientation, RenderSize, FitMargin, ShipOverlay.RoomOnTop(scene, OverlayLook()))
        : null;

    private void OnMaxPxPerCellChanged()
    {
        if (Basis() is { } basis)
        {
            Navigate(_navigation.Clamped(basis, Limits, RenderSize));
        }
    }

    private void OnSceneChanged(ShipScene? old)
    {
        ShowToolTip(null);

        // Another ship starts from its saved view or its own default camera (D61, K1.2); a tick or a new plan on the same
        // ship keeps the view (D16).
        if (Scene is { } scene && old?.Ship.Id != scene.Ship.Id)
        {
            _navigation = NavigationOf(Camera, scene);
        }

        // A drag holds crates of the old plan; Core would not find them in the new one.
        ReleaseMouseCapture();
        _scene3D.Show(Scene, Look());
        ApplyCamera();
    }

    private void OnHoloChanged()
    {
        _scene3D.ShowHolo(Holo, HoloOpacity, HoloHullColor, HoloEdgeColor);
        ApplyCamera();
    }

    /// <summary>A player's turn, zoom or pan: shown, and written to <see cref="Camera"/> for the settings (K1.2).</summary>
    private void Navigate(CameraNavigation navigation)
    {
        if (navigation == _navigation)
        {
            return;
        }

        Show(navigation);
        SetCurrentValue(CameraProperty, navigation.ToCamera());
    }

    private void Show(CameraNavigation navigation)
    {
        _navigation = navigation;
        ApplyCamera();
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

    private static CameraNavigation NavigationOf(ShipCamera? camera, ShipScene scene) =>
        camera is null ? CameraNavigation.DefaultFor(scene.Bounds) : CameraNavigation.From(camera);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ApplyCamera();
    }

    /// <summary>
    /// The scene's camera from the current navigation, size and scene (D83), and the overlay drawn for it (T8.2);
    /// without a scene or before the first layout the overlay is empty.
    /// </summary>
    private void ApplyCamera()
    {
        _shown = null;
        if (Basis() is { } basis && Scene is not null)
        {
            var camera = _navigation.On(basis, Limits, RenderSize);
            camera.ApplyTo(_camera);
            _shown = camera;
            _scene3D.Follow(camera, _navigation.ZoomOn(basis, Limits, RenderSize));
        }

        DrawOverlay();
    }

    /// <summary>The overlay for the camera on screen; empty without a scene or before the first layout.</summary>
    private void DrawOverlay()
    {
        using var dc = _overlay.RenderOpen();
        if (Scene is { } scene && _shown is { } camera)
        {
            ShipOverlay.Draw(dc, scene, camera, Selection, OverlayLook(), _preview);
        }
    }

    /// <summary>
    /// What the running drag shows, once it has left the drag rectangle (a right click alone changes nothing on screen):
    /// its crates and those its outcome moves faded in the scene, target and ghosts in the overlay (<see cref="DragPreview"/>).
    /// Without a drag both are cleared. The camera stays: the ship stands still while cargo moves.
    /// </summary>
    private void ShowDrag()
    {
        _preview = _drag is { IsMoving: true } drag && Scene is { } scene
            ? DragPreview.Of(scene.Ship, scene.Plan, drag.Dragged, drag.Gesture, drag.Outcome)
            : DragPreview.None;
        _scene3D.Fade(_preview.Faded);
        DrawOverlay();
    }

    private OverlayLook OverlayLook() => new(
        RackNumberBrush,
        RackNumberFontSize,
        RackNumberHaloBrush,
        RackNumberHaloSize,
        SelectionPen,
        SelectionOutlinePen,
        new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void StartDrag(Point position, MouseButton button)
    {
        // One drag at a time: the other button doesn't take over a running drag.
        if (_drag is not null)
        {
            return;
        }

        ShowToolTip(null);
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

    /// <summary>Ends <paramref name="drag"/> at <paramref name="position"/>: on the button's up, or from the poll where the up didn't come (D45).</summary>
    private void FinishDrag(Drag drag, Point position)
    {
        // Resolve before releasing the capture, which clears the drag; run the command after, so the new plan is drawn
        // without the fade. A refused drop runs too: the view model shows the reason (D24).
        var gesture = Resolve(drag, position, drag.IsMoving);
        ReleaseMouseCapture();

        // A drag that never got the capture gets no LostMouseCapture: it ends here, or every later press would be
        // ignored as a second drag (T8.4).
        if (_drag is not null)
        {
            _drag = null;
            ShowDrag();
            ShowToolTip(null);
        }

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

    // A new theme value restyles the crates and redraws the overlay at once (live preview, T4.2). The rack numbers' size and
    // halo set the room above a long ship's racks, so the fit and with it the camera.
    private static DependencyProperty ThemeProperty(string name, Type type, object? defaultValue) => DependencyProperty.Register(
        name,
        type,
        typeof(ShipViewControl),
        new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ShipViewControl)d).OnLookChanged()));

    // The hologram and its look: a new value rebuilds its materials, the geometry stays (D63).
    private static DependencyProperty HoloLookProperty(string name, Type type, object? defaultValue) => DependencyProperty.Register(
        name,
        type,
        typeof(ShipViewControl),
        new PropertyMetadata(defaultValue, (d, _) => ((ShipViewControl)d).OnHoloChanged()));

    /// <summary>The crates' look from the theme and the selection (T8.2), and the outline's line and halo (T8.5).</summary>
    private CrateLook Look() => new(
        StopPalette,
        CrateFillAlpha,
        (CrateEdgeBrush as SolidColorBrush)?.Color,
        SelectionFocus.Missions(Selection.In(Scene)),
        new OutlinePaint(ColourOf(RackNumberBrush, OutlineOpacity), ColourOf(RackNumberHaloBrush, 1), RackNumberHaloSize));

    // The outline is a backdrop (D62): its line in the rack numbers' colour, toned down below the racks' edges.
    private const double OutlineOpacity = 0.6;

    // A theme brush as one colour, its opacity in the alpha; null without a brush.
    private static Color? ColourOf(Brush? brush, double opacity) => brush is SolidColorBrush solid
        ? solid.Color with { A = (byte)Math.Round(solid.Color.A * solid.Opacity * opacity) }
        : null;

    private void OnLookChanged()
    {
        _scene3D.Show(Scene, Look());
        ApplyCamera();
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
