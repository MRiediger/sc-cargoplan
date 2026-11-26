using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CargoPlan.App.Routing;
using CargoPlan.App.Settings;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;

namespace CargoPlan.App.Tests;

/// <summary>
/// The map view rendered as a control (T13.7, D128; the T13.2 backlog's render test of a Plan tab view): labels in the
/// legend's font, lines solid or dashed, the dimmed and the route's, zoom that only projects (K4). WPF only through
/// <see cref="Sta.Run"/>, in software (A12). The pin's data only where the pin is the test (A10): the label overlap.
/// Env CARGOPLAN_MAP_PNG = a folder: the pin's maps as PNG there, for the picture by hand.
/// </summary>
public sealed class RouteMapViewTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L5 = "HUR-L5 High Course Station";
    private const string Baijini = "Baijini Point";

    private static readonly Size Wide = new(1100, 700);

    private static readonly DateTime Monday = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Positions Places = new(
    [
        P(Everus, "Manmade", 12.849, 0.001), P(L1, "Manmade_VisibleOnInteraction", 11.56, 0),
        P(L5, "Manmade_VisibleOnInteraction", 6.43, -11.13), P(Baijini, "Manmade", 18.59, -22.15),
    ]);

    private static readonly HaulTemplate Seen = T([Everus], [L1], "seen");

    [Theory]
    [InlineData(StarSystem.Stanton)]
    [InlineData(StarSystem.Pyro)]
    public void The_pins_map_at_1100_by_700_has_a_label_per_node_and_no_two_shown_labels_overlap(StarSystem system) => Sta.Run(() =>
    {
        var model = PinModel(system);
        var view = Shown(model.Map!);

        view.Labels.Keys.Should().BeEquivalentTo(model.Map!.Graph.Nodes.Select(n => n.Key));
        Rect[] boxes = [.. view.Labels.Values.Where(b => b.Visibility == Visibility.Visible).Select(Box)];
        boxes.SelectMany((a, i) => boxes.Skip(i + 1).Where(b => a.IntersectsWith(b) && Rect.Intersect(a, b) is { Width: > 0, Height: > 0 })).Should().BeEmpty();
        Picture(view, $"map-{system}.png");

        model.FocusCommand.Execute(system == StarSystem.Stanton ? Everus : "Ruin Station");
        view.Scene = model.Map;
        view.UpdateLayout();
        Picture(view, $"map-{system}-focus.png");
    });

    [Fact]
    public void A_template_edge_is_dashed_an_observed_one_solid_the_dimmed_at_a_quarter_and_the_route_in_the_status_colour() => Sta.Run(() =>
    {
        var model = Model([Seen, T([Everus], [L5]), T([Baijini], [L1])], observed: true);
        model.FocusCommand.Execute(Everus);
        model.ShowRoute(new Route("Everus Harbor ring", Monday, Everus, LoopKind.LocalRing, StarSystem.Stanton, [L5], [], []));
        var view = Shown(model.Map!);

        var lines = Lines(view);
        lines.Where(l => !l.Route).Should().HaveCount(3);
        lines.Single(l => l.From == Everus && l.To == L1).Should().BeEquivalentTo(new { Dashed = false, Dimmed = false }, "seen in a scan: solid");
        lines.Single(l => l.From == Everus && l.To == L5 && !l.Route).Should().BeEquivalentTo(new { Dashed = true, Dimmed = false }, "the pools only: dashed");
        lines.Single(l => l.From == Baijini).Dimmed.Should().BeTrue("the focus is Everus");
        lines.Where(l => l.Route).Should().HaveCount(2).And.OnlyContain(l => l.Brush == RouteBrush && l.Thickness == 3);
        view.Numbers.Should().ContainKey(L5).WhoseValue.Text.Should().Be("1");
    });

    [Fact]
    public void An_own_plan_is_drawn_in_the_lines_colour_there_and_back() => Sta.Run(() =>
    {
        // T13.7b: the draft's legs, thicker than an edge's strongest, under the active route.
        var model = Model([T([Everus], [L1]), T([L1], [Everus])]);
        model.ShowShip(CargoPlan.Core.Tests.FrozenShips.Load("hull_b"));
        model.FocusCommand.Execute(Everus);
        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        var view = Shown(model.Map!);

        Lines(view).Where(l => l.Thickness == 2.5).Select(l => (l.From, l.To, l.Brush)).Should().Equal((Everus, L1, Brushes.White), (L1, Everus, Brushes.White));
    });

    [Fact]
    public void A_zoom_only_projects_the_same_layout_and_labels_move_keep_their_size_and_stop_at_the_bounds() => Sta.Run(() =>
    {
        var view = Shown(Model([T([Everus], [L1]), T([Everus], [Baijini])]).Map!);
        var layout = view.Layout;
        var labels = view.Labels.Values.ToList();
        var (left, size, redraws) = (Canvas.GetLeft(view.Labels[Baijini]), view.Labels[Baijini].FontSize, view.Redraws);

        view.ZoomAt(new Point(300, 200), 120);

        view.Layout.Should().BeSameAs(layout, "no layout anew (K4)");
        view.Labels.Values.Should().Equal(labels, "the same text blocks, nothing built");
        view.Redraws.Should().Be(redraws + 1);
        view.Zoom.Should().BeApproximately(RouteMapView.ZoomStep, 1e-9);
        Canvas.GetLeft(view.Labels[Baijini]).Should().NotBe(left);
        view.Labels[Baijini].FontSize.Should().Be(size, "text keeps its size");

        for (var i = 0; i < 30; i++)
        {
            view.ZoomAt(new Point(300, 200), 120);
        }

        view.Zoom.Should().Be(RouteMapView.MaxZoom);
        for (var i = 0; i < 60; i++)
        {
            view.ZoomAt(new Point(300, 200), -120);
        }

        view.Zoom.Should().Be(RouteMapView.MinZoom);
    });

    [Fact]
    public void A_new_size_or_a_new_scene_keeps_the_zoom_and_the_part_of_the_map_in_the_middle() => Sta.Run(() =>
    {
        // Gate finding 7b (Martin): resizing the window or a click on an edge (its plan's bar takes room) reset the zoom.
        var model = Model([T([Everus], [L1]), T([Everus], [Baijini])]);
        var view = Shown(model.Map!);
        view.ZoomAt(new Point(700, 200), 360);
        var (zoom, pan, scale) = (view.Zoom, view.Pan, view.Layout!.PixelsPerGm);

        view.Measure(new Size(1100, 560));
        view.Arrange(new Rect(0, 0, 1100, 560));
        view.UpdateLayout();

        view.Zoom.Should().Be(zoom);
        var ratio = view.Layout!.PixelsPerGm / scale;
        ratio.Should().BeLessThan(1, "a smaller view lays the system out smaller");
        (view.Pan.X, view.Pan.Y).Should().Be((pan.X * ratio, pan.Y * ratio), "the point in the middle stays in the middle");

        var before = view.Pan;
        view.Scene = model.Map! with { Hub = null };
        (view.Zoom, view.Pan).Should().Be((zoom, before), "a new scene over the same nodes is no new layout");
    });

    [Fact]
    public void The_arrow_heads_show_from_half_the_wheels_notches_on() => Sta.Run(() =>
    {
        // Gate finding 7b (Martin): at every zoom the heads were too much; from half the notches between all out (0.5x)
        // and all in (6x), no zoom steps: √(0.5 · 6) ≈ 1.73x, the 7th of about 14 notches from all out, the 4th from fit.
        var model = Model([T([Everus], [L1]), T([L1], [Everus]), T([Everus], [Baijini])]);
        model.ShowRoute(new Route("Everus Harbor ring", Monday, Everus, LoopKind.LocalRing, StarSystem.Stanton, [L1], [], []));
        var view = Shown(model.Map!);

        RouteMapView.ArrowsFromZoom.Should().BeApproximately(Math.Sqrt(3), 1e-9);
        Heads(view).Should().Be(0, "fit");
        for (var notch = 1; notch <= 3; notch++)
        {
            view.ZoomAt(new Point(550, 350), 120);
        }

        Heads(view).Should().Be(0, "three notches in: 1.73x is just under the half");
        view.ZoomAt(new Point(550, 350), 120);
        Heads(view).Should().Be(3 + 2, "the 4th notch from fit: three edges and the route's two legs");
    });

    [Fact]
    public void An_arrow_head_sits_in_the_middle_of_its_line_and_is_ten_pixels_long() => Sta.Run(() =>
    {
        // Gate finding 7b (Martin): a little larger, in the middle of the line, not at its end.
        var view = Shown(Model([T([Everus], [Baijini])]).Map!);
        view.Project(RouteMapView.MaxZoom, default);

        var line = Lines(view).Single();
        var head = Geometries(view).OfType<StreamGeometry>().Single().Bounds;
        var middle = new Point((line.A.X + line.B.X) / 2, (line.A.Y + line.B.Y) / 2);

        (head.X + (head.Width / 2) - middle.X).Should().BeInRange(-3, 3);
        (head.Y + (head.Height / 2) - middle.Y).Should().BeInRange(-3, 3);
        Math.Max(head.Width, head.Height).Should().BeGreaterThanOrEqualTo(RouteMapView.ArrowLength - 0.5);
    });

    [Fact]
    public void The_wheel_zooms_about_the_pointer_and_fit_goes_back() => Sta.Run(() =>
    {
        var view = Shown(Model([T([Everus], [Baijini])]).Map!);
        var before = view.Layout!.Project(view.Layout.Points[Baijini], 1, default);

        view.ZoomAt(new Point(before.X, before.Y), 240);

        var after = view.Layout.Project(view.Layout.Points[Baijini], view.Zoom, view.Pan);
        (after.X, after.Y).Should().Be((before.X, before.Y), "what lies under the pointer stays there");

        view.PanBy(new Vector(20, -10));
        view.Fit();
        (view.Zoom, view.Pan).Should().Be((1, default(MapPoint)));
    });

    [Fact]
    public void The_tool_tip_under_the_pointer_is_the_nodes_or_the_nearest_edges_and_free_room_has_none() => Sta.Run(() =>
    {
        var map = Model([T([Everus], [Baijini])]).Map!;
        var view = Shown(map);
        var (e, b) = (view.Layout!.Points[Everus], view.Layout.Points[Baijini]);

        view.ToolTipAt(new Point(e.X, e.Y)).Should().Be(map.NodeToolTip(map.Graph.Nodes.Single(n => n.Key == Everus)));
        view.ToolTipAt(new Point((e.X + b.X) / 2, (e.Y + b.Y) / 2)).Should().Be(map.EdgeToolTip(map.Graph.Edge(Everus, Baijini)!));
        view.ToolTipAt(new Point(5, 5)).Should().BeNull();
    });

    private static Brush RouteBrush { get; } = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00));

    // The view at the overlay's size for the map, with the legend's style pinned to Segoe UI 12 px (the font of CI; a
    // machine at 110 % would measure 11 px) and the status line's colour.
    private static RouteMapView Shown(MapScene scene)
    {
        var view = new RouteMapView();
        view.Resources["LegendTextStyle"] = new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI")), new Setter(TextBlock.FontSizeProperty, 12.0),
                new Setter(TextBlock.ForegroundProperty, Brushes.White),
            },
        };
        view.Resources["LegendForegroundBrush"] = Brushes.White;
        view.Resources["NoticeForegroundBrush"] = RouteBrush;
        view.Scene = scene;
        view.Measure(Wide);
        view.Arrange(new Rect(Wide));
        view.UpdateLayout();
        return view;
    }

    private static Rect Box(TextBlock block) => new(Canvas.GetLeft(block), Canvas.GetTop(block), block.ActualWidth, block.ActualHeight);

    private sealed record Line(string? From, string? To, bool Dashed, bool Dimmed, bool Route, Brush Brush, double Thickness, Point A = default, Point B = default);

    // Every geometry the drawing holds.
    private static List<Geometry> Geometries(RouteMapView view)
    {
        List<Geometry> all = [];
        void Walk(Drawing d)
        {
            if (d is DrawingGroup g)
            {
                foreach (var child in g.Children)
                {
                    Walk(child);
                }
            }
            else if (d is GeometryDrawing { Geometry: { } geometry })
            {
                all.Add(geometry);
            }
        }

        Walk(VisualTreeHelper.GetDrawing(Host(view)) ?? new DrawingGroup());
        return all;
    }

    // The drawing's lines, told apart by their pens; each mapped back to its edge or leg by its ends.
    private static List<Line> Lines(RouteMapView view)
    {
        var drawing = VisualTreeHelper.GetDrawing(Host(view)) ?? new DrawingGroup();
        var points = view.Layout!.Points.ToDictionary(p => p.Key, p => view.Layout.Project(p.Value, view.Zoom, view.Pan));
        string? Near(Point p) => points.MinBy(kv => (Math.Pow(kv.Value.X - p.X, 2) + Math.Pow(kv.Value.Y - p.Y, 2))).Key;
        List<Line> lines = [];
        void Walk(Drawing d, bool dimmed)
        {
            switch (d)
            {
                case DrawingGroup g:
                    foreach (var child in g.Children)
                    {
                        Walk(child, dimmed || g.Opacity < 1);
                    }

                    break;
                case GeometryDrawing { Geometry: LineGeometry line, Pen: { } pen }:
                    var route = pen.Brush == view.TryFindResource("NoticeForegroundBrush");
                    lines.Add(new Line(Near(line.StartPoint), Near(line.EndPoint), pen.DashStyle != DashStyles.Solid, dimmed, route, pen.Brush, pen.Thickness, line.StartPoint, line.EndPoint));
                    break;
            }
        }

        Walk(drawing, false);
        return lines;
    }

    // The arrow heads: filled geometries that are no line and no disc.
    private static int Heads(RouteMapView view)
    {
        static int Count(Drawing d) => d switch
        {
            DrawingGroup g => g.Children.Sum(Count),
            GeometryDrawing { Geometry: StreamGeometry } => 1,
            _ => 0,
        };

        return Count(VisualTreeHelper.GetDrawing(Host(view)) ?? new DrawingGroup());
    }

    private static Visual Host(RouteMapView view) =>
        Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(view)).Select(i => VisualTreeHelper.GetChild(view, i)).OfType<FrameworkElement>()
            .Where(c => c is not TextBlock).Select(c => (Visual)VisualTreeHelper.GetChild(c, 0)).First();

    private static RoutesViewModel Model(IReadOnlyList<HaulTemplate> templates, bool observed = false)
    {
        var edges = observed ? Observed() : null;
        var model = new RoutesViewModel(new HaulingLoad.Loaded(new HaulingData(templates, Places), []), new ShipDataVersion(2026, 11, 2), RoutesSettings.Default, null, edges);
        model.PreviousCommand.Execute("view");
        return model;
    }

    // A scan of the "seen" template from Everus to L1.
    private static ObservedEdges Observed()
    {
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 100_000, ["Gold"]);
        var scan = new ObservedScan(key, null, [Everus], [new ObservedDelivery(L1, "Gold", 30)], Monday);
        return ObservedEdges.From(new Observations([Observation.Of(scan).With(scan)]), [Seen], Places);
    }

    private static RoutesViewModel PinModel(StarSystem system)
    {
        var load = HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder));
        var model = new RoutesViewModel(load, new ShipDataVersion(2026, 11, 2), RoutesSettings.Default with { Rank = "senior" }, null);
        if (system == StarSystem.Pyro)
        {
            model.NextCommand.Execute("system");
        }

        model.PreviousCommand.Execute("view");
        return model;
    }

    // By hand: the view as a PNG into CARGOPLAN_MAP_PNG, on the overlay's dark background.
    private static void Picture(RouteMapView view, string name)
    {
        if (Environment.GetEnvironmentVariable("CARGOPLAN_MAP_PNG") is not { Length: > 0 } folder)
        {
            return;
        }

        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x1C)), null, new Rect(Wide));
        }

        var bitmap = new RenderTargetBitmap((int)Wide.Width, (int)Wide.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(background);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(folder);
        using var file = File.Create(Path.Combine(folder, name));
        encoder.Save(file);
    }

    private static HaulTemplate T(string[] pickups, string[] dropoffs, string? uuid = null) => new(
        uuid ?? Guid.NewGuid().ToString("N"), "d", HaulContractor.Covalex, "Senior Rank - Medium Cargo Haul", HaulRank.Senior, HaulSize.Medium, false,
        HaulShape.AToB, 1, StarSystem.Stanton, "Stanton1", HaulMissionType.Stellar, null, 100_000, [new HaulLeg("Gold", 10, 64, CrateSize.Sixteen)], pickups, dropoffs);

    private static StarPosition P(string name, string type, double xGm, double yGm) =>
        new(name, name, StarSystem.Stanton, type, null, xGm * 1e9, yGm * 1e9, 0, []);
}
