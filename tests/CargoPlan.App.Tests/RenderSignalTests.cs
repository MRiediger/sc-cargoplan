using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using CargoPlan.App.Vr;

namespace CargoPlan.App.Tests;

// T14.2, K1: the dirty signal comes from the UI thread's render work (Dispatcher.Hooks), not from CompositionTarget.Rendering,
// which renders every frame once it has a handler. Guard of a WPF internal: these cases decide whether it holds.
public class RenderSignalTests
{
    private static readonly TimeSpan Rest = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(150);

    [Fact]
    public void At_rest_there_is_no_signal() => Sta.Run(() =>
    {
        using var scene = new Scene();

        scene.Signals = 0;
        Pump(Rest);

        scene.Signals.Should().Be(0);
    });

    [Fact]
    public void A_colour_change_of_a_brush_signals() => Sta.Run(() =>
    {
        // A Freezable changes without a layout pass: LayoutUpdated would miss it.
        using var scene = new Scene();

        scene.Signals = 0;
        scene.Brush.Color = Colors.Orange;
        Pump(Short);

        scene.Signals.Should().BeGreaterThan(0);
    });

    [Fact]
    public void A_camera_move_in_a_viewport_signals() => Sta.Run(() =>
    {
        // The holo turns by its camera alone (D83).
        using var scene = new Scene();

        scene.Signals = 0;
        scene.Camera.Position = new Point3D(0, 1, 5);
        Pump(Short);

        scene.Signals.Should().BeGreaterThan(0);
    });

    [Fact]
    public void A_running_animation_signals_on_and_on() => Sta.Run(() =>
    {
        using var scene = new Scene();

        scene.Signals = 0;
        scene.Spot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.2, TimeSpan.FromSeconds(2)) { RepeatBehavior = RepeatBehavior.Forever });
        Pump(Rest);

        scene.Signals.Should().BeGreaterThanOrEqualTo(5);
    });

    [Fact]
    public void Detached_it_signals_nothing() => Sta.Run(() =>
    {
        using var scene = new Scene();

        scene.Signal.Detach();
        scene.Signals = 0;
        scene.Brush.Color = Colors.Orange;
        Pump(Short);

        scene.Signals.Should().Be(0);
    });

    [Fact]
    public void The_style_takes_the_window_background_and_the_rounding_of_the_root_border() => Sta.Run(() =>
    {
        // K9, E1: the key is the Preferences' window background, the rounding in physical pixels.
        using var scene = new Scene();
        scene.Window.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xA0, 0x14, 0x14, 0x14));

        var style = VrFrameStyle.Of(scene.Window);

        var scale = VisualTreeHelper.GetDpi(scene.Window).DpiScaleX;
        style.Should().Be(new VrFrameStyle(0xA0141414, 4 * scale));
    });

    [Fact]
    public void Without_that_brush_there_is_no_key() => Sta.Run(() =>
    {
        using var scene = new Scene();

        VrFrameStyle.Of(scene.Window).Key.Should().BeNull();
    });

    private static void Pump(TimeSpan duration)
    {
        // Background priority like the app's timers: not render work itself.
        DispatcherFrame frame = new();
        DispatcherTimer timer = new() { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>A shown layered window like the overlay: a rounded root border, a rectangle and a small viewport.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene()
        {
            Camera = new PerspectiveCamera(new Point3D(0, 0, 5), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 40);
            var cube = new GeometryModel3D(new MeshGeometry3D
            {
                Positions = [new(-1, -1, 0), new(1, -1, 0), new(0, 1, 0)],
                TriangleIndices = [0, 1, 2],
            }, new DiffuseMaterial(Brushes.White));
            var viewport = new Viewport3D { Camera = Camera, Width = 80, Height = 60 };
            viewport.Children.Add(new ModelVisual3D { Content = new Model3DGroup { Children = [cube, new AmbientLight(Colors.White)] } });

            Spot = new Rectangle { Width = 20, Height = 20, Fill = Brush };
            var panel = new StackPanel();
            panel.Children.Add(Spot);
            panel.Children.Add(viewport);

            Window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 200,
                Height = 150,
                Left = 100,
                Top = 100,
                Content = new Border { CornerRadius = new CornerRadius(4), Background = Brushes.DimGray, Child = panel },
            };
            Window.Show();
            Pump(Rest);

            Signal = new RenderSignal(Window.Dispatcher, () => Signals++);
            Signal.Attach();
        }

        public Window Window { get; }

        public SolidColorBrush Brush { get; } = new(Colors.SteelBlue);

        public PerspectiveCamera Camera { get; }

        public Rectangle Spot { get; }

        public RenderSignal Signal { get; }

        public int Signals { get; set; }

        public void Dispose()
        {
            Signal.Detach();
            Window.Close();
        }
    }
}
