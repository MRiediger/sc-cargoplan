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
/// The header (B15, D110): ship name, capacity long or compact, the status and the buttons at the right end. Short of room
/// the status goes first, then the capacity's long form, then the buttons move to a second line; the name trims only when
/// it and the short capacity are wider than the row. Fixed-width borders keep the slots independent of a font; the
/// TextBlock tests and the ship data test measure real text.
/// </summary>
public class HeaderPanelTests(ITestOutputHelper output)
{
    private static Border Child(double width, HeaderPart part, double height = 10)
    {
        var child = new Border { Width = width, Height = height };
        HeaderPanel.SetPart(child, part);
        return child;
    }

    /// <summary>Name 80, capacity 140, compact 40, status 100, buttons 200 × 26: 520 for all of it, as in <see cref="HeaderLayoutTests"/>.</summary>
    private static (HeaderPanel Panel, Border Name, Border Capacity, Border Compact, Border Status, Border Buttons) Header()
    {
        var (name, capacity, compact, status, buttons) = (Child(80, HeaderPart.Name), Child(140, HeaderPart.Capacity),
            Child(40, HeaderPart.CompactCapacity), Child(100, HeaderPart.Status), Child(200, HeaderPart.Buttons, ButtonHeight));
        var panel = new HeaderPanel { LineHeight = LineHeight, Children = { name, capacity, compact, status, buttons } };
        return (panel, name, capacity, compact, status, buttons);
    }

    private const double LineHeight = 30;

    private const double ButtonHeight = 26;

    /// <summary>Measured and arranged at <paramref name="width"/> and the height it asks for, as the overlay's DockPanel does.</summary>
    private static void Layout(HeaderPanel panel, double width)
    {
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
    }

    private static Rect Slot(UIElement child) => LayoutInformation.GetLayoutSlot((FrameworkElement)child);

    [Fact]
    public void With_room_everything_stands_on_one_line_status_and_buttons_at_the_right_end()
    {
        Sta.Run(() =>
        {
            var (panel, name, capacity, compact, status, buttons) = Header();

            Layout(panel, 600);

            panel.Stage.Should().Be(HeaderStage.Full);
            panel.DesiredSize.Height.Should().Be(LineHeight);
            (Slot(name).X, Slot(name).Width).Should().Be((0, 80));
            (Slot(capacity).X, Slot(capacity).Width).Should().Be((80, 140));
            Slot(compact).Width.Should().Be(0);
            (Slot(status).X, Slot(status).Width).Should().Be((300, 100));
            (Slot(buttons).X, Slot(buttons).Y, Slot(buttons).Width).Should().Be((400, 0, 200));
        });
    }

    [Fact]
    public void One_short_of_everything_the_status_goes()
    {
        Sta.Run(() =>
        {
            var (panel, name, capacity, _, status, buttons) = Header();

            Layout(panel, 519);

            panel.Stage.Should().Be(HeaderStage.NoStatus);
            (Slot(name).Width, Slot(capacity).Width, Slot(status).Width).Should().Be((80, 140, 0));
            (Slot(buttons).X, Slot(buttons).Y).Should().Be((319, 0));
        });
    }

    [Fact]
    public void One_short_of_name_capacity_and_buttons_the_capacity_turns_compact()
    {
        Sta.Run(() =>
        {
            var (panel, name, capacity, compact, status, buttons) = Header();

            Layout(panel, 419);

            panel.Stage.Should().Be(HeaderStage.CompactCapacity);
            panel.DesiredSize.Height.Should().Be(LineHeight);
            (Slot(name).Width, Slot(capacity).Width, Slot(status).Width).Should().Be((80, 0, 0));
            (Slot(compact).X, Slot(compact).Width).Should().Be((80, 40));
            (Slot(buttons).X, Slot(buttons).Y).Should().Be((219, 0));
        });
    }

    [Fact]
    public void Then_the_buttons_go_to_a_second_line_at_the_same_right_end()
    {
        // R3: same x as on the first line, so the padlock's neighbours never jump sideways.
        Sta.Run(() =>
        {
            var (panel, name, _, compact, _, buttons) = Header();

            Layout(panel, 319);

            panel.Stage.Should().Be(HeaderStage.ButtonsWrapped);
            panel.DesiredSize.Height.Should().Be(LineHeight + ButtonHeight);
            (Slot(name).Width, Slot(compact).X, Slot(compact).Width).Should().Be((80, 80, 40));
            Slot(buttons).Should().Be(new Rect(119, LineHeight, 200, ButtonHeight));
        });
    }

    [Fact]
    public void Only_when_name_and_compact_capacity_are_wider_than_the_row_the_name_trims()
    {
        Sta.Run(() =>
        {
            var (panel, name, _, compact, _, _) = Header();

            Layout(panel, 100);

            panel.Stage.Should().Be(HeaderStage.ButtonsWrapped);
            (Slot(name).Width, Slot(compact).X, Slot(compact).Width).Should().Be((60, 60, 40));
        });
    }

    [Fact]
    public void Locked_the_collapsed_buttons_take_no_line()
    {
        // Locked, the button block measures 0 (D110): the second line goes by itself.
        Sta.Run(() =>
        {
            var (panel, name, _, _, _, buttons) = Header();
            buttons.Visibility = Visibility.Collapsed;

            Layout(panel, 150);

            panel.DesiredSize.Height.Should().Be(LineHeight);
            Slot(name).Width.Should().Be(80);
        });
    }

    [Fact]
    public void A_part_that_gave_way_is_clipped_to_nothing()
    {
        // Neither drawn nor hit: its tool tip can't come up over the name.
        Sta.Run(() =>
        {
            var (panel, _, capacity, _, status, _) = Header();

            Layout(panel, 419);

            foreach (var gone in new[] { capacity, status })
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
            var (panel, _, _, _, status, _) = Header();
            status.Visibility = Visibility.Collapsed;
            Layout(panel, 500);
            panel.Stage.Should().Be(HeaderStage.Full);

            status.Visibility = Visibility.Visible;
            Layout(panel, 500);

            panel.Stage.Should().Be(HeaderStage.NoStatus);
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
            var widths = Widths(texts);
            double[] limits =
            [
                widths.Name + widths.Capacity + widths.Status + widths.Buttons,
                widths.Name + widths.Capacity + widths.Buttons,
                widths.Name + widths.CompactCapacity + widths.Buttons,
                widths.Name + widths.CompactCapacity,
            ];
            HeaderStage[] stages = [HeaderStage.Full, HeaderStage.NoStatus, HeaderStage.CompactCapacity, HeaderStage.ButtonsWrapped];

            foreach (var (available, stage) in limits.Zip(stages))
            {
                Layout(texts.Panel, Math.Ceiling(available));

                texts.Panel.Stage.Should().Be(stage);
                Slot(texts.Name).Width.Should().BeGreaterThanOrEqualTo(widths.Name, $"{shipName} untrimmed at {stage}");
            }
        });
    }

    [Fact]
    public void At_the_minimum_width_every_ship_name_stands_in_full_and_the_buttons_fit_their_line()
    {
        // Data test over ships/ (A10), R2 of the approval: the longest name and the widest short capacity "N/N +N" (N the
        // ship's own capacity, same digits and comma), maximised over all ships even when they aren't the same ship. More
        // waiting cargo than capacity may cost a digit (D110). The second line holds every button, undo and redo included.
        var overlay = OverlayXaml();
        var minWidth = Number(overlay.Attribute("MinWidth"));
        var frame = overlay.Elements().Single(e => e.Name.LocalName == "Border");
        var padding = 2 * Number(frame.Attribute("Padding"));
        var header = frame.Descendants().Single(e => e.Name.LocalName == "HeaderPanel");
        var lockMargin = Number(header.Attribute("Margin"), part: 2);
        var buttons = header.Descendants().Count(e => e.Name.LocalName == "Button") * HeaderButtonWidth;

        var ships = ShipLoader.LoadAll(Path.Combine(AppContext.BaseDirectory, "ships")).Ships;
        // Unlocked the name carries the selection's chevron: the wider of the two, so the rule holds locked and unlocked.
        List<(string Ship, double Name, double Compact, double CompactWithoutWaiting)> measured = [];
        Sta.Run(() => measured = [.. ships.Select(ship =>
        {
            var widths = Widths(Texts(ship.Name, "", Compact(ship.TotalScu, ship.TotalScu), ""));
            var withoutWaiting = Widths(Texts(ship.Name, "", Compact(ship.TotalScu, 0), "")).CompactCapacity;
            return (ship.Name, widths.Name, widths.CompactCapacity, withoutWaiting);
        })]);
        var longest = measured.MaxBy(m => m.Name);
        var widest = measured.MaxBy(m => m.Compact);
        var text = longest.Name + widest.Compact;
        var need = Math.Max(text, buttons) + padding + lockMargin;
        var needWithoutWaiting = Math.Max(longest.Name + measured.Max(m => m.CompactWithoutWaiting), buttons) + padding + lockMargin;

        output.WriteLine(Inv($"Longest name with chevron: {longest.Ship}, {longest.Name:F1} px; widest short capacity: {widest.Ship}, {widest.Compact:F1} px."));
        output.WriteLine(Inv($"Line 1 needs {text:F1} px, line 2 {buttons} px; with padding {padding} and lock margin {lockMargin}: window {need:F1} px."));
        output.WriteLine(Inv($"For comparison, without \"+N\": window {needWithoutWaiting:F1} px."));
        minWidth.Should().BeGreaterThanOrEqualTo(need, "no ship name trims at the minimum width");
        minWidth.Should().BeLessThan(need + 10, "MinWidth is the need rounded up to the next 10 px");
    }

    // --- Real text, built like the header in OverlayWindow.xaml ---

    /// <summary>The header buttons' width (HeaderButtonStyle) and the unlocked block of seven without undo and redo.</summary>
    private const double HeaderButtonWidth = 30;

    private sealed record HeaderTexts(HeaderPanel Panel, FrameworkElement Name);

    private static string Capacity(int totalScu, int waiting) =>
        Inv($"0/{totalScu:N0} · {totalScu:N0} free") + (waiting > 0 ? Inv($" · {waiting:N0} waiting") : "");

    private static string Compact(int totalScu, int waiting) =>
        Inv($"{totalScu:N0}/{totalScu:N0}") + (waiting > 0 ? Inv($" +{waiting:N0}") : "");

    /// <summary>
    /// The window's default font (the header sets none), the name bold, the capacities 8 px and the status 6 px from what
    /// stands left of them, as in the overlay. Unlocked the name carries the selection's chevron, 10 px glyph with 6 px
    /// margin, and seven buttons follow.
    /// </summary>
    private static HeaderTexts Texts(string name, string capacity, string compact, string status, bool locked = false)
    {
        var nameText = new TextBlock { Text = name, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        FrameworkElement nameElement = locked
            ? nameText
            : new DockPanel { Children = { Docked(new TextBlock { Text = "", FontFamily = new("Segoe MDL2 Assets"), FontSize = 10, Margin = new(6, 4, 0, 0) }), nameText } };
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
                Child(locked ? 0 : 7 * HeaderButtonWidth, HeaderPart.Buttons, ButtonHeight),
            },
        };
        return new(panel, nameElement);
    }

    private static TextBlock Docked(TextBlock text)
    {
        DockPanel.SetDock(text, Dock.Right);
        return text;
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
        return new(parts[HeaderPart.Name], parts[HeaderPart.Status], parts[HeaderPart.Capacity], parts[HeaderPart.CompactCapacity], parts[HeaderPart.Buttons]);
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
