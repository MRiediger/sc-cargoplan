using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.Theme;
using CargoPlan.App.Win32;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Diagnostics;

/// <summary>
/// The measuring tool of Phase 8 (<c>--measure-frames</c>, D83): the ship view in a transparent window like the overlay's,
/// a full plan of one crate size, the cached hologram, the camera turning at 12°/s for 30 s; frame intervals from
/// <see cref="CompositionTarget.Rendering"/>, then a hit-test sweep. One line per run in measure-frames.log next to the
/// exe, exit code 0 for a valid run, else 1. Reads no settings and writes no plan: the player's files stay untouched.
/// Taken over from the S8.0 spike (Spike3DWindow), so the numbers compare with its tables.
/// </summary>
internal sealed class FrameMeasureWindow : Window
{
    public const string Empty = "empty";
    public const string LogFile = "measure-frames.log";

    private const double OrbitDegreesPerSecond = 12;
    private const int HitSamples = 600;
    private const int HitColumns = 30;
    private const double WindowWidth = 800;
    private const double WindowHeight = 600;

    // The look of the S8.0 measurements (Martin's setting then): shaded hologram at 0.45.
    private const ShipOutlineMode Look = ShipOutlineMode.HoloShaded;
    private const double HoloOpacity = 0.45;

    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    // Over the game (T8.7, M5): time to bring Star Citizen to the front before the run starts.
    private static readonly TimeSpan Lead = TimeSpan.FromSeconds(10);

    // One crate size per ship fills every rack as in S8.0: the Hull C gives 144 crates. A target not listed is refused.
    // hull_c_states shows every crate state at once (T8.2): 24 stops (three colour rounds, so both hatches), every 5th stop
    // delivered, every 7th loaded, one overlapping crate (a violation), mission 1 selected.
    // hull_c_drag (T8.4, M3) fills every rack half (72 crates of 32 SCU, the least size its racks take, D70), so a dragged
    // crate finds free columns and crates to displace; a full ship has no target on top (Gesture.None) and would measure no
    // preview. The camera stands still while the cursor drags a crate over the racks.
    // hull_a_outline (T8.5) shows the outline as a surface with its line and halo instead of the hologram.
    private static readonly Dictionary<string, (string Ship, CrateSize Size, bool States, bool Drag, bool Outline)> Targets = new()
    {
        ["utv"] = ("utv", CrateSize.One, false, false, false),
        ["hull_a"] = ("hull_a", CrateSize.Sixteen, false, false, false),
        ["hull_a_outline"] = ("hull_a", CrateSize.Sixteen, false, false, true),
        ["hull_c"] = ("hull_c", CrateSize.ThirtyTwo, false, false, false),
        ["hull_c_states"] = ("hull_c", CrateSize.ThirtyTwo, true, false, false),
        ["hull_c_drag"] = ("hull_c", CrateSize.ThirtyTwo, false, true, false),
    };

    // The cursor's path while dragging: a Lissajous figure over the middle of the view, slow enough to stop on cells.
    private const double PathWidthShare = 0.4;
    private const double PathHeightShare = 0.3;
    private const double PathPeriodX = 7;
    private const double PathPeriodY = 5;

    private const int StateStops = 24;
    private const int DeliveredEvery = 5;
    private const int LoadedEvery = 7;

    private readonly string _target;
    private readonly ShipViewControl _view = new();
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, LogFile);
    private readonly Ship? _ship;
    private readonly PreparedHolo? _holo;
    private readonly int _crates;
    private readonly bool _drag;
    private readonly bool _overGame;

    // Key changes of the drag's preview while the cursor runs its path (T8.7): far fewer than without the game means
    // Star Citizen held the cursor, even with the drag still held.
    private int _pathKeys;

    /// <param name="overGame">Lie over the game like the overlay: topmost, never the foreground window, after <see cref="Lead"/>.</param>
    public FrameMeasureWindow(ShipCatalog catalog, string target, bool overGame = false)
    {
        _target = target;
        _overGame = overGame;
        Title = "SC-CargoPlan frame measurement";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Topmost = overGame;
        ShowActivated = !overGame;
        Width = WindowWidth;
        Height = WindowHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ThemeResources.Apply(Resources, AppSettings.Default.Appearance);
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");

        // The overlay's cell, wired like OverlayWindow.xaml: the hologram is part of the view's scene (D83).
        _view.SetResourceReference(ShipViewControl.CrateFillAlphaProperty, "CratesFillAlpha");
        _view.SetResourceReference(ShipViewControl.CrateEdgeBrushProperty, "CratesEdgeBrush");
        _view.SetResourceReference(ShipViewControl.RackNumberBrushProperty, "CratesRackNumberBrush");
        _view.SetResourceReference(ShipViewControl.RackNumberFontSizeProperty, "CratesRackNumberFontSize");
        _view.SetResourceReference(ShipViewControl.RackNumberHaloBrushProperty, "CratesHaloBrush");
        _view.SetResourceReference(ShipViewControl.RackNumberHaloSizeProperty, "CratesHaloSize");
        _view.SetResourceReference(ShipViewControl.SelectionPenProperty, "CratesSelectionPen");
        _view.SetResourceReference(ShipViewControl.SelectionOutlinePenProperty, "CratesSelectionOutlinePen");
        _view.HoloOpacity = HoloOpacity;
        var cell = new Grid { ClipToBounds = true };
        cell.Children.Add(_view);
        Content = cell;

        if (target != Empty && Targets.TryGetValue(target, out var spec) && catalog.Find(spec.Ship) is { } ship)
        {
            _ship = ship;
            _drag = spec.Drag;
            var scu = spec.Drag ? (Func<Rack, int>)(r => r.CapacityScu / 2 / (int)spec.Size * (int)spec.Size) : r => r.CapacityScu;
            Placement[] placements = [.. ship.Racks.Select(r => new Placement("Measure", r.Id, new CargoItem("Measure", scu(r), spec.Size)))];
            var plan = new LoadPlan(ship.Id, CrateLayout.LayOut(ship, placements, FillOrder.BowFirst), []);
            if (spec.States)
            {
                plan = WithStates(plan);
            }

            _crates = plan.Crates.Count;
            var path = ship.FleetyardsSlug is { } slug && !spec.Outline ? Path.Combine(HoloRelease.DefaultCacheFolder, slug + ".holo") : null;
            _holo = path is not null && File.Exists(path) ? PreparedHolo.Prepare(ship, HoloReader.Read(File.ReadAllBytes(path)), Look) : null;
            var outline = spec.Outline ? ShipOutlineLoader.Load().For(ship) : null;
            var scene = ShipViewBuilder.Build(plan, ship, spec.States ? LoadPlanner.Validate(plan, ship) : []) with
            {
                Marker = _holo is { } h ? new BowMarker.Holo(h.Placement.Min, h.Placement.Max)
                    : outline is not null ? new BowMarker.Outline(OutlinePlacement.Place(outline, ship))
                    : new BowMarker.Wedge(),
            };
            _view.Scene = scene;
            _view.Holo = _holo;
            if (spec.States)
            {
                _view.Selection = CrateSelection.Of(scene, plan.Crates.Where(c => c.Mission == 1));
            }
        }
        ContentRendered += OnContentRendered;
    }

    // Like the overlay (OverlayWindow.OnSourceInitialized): the game keeps the foreground and its free cursor (D11).
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_overGame)
        {
            NativeMethods.PreventActivation(new System.Windows.Interop.WindowInteropHelper(this).Handle, showInTaskbar: true);
        }
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        var known = _target == Empty || _ship is not null;
        if (_overGame)
        {
            await Task.Delay(Lead);
        }

        var intervals = known ? await MeasureFramesAsync() : [];
        // "held" says the drag still runs after the frames: a lost capture would end it, and the sweep would time tool tips.
        var drags = _drag ? Inv($" | drag held {(_view.IsDragging ? "yes" : "NO")}, path keys {_pathKeys}, ") + DragSweep() : "";
        var hits = _ship is not null ? HitSweep() : null;
        var valid = known && FrameStatistics.IsValid(intervals);

        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var subject = (_ship is null ? _target : Inv($"{_target} {_crates} crates, holo {(_holo is null ? "no" : "yes")}, marker {_view.Scene?.Marker.GetType().Name}"))
            + (_overGame ? " over game" : "");
        var frames = known ? FrameStatistics.Summary(intervals, Duration) : "unknown target (empty, " + string.Join(", ", Targets.Keys) + ")";
        File.AppendAllText(_logPath, Inv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {version} {subject}: frames {frames}{drags}{(hits is null ? "" : " | hits " + hits)}") + Environment.NewLine);

        Application.Current.Shutdown(valid ? 0 : 1);
    }

    // One sample per frame: Rendering can come more than once per frame, the frames differ in RenderingTime.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.renderingeventargs.renderingtime
    private Task<List<double>> MeasureFramesAsync()
    {
        var done = new TaskCompletionSource<List<double>>();
        var intervals = new List<double>();
        TimeSpan? start = null, last = null;
        var camera = _ship is { } ship ? CameraOrientation.DefaultFor(ship.Bounds) : CameraOrientation.Default;
        CrateDrop.Key? pathKey = null;

        void OnRendering(object? sender, EventArgs e)
        {
            if (e is not RenderingEventArgs args || args.RenderingTime == last)
            {
                return;
            }

            start ??= args.RenderingTime;
            if (last is { } previous)
            {
                var dt = (args.RenderingTime - previous).TotalMilliseconds;
                intervals.Add(dt);

                // Dragging: the camera stands still, the cursor moves along the path and the view's poll follows it as
                // in the game (D45).
                if (_drag)
                {
                    if (KeyNow() is var now && !Equals(now, pathKey))
                    {
                        (_pathKeys, pathKey) = (_pathKeys + 1, now);
                    }

                    NativeMethods.MoveCursor(_view.PointToScreen(PathAt((args.RenderingTime - start.Value).TotalSeconds)));
                    last = args.RenderingTime;
                    if (args.RenderingTime - start >= Duration)
                    {
                        CompositionTarget.Rendering -= OnRendering;
                        done.SetResult(intervals);
                    }

                    return;
                }

                // Yaw and pitch only: the view fits, the same input before and after the 3D view (D83). The empty window
                // redraws its bare view instead; unchanged it gets about 32 frames a second (M0, T8.1).
                camera = camera.OrbitBy(OrbitDegreesPerSecond * dt / 1000, 0);
                _view.SetCurrentValue(ShipViewControl.CameraProperty, new ShipCamera(camera.Yaw, camera.Pitch));
                if (_ship is null)
                {
                    _view.InvalidateVisual();
                }
            }

            last = args.RenderingTime;
            if (args.RenderingTime - start >= Duration)
            {
                CompositionTarget.Rendering -= OnRendering;
                done.SetResult(intervals);
            }
        }

        // Dragging starts with a right press on a crate still on board, as a player would.
        if (_drag && FirstCrate() is { } press)
        {
            NativeMethods.MoveCursor(_view.PointToScreen(press));
            _view.Press(MouseButton.Right, press, 1);
        }

        CompositionTarget.Rendering += OnRendering;
        return done.Task;
    }

    /// <summary>The cursor's place on the drag path <paramref name="seconds"/> into the run, in the view's DIPs.</summary>
    private Point PathAt(double seconds) => new(
        _view.ActualWidth * (0.5 + PathWidthShare * Math.Sin(2 * Math.PI * seconds / PathPeriodX)),
        _view.ActualHeight * (0.5 + PathHeightShare * Math.Sin(2 * Math.PI * seconds / PathPeriodY)));

    /// <summary>The first point of the hit grid on a crate still on board; null if there is none.</summary>
    private Point? FirstCrate()
    {
        var rows = HitSamples / HitColumns;
        return Enumerable.Range(0, HitSamples)
            .Select(i => new Point((i % HitColumns + 0.5) * _view.ActualWidth / HitColumns, (i / HitColumns + 0.5) * _view.ActualHeight / rows))
            .Select(p => (Point?)p)
            .FirstOrDefault(p => _view.HitAt(p!.Value)?.Crate is { Delivered: false });
    }

    /// <summary>
    /// The drag's steps outside the frames: the running drag moved over a grid on the racks' part of the view, timed one
    /// by one; a step whose preview key changed (<see cref="CrateDrop.KeyOf"/>) asked Core, the D65 budget (T8.4, M3).
    /// The racks fill little of the view with the hologram's fit (23 of 600 points of the whole view hit one), so the
    /// grid lies on the rectangle around the points that hit a rack, found on a finer grid first.
    /// </summary>
    private string DragSweep()
    {
        var area = RackArea();
        var moves = new List<double>(HitSamples);
        var previews = new List<double>();
        var rows = HitSamples / HitColumns;
        var key = KeyNow();
        for (var i = 0; i < HitSamples; i++)
        {
            var point = new Point(area.X + (i % HitColumns + 0.5) * area.Width / HitColumns, area.Y + (i / HitColumns + 0.5) * area.Height / rows);
            var watch = Stopwatch.StartNew();
            _view.MoveTo(point);
            watch.Stop();
            moves.Add(watch.Elapsed.TotalMilliseconds);
            if (KeyNow() is var now && !Equals(now, key))
            {
                previews.Add(watch.Elapsed.TotalMilliseconds);
                key = now;
            }
        }

        return FrameStatistics.DragSummary(moves, previews);
    }

    // The finer grid that finds the racks' part of the view: about 4 px apart in the 800 × 600 window.
    private const int AreaColumns = 200;
    private const int AreaRows = 150;

    /// <summary>The rectangle around every point of a fine grid that hits a rack; the whole view if none does.</summary>
    private Rect RackArea()
    {
        var area = Rect.Empty;
        for (var i = 0; i < AreaColumns * AreaRows; i++)
        {
            var point = new Point((i % AreaColumns + 0.5) * _view.ActualWidth / AreaColumns, (i / AreaColumns + 0.5) * _view.ActualHeight / AreaRows);
            if (_view.HitAt(point) is not null)
            {
                area.Union(point);
            }
        }

        return area.IsEmpty ? new Rect(0, 0, _view.ActualWidth, _view.ActualHeight) : area;
    }

    private CrateDrop.Key? KeyNow() => _view.PendingGesture is Gesture.MoveCrates move ? CrateDrop.KeyOf(_ship!, move) : null;

    /// <summary>The view's own hit test on a 30 × 20 grid of points, timed one by one: rays since T8.3 (M2).</summary>
    private string HitSweep()
    {
        var times = new List<double>(HitSamples);
        var hits = 0;
        var rows = HitSamples / HitColumns;
        for (var i = 0; i < HitSamples; i++)
        {
            var point = new Point((i % HitColumns + 0.5) * _view.ActualWidth / HitColumns, (i / HitColumns + 0.5) * _view.ActualHeight / rows);
            var watch = Stopwatch.StartNew();
            var hit = _view.HitAt(point);
            watch.Stop();
            times.Add(watch.Elapsed.TotalMilliseconds);
            hits += hit is null ? 0 : 1;
        }

        return FrameStatistics.HitSummary(times, hits);
    }

    /// <summary>
    /// The full plan spread over <see cref="StateStops"/> stops, one mission each; whole stops delivered or loaded, as
    /// the legend does it; one more crate on the first crate's cells, which <c>Validate</c> reports.
    /// </summary>
    private static LoadPlan WithStates(LoadPlan plan)
    {
        Crate[] crates =
        [
            .. plan.Crates.Select((crate, i) =>
            {
                var stop = i % StateStops;
                var spread = crate with { Destination = Inv($"Stop {stop + 1:00}"), Mission = stop + 1 };
                return stop % DeliveredEvery == DeliveredEvery - 1 ? spread.AsDelivered()
                    : stop % LoadedEvery == LoadedEvery - 1 ? spread.AsLoaded()
                    : spread;
            }),
        ];
        return plan with { Crates = [.. crates, crates[0] with { Destination = "Overlap", Mission = StateStops + 1 }] };
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
