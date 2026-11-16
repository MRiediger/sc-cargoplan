using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// The header (B15, D110): ship name, capacity long or compact, the status, the tool buttons and gear and exit (chrome) at
/// the right end. Short of room the status goes first, then the capacity's long form, then the tools move to a second line,
/// left-aligned, while the chrome stays; the name trims only when it, the short capacity and the chrome are wider than the
/// row. Fixed-width borders keep the slots independent of a font; the TextBlock tests and the ship data test measure text.
/// </summary>
public class HeaderPanelTests(ITestOutputHelper output)
{
    private const double LineHeight = 30;

    private const double ButtonHeight = 26;

    /// <summary>The header buttons' width (HeaderButtonStyle).</summary>
    private const double HeaderButtonWidth = 30;

    private static Border Child(double width, HeaderPart part, double height = 10)
    {
        var child = new Border { Width = width, Height = height };
        HeaderPanel.SetPart(child, part);
        return child;
    }

    private sealed record Parts(HeaderPanel Panel, Border Name, Border Capacity, Border Compact, Border Status, Border Tools, Border Chrome);

    /// <summary>
    /// Name 80, capacity 140, compact 40, status 100, tools 140 × 26, chrome 60 × 26: 520 for all of it, as in
    /// <see cref="HeaderLayoutTests"/>.
    /// </summary>
    private static Parts Header()
    {
        Parts parts = new(new HeaderPanel { LineHeight = LineHeight }, Child(80, HeaderPart.Name), Child(140, HeaderPart.Capacity),
            Child(40, HeaderPart.CompactCapacity), Child(100, HeaderPart.Status), Child(140, HeaderPart.Tools, ButtonHeight),
            Child(60, HeaderPart.Chrome, ButtonHeight));
        foreach (var child in new[] { parts.Name, parts.Capacity, parts.Compact, parts.Status, parts.Tools, parts.Chrome })
        {
            parts.Panel.Children.Add(child);
        }

        return parts;
    }

    /// <summary>Measured and arranged at <paramref name="width"/> and the height it asks for, as the overlay's DockPanel does.</summary>
    private static void Layout(HeaderPanel panel, double width)
    {
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
    }

    private static Rect Slot(UIElement child) => LayoutInformation.GetLayoutSlot((FrameworkElement)child);

    [Fact]
    public void With_room_everything_stands_on_one_line_status_tools_and_chrome_at_the_right_end()
    {
        Sta.Run(() =>
        {
            var h = Header();

            Layout(h.Panel, 600);

            h.Panel.Stage.Should().Be(HeaderStage.Full);
            h.Panel.DesiredSize.Height.Should().Be(LineHeight);
            (Slot(h.Name).X, Slot(h.Name).Width).Should().Be((0, 80));
            (Slot(h.Capacity).X, Slot(h.Capacity).Width).Should().Be((80, 140));
            Slot(h.Compact).Width.Should().Be(0);
            (Slot(h.Status).X, Slot(h.Status).Width).Should().Be((300, 100));
            (Slot(h.Tools).X, Slot(h.Tools).Y).Should().Be((400, 0));
            (Slot(h.Chrome).X, Slot(h.Chrome).Y).Should().Be((540, 0));
        });
    }

    [Fact]
    public void One_short_of_everything_the_status_goes()
    {
        Sta.Run(() =>
        {
            var h = Header();

            Layout(h.Panel, 519);

            h.Panel.Stage.Should().Be(HeaderStage.NoStatus);
            (Slot(h.Name).Width, Slot(h.Capacity).Width, Slot(h.Status).Width).Should().Be((80, 140, 0));
            (Slot(h.Tools).X, Slot(h.Chrome).X).Should().Be((319, 459));
        });
    }

    [Fact]
    public void One_short_of_name_capacity_and_buttons_the_capacity_turns_compact()
    {
        Sta.Run(() =>
        {
            var h = Header();

            Layout(h.Panel, 419);

            h.Panel.Stage.Should().Be(HeaderStage.CompactCapacity);
            h.Panel.DesiredSize.Height.Should().Be(LineHeight);
            (Slot(h.Name).Width, Slot(h.Capacity).Width, Slot(h.Status).Width).Should().Be((80, 0, 0));
            (Slot(h.Compact).X, Slot(h.Compact).Width).Should().Be((80, 40));
            (Slot(h.Tools).X, Slot(h.Tools).Y, Slot(h.Chrome).X).Should().Be((219, 0, 359));
        });
    }

    [Fact]
    public void Then_the_tools_go_left_aligned_to_a_second_line_and_gear_and_exit_stay_at_the_right_end()
    {
        // Martin's stage 3: the right of the second line stays free, so it still moves the window.
        Sta.Run(() =>
        {
            var h = Header();

            Layout(h.Panel, 319);

            h.Panel.Stage.Should().Be(HeaderStage.ToolsWrapped);
            h.Panel.DesiredSize.Height.Should().Be(LineHeight + ButtonHeight);
            (Slot(h.Name).Width, Slot(h.Compact).X, Slot(h.Compact).Width).Should().Be((80, 80, 40));
            Slot(h.Chrome).Should().Be(new Rect(259, 0, 60, LineHeight));
            Slot(h.Tools).Should().Be(new Rect(0, LineHeight, 140, ButtonHeight));
        });
    }

    [Fact]
    public void Only_when_name_compact_capacity_and_chrome_are_wider_than_the_row_the_name_trims()
    {
        Sta.Run(() =>
        {
            var h = Header();

            Layout(h.Panel, 150);

            h.Panel.Stage.Should().Be(HeaderStage.ToolsWrapped);
            (Slot(h.Name).Width, Slot(h.Compact).X, Slot(h.Chrome).X).Should().Be((50, 50, 90));
        });
    }

    [Fact]
    public void Locked_the_collapsed_buttons_take_no_line()
    {
        // Locked, tools and chrome measure 0 (D110): the second line goes by itself, the first is name, capacity, padlock.
        Sta.Run(() =>
        {
            var h = Header();
            h.Tools.Visibility = Visibility.Collapsed;
            h.Chrome.Visibility = Visibility.Collapsed;

            Layout(h.Panel, 150);

            h.Panel.DesiredSize.Height.Should().Be(LineHeight);
            Slot(h.Name).Width.Should().Be(80);
        });
    }

    [Fact]
    public void A_part_that_gave_way_is_clipped_to_nothing()
    {
        // Neither drawn nor hit: its tool tip can't come up over the name.
        Sta.Run(() =>
        {
            var h = Header();

            Layout(h.Panel, 419);

            foreach (var gone in new[] { h.Capacity, h.Status })
            {
                var clip = LayoutInformation.GetLayoutClip(gone);
                clip.Should().NotBeNull();
                (clip!.Bounds.IsEmpty || clip.Bounds.Width * clip.Bounds.Height == 0).Should().BeTrue();
            }
        });
    }

    [Fact]
    public void A_status_that_comes_later_measures_the_header_again()
    {
        // "All stops delivered." comes and goes by its own binding; the panel never changes a child's visibility.
        Sta.Run(() =>
        {
            var h = Header();
            h.Status.Visibility = Visibility.Collapsed;
            Layout(h.Panel, 500);
            h.Panel.Stage.Should().Be(HeaderStage.Full);

            h.Status.Visibility = Visibility.Visible;
            Layout(h.Panel, 500);

            h.Panel.Stage.Should().Be(HeaderStage.NoStatus);
        });
    }

    [Theory]
    [InlineData("ARGO RAFT", 192)]
    [InlineData("MISC Hull C", 4608)]
    public void The_ship_name_stands_in_full_in_every_stage(string shipName, int totalScu)
    {
        Sta.Run(() =>
        {
            var texts = Texts(shipName, Capacity(totalScu, waiting: 16), Compact(totalScu, totalScu), "All stops delivered.");
            var w = Widths(texts);
            double[] limits =
            [
                w.Name + w.Capacity + w.Status + w.Tools + w.Chrome,
                w.Name + w.Capacity + w.Tools + w.Chrome,
                w.Name + w.CompactCapacity + w.Tools + w.Chrome,
                w.Name + w.CompactCapacity + w.Chrome,
            ];
            HeaderStage[] stages = [HeaderStage.Full, HeaderStage.NoStatus, HeaderStage.CompactCapacity, HeaderStage.ToolsWrapped];

            foreach (var (available, stage) in limits.Zip(stages))
            {
                Layout(texts.Panel, Math.Ceiling(available));

                texts.Panel.Stage.Should().Be(stage);
                Slot(texts.Name).Width.Should().BeGreaterThanOrEqualTo(w.Name, $"{shipName} untrimmed at {stage}");
            }
        });
    }

    [Fact]
    public void At_the_minimum_width_every_ship_name_stands_in_full_with_its_own_short_capacity()
    {
        // Data test over ships/ (A10), Martin's rule: per ship its name with the selection's chevron, its own short capacity
        // "N/N +N" (N its capacity, same digits and comma) and gear and exit on the first line; the maximum over all ships,
        // plus padding and the lock window's margin, rounded up to 10 px. More waiting cargo than capacity with a digit
        // more may still trim a name (D110). The second line holds every tool, undo and redo included.
        var overlay = OverlayXaml();
        var minWidth = Number(overlay.Attribute("MinWidth"));
        var frame = overlay.Elements().Single(e => e.Name.LocalName == "Border");
        var padding = 2 * Number(frame.Attribute("Padding"));
        var header = frame.Descendants().Single(e => e.Name.LocalName == "HeaderPanel");
        var lockMargin = Number(header.Attribute("Margin"), part: 2);
        double Buttons(string part) => header.Elements().Single(e => PartOf(e) == part).Descendants().Count(e => e.Name.LocalName == "Button") * HeaderButtonWidth;
        var (tools, chrome) = (Buttons("Tools"), Buttons("Chrome"));

        var ships = ShipLoader.LoadAll(Path.Combine(AppContext.BaseDirectory, "ships")).Ships;
        List<(string Ship, double Name, double Compact)> measured = [];
        Sta.Run(() => measured = [.. ships.Select(ship =>
        {
            var widths = Widths(Texts(ship.Name, "", Compact(ship.TotalScu, ship.TotalScu), ""));
            return (ship.Name, widths.Name, widths.CompactCapacity);
        })]);
        var widest = measured.MaxBy(m => m.Name + m.Compact);
        var line1 = widest.Name + widest.Compact + chrome;
        var need = Math.Max(line1, tools) + padding + lockMargin;

        output.WriteLine(Inv($"Widest first line: {widest.Ship}, name {widest.Name:F1} + short capacity {widest.Compact:F1} + chrome {chrome} = {line1:F1} px."));
        output.WriteLine(Inv($"Second line {tools} px; with padding {padding} and lock margin {lockMargin}: window {need:F1} px."));
        foreach (var next in measured.OrderByDescending(m => m.Name + m.Compact).Take(5))
        {
            output.WriteLine(Inv($"  {next.Ship}: {next.Name + next.Compact:F1} px"));
        }

        minWidth.Should().BeGreaterThanOrEqualTo(need, "no ship name trims at the minimum width");
        minWidth.Should().BeLessThan(need + 10, "MinWidth is the need rounded up to the next 10 px");
    }

    // --- Real text, built like the header in OverlayWindow.xaml ---

    private sealed record HeaderTexts(HeaderPanel Panel, FrameworkElement Name);

    private static string? PartOf(XElement element) =>
        (string?)element.Attributes().SingleOrDefault(a => a.Name.LocalName == "HeaderPanel.Part");

    private static string Capacity(int totalScu, int waiting) =>
        Inv($"0/{totalScu:N0} · {totalScu:N0} free") + (waiting > 0 ? Inv($" · {waiting:N0} waiting") : "");

    private static string Compact(int totalScu, int waiting) =>
        Inv($"{totalScu:N0}/{totalScu:N0}") + (waiting > 0 ? Inv($" +{waiting:N0}") : "");

    /// <summary>
    /// The window's default font (the header sets none), the name bold with the selection's chevron (10 px glyph, 6 px
    /// margin), the capacities 8 px and the status 6 px from what stands left of them, five tools (no undo, redo) and gear
    /// and exit, as in the unlocked overlay.
    /// </summary>
    private static HeaderTexts Texts(string name, string capacity, string compact, string status)
    {
        var nameText = new TextBlock { Text = name, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        var chevron = new TextBlock { Text = "", FontFamily = new("Segoe MDL2 Assets"), FontSize = 10, Margin = new(6, 4, 0, 0) };
        DockPanel.SetDock(chevron, Dock.Right);
        var nameElement = new DockPanel { Children = { chevron, nameText } };
        HeaderPanel.SetPart(nameElement, HeaderPart.Name);
        var panel = new HeaderPanel
        {
            LineHeight = LineHeight,
            Children =
            {
                nameElement,
                Part(new TextBlock { Text = capacity, Margin = new(8, 0, 0, 0), TextWrapping = TextWrapping.NoWrap }, HeaderPart.Capacity),
                Part(new TextBlock { Text = compact, Margin = new(8, 0, 0, 0), TextWrapping = TextWrapping.NoWrap }, HeaderPart.CompactCapacity),
                Part(new TextBlock { Text = status, Margin = new(6, 0, 0, 0), TextWrapping = TextWrapping.NoWrap }, HeaderPart.Status),
                Child(5 * HeaderButtonWidth, HeaderPart.Tools, ButtonHeight),
                Child(2 * HeaderButtonWidth, HeaderPart.Chrome, ButtonHeight),
            },
        };
        return new(panel, nameElement);
    }

    private static TextBlock Part(TextBlock text, HeaderPart part)
    {
        HeaderPanel.SetPart(text, part);
        return text;
    }

    /// <summary>Each part's own width, measured without a limit.</summary>
    private static HeaderWidths Widths(HeaderTexts texts)
    {
        var parts = texts.Panel.Children.Cast<UIElement>().ToDictionary(HeaderPanel.GetPart, c =>
        {
            c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return c.DesiredSize.Width;
        });
        return new(parts[HeaderPart.Name], parts[HeaderPart.Status], parts[HeaderPart.Capacity], parts[HeaderPart.CompactCapacity],
            parts[HeaderPart.Tools], parts[HeaderPart.Chrome]);
    }

    private static XElement OverlayXaml()
    {
        using var stream = typeof(HeaderPanelTests).Assembly.GetManifestResourceStream("xaml/OverlayWindow.xaml")
            ?? throw new InvalidOperationException("Test resource 'xaml/OverlayWindow.xaml' not found.");
        return XDocument.Load(stream).Root!;
    }

    /// <summary>A number, or the <paramref name="part"/>th of a thickness "l,t,r,b".</summary>
    private static double Number(XAttribute? attribute, int part = 0)
    {
        var parts = attribute!.Value.Split(',');
        return double.Parse(parts[Math.Min(part, parts.Length - 1)], CultureInfo.InvariantCulture);
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);
}
