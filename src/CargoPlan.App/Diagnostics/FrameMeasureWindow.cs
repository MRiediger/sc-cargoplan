using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.Theme;
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

    // One crate size per ship fills every rack as in S8.0: the Hull C gives 144 crates. A target not listed is refused.
    // hull_c_states shows every crate state at once (T8.2): 24 stops (three colour rounds, so both hatches), every 5th stop
    // delivered, every 7th loaded, one overlapping crate (a violation), mission 1 selected.
    private static readonly Dictionary<string, (string Ship, CrateSize Size, bool States)> Targets = new()
    {
        ["utv"] = ("utv", CrateSize.One, false),
        ["hull_a"] = ("hull_a", CrateSize.Sixteen, false),
        ["hull_c"] = ("hull_c", CrateSize.ThirtyTwo, false),
        ["hull_c_states"] = ("hull_c", CrateSize.ThirtyTwo, true),
    };

    private const int StateStops = 24;
    private const int DeliveredEvery = 5;
    private const int LoadedEvery = 7;

    private readonly string _target;
    private readonly ShipViewControl _view = new();
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, LogFile);
    private readonly Ship? _ship;
    private readonly PreparedHolo? _holo;
    private readonly int _crates;

    public FrameMeasureWindow(ShipCatalog catalog, string target)
    {
        _target = target;
        Title = "SC-CargoPlan frame measurement";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
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
            Placement[] placements = [.. ship.Racks.Select(r => new Placement("Measure", r.Id, new CargoItem("Measure", r.CapacityScu, spec.Size)))];
            var plan = new LoadPlan(ship.Id, CrateLayout.LayOut(ship, placements, FillOrder.BowFirst), []);
            if (spec.States)
            {
                plan = WithStates(plan);
            }

            _crates = plan.Crates.Count;
            var path = ship.FleetyardsSlug is { } slug ? Path.Combine(HoloRelease.DefaultCacheFolder, slug + ".holo") : null;
            _holo = path is not null && File.Exists(path) ? PreparedHolo.Prepare(ship, HoloReader.Read(File.ReadAllBytes(path)), Look) : null;
            var scene = ShipViewBuilder.Build(plan, ship, spec.States ? LoadPlanner.Validate(plan, ship) : []) with
            {
                Marker = _holo is { } h ? new BowMarker.Holo(h.Placement.Min, h.Placement.Max) : new BowMarker.Wedge(),
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

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        var known = _target == Empty || _ship is not null;
        var intervals = known ? await MeasureFramesAsync() : [];
        var hits = _ship is not null ? HitSweep() : null;
        var valid = known && FrameStatistics.IsValid(intervals);

        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var subject = _ship is null ? _target : Inv($"{_target} {_crates} crates, holo {(_holo is null ? "no" : "yes")}");
        var frames = known ? FrameStatistics.Summary(intervals, Duration) : "unknown target (empty, " + string.Join(", ", Targets.Keys) + ")";
        File.AppendAllText(_logPath, Inv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {version} {subject}: frames {frames}{(hits is null ? "" : " | hits " + hits)}") + Environment.NewLine);

        Application.Current.Shutdown(valid ? 0 : 1);
    }

    // One sample per frame: Rendering can come more than once per frame, the frames differ in RenderingTime.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.renderingeventargs.renderingtime
    private Task<List<double>> MeasureFramesAsync()
    {
        var done = new TaskCompletionSource<List<double>>();
        var intervals = new List<double>();
        TimeSpan? start = null, last = null;
        var camera = _ship is { } ship ? IsoProjection.DefaultFor(ship.Bounds) : IsoProjection.Default;

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

        CompositionTarget.Rendering += OnRendering;
        return done.Task;
    }

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
