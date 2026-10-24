using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.Core.Holo;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The ship's hologram behind the ship view (D63, the spike's way b): a <see cref="Viewport3D"/> as the layer under
/// <see cref="ShipViewControl"/>, its camera following the view's (<see cref="HoloCamera"/>), so the hologram never
/// covers a crate. Collapsed without a hologram, so the outline modes draw exactly as before. Never hit: the ship view
/// above takes every click.
/// </summary>
public sealed class HoloBackdrop : FrameworkElement
{
    public static readonly DependencyProperty HoloProperty = DependencyProperty.Register(
        nameof(Holo),
        typeof(PreparedHolo),
        typeof(HoloBackdrop),
        new PropertyMetadata(null, (d, _) => ((HoloBackdrop)d).OnHoloChanged()));

    /// <summary>Bound to <see cref="ShipViewControl.View"/>.</summary>
    public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
        nameof(View),
        typeof(ShipViewState),
        typeof(HoloBackdrop),
        new PropertyMetadata(null, (d, _) => ((HoloBackdrop)d).OnViewChanged()));

    private static readonly Material Lit = Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(MissionPalette.HoloFill))));
    private static readonly Material HiddenFill = Unlit(MissionPalette.HoloHiddenFill);
    private static readonly Material Line = Unlit(MissionPalette.HoloLine);

    private readonly Viewport3D _viewport = new() { IsHitTestVisible = false };
    // Far plane at twice the camera's distance instead of the default infinity, so depth keeps its precision.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.projectioncamera.farplanedistance
    private readonly OrthographicCamera _camera = new() { NearPlaneDistance = 0, FarPlaneDistance = 2 * HoloCamera.Distance };
    private readonly DirectionalLight _light = new(MissionPalette.HoloLight, new Vector3D(0, 0, -1));
    private readonly GeometryModel3D _surface = new();
    private readonly GeometryModel3D _lines = new() { Material = Line, BackMaterial = Line };

    // What the strips were built for: they change with the camera, the scale and the edge level, not with a pan.
    private (IsoProjection Camera, double Scale, int Level)? _linesFor;

    public HoloBackdrop()
    {
        IsHitTestVisible = false;
        Opacity = MissionPalette.HoloOpacity;
        Visibility = Visibility.Collapsed;
        _viewport.Camera = _camera;
        _viewport.Children.Add(new ModelVisual3D
        {
            Content = new Model3DGroup { Children = [new AmbientLight(MissionPalette.HoloAmbient), _light, _surface, _lines] },
        });
        AddVisualChild(_viewport);
    }

    public PreparedHolo? Holo
    {
        get => (PreparedHolo?)GetValue(HoloProperty);
        set => SetValue(HoloProperty, value);
    }

    public ShipViewState? View
    {
        get => (ShipViewState?)GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    // One visual child (AddVisualChild in the constructor), laid out over the whole element; the layout overrides below
    // measure and arrange it. Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkelement.visualchildrencount
    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => index == 0 ? _viewport : throw new ArgumentOutOfRangeException(nameof(index));

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

    private void OnHoloChanged()
    {
        _linesFor = null;
        _lines.Geometry = null;
        if (Holo is not { } holo)
        {
            _surface.Geometry = null;
            Visibility = Visibility.Collapsed;
            return;
        }

        // BackMaterial too: a hull with gaps shows its inside instead of holes (T7.4b).
        var material = holo.Look == ShipOutlineMode.HoloHiddenline ? HiddenFill : Lit;
        _surface.Material = material;
        _surface.BackMaterial = material;
        _surface.Geometry = holo.Surface;
        Visibility = Visibility.Visible;
        OnViewChanged();
    }

    private void OnViewChanged()
    {
        if (View is not { } view || Holo is not { } holo)
        {
            return;
        }

        var camera = HoloCamera.For(view.Camera, view.Transform, view.Size);
        _camera.Position = camera.Position;
        _camera.LookDirection = camera.LookDirection;
        _camera.UpDirection = camera.UpDirection;
        _camera.Width = camera.Width;
        _light.Direction = camera.LookDirection;

        if (holo.Contour is not { } contour)
        {
            return;
        }

        var level = HoloEdges.LevelFor(view.Zoom);
        if (_linesFor == (view.Camera, view.Transform.Scale, level))
        {
            return;
        }

        _linesFor = (view.Camera, view.Transform.Scale, level);
        var look = camera.LookDirection;
        var contourEdges = contour.Visible(new System.Numerics.Vector3((float)look.X, (float)look.Y, (float)look.Z));
        int[] segments = [.. holo.Mesh.Edges.AsSpan(0, 2 * holo.Mesh.EdgeCountFor(level)), .. contourEdges];
        var (positions, indices) = HoloLines.Strips(holo.Points, segments, look, HoloLines.HalfWidthCells(view.Transform.Scale));
        _lines.Geometry = Frozen(new MeshGeometry3D { Positions = new Point3DCollection(positions), TriangleIndices = new Int32Collection(indices) });
    }

    // Black diffuse under an emissive colour: the colour alone, opaque and unlit. EmissiveMaterial only adds its colour to
    // the material under it.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.emissivematerial
    private static Material Unlit(Color color) => Frozen(new MaterialGroup
    {
        Children = [new DiffuseMaterial(Brushes.Black), new EmissiveMaterial(new SolidColorBrush(color))],
    });

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
