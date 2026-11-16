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
/// The header's text row (B15, D110): ship name, capacity long or compact, and the status at the right end. Short of room
/// the status goes first, then the capacity's long form; the name trims last. Fixed-width borders keep the slots
/// independent of a font; the TextBlock tests and the ship data test measure real text.
/// </summary>
public class HeaderPanelTests(ITestOutputHelper output)
{
    private static Border Child(double width, HeaderPart part)
    {
        var child = new Border { Width = width, Height = 10 };
        HeaderPanel.SetPart(child, part);
        return child;
    }

    /// <summary>Name 80, capacity 140, compact 40, status 100: 320 for all of it, as in <see cref="HeaderLayoutTests"/>.</summary>
    private static (HeaderPanel Panel, Border Name, Border Capacity, Border Compact, Border Status) Header()
    {
        var (name, capacity, compact, status) = (Child(80, HeaderPart.Name), Child(140, HeaderPart.Capacity),
            Child(40, HeaderPart.CompactCapacity), Child(100, HeaderPart.Status));
        var panel = new HeaderPanel { Children = { name, capacity, compact, status } };
        return (panel, name, capacity, compact, status);
    }

    /// <summary>Measured and arranged at <paramref name="width"/>, as the header's DockPanel does with its last child.</summary>
    private static void Layout(HeaderPanel panel, double width)
    {
        panel.Measure(new Size(width, 30));
        panel.Arrange(new Rect(0, 0, width, 30));
    }

    private static Rect Slot(UIElement child) => LayoutInformation.GetLayoutSlot((FrameworkElement)child);

    [Fact]
    public void With_room_name_capacity_and_status_stand_and_the_status_at_the_right_end()
    {
        Sta.Run(() =>
        {
            var (panel, name, capacity, compact, status) = Header();

            Layout(panel, 500);

            panel.Stage.Should().Be(HeaderStage.Full);
            (Slot(name).X, Slot(name).Width).Should().Be((0, 80));
            (Slot(capacity).X, Slot(capacity).Width).Should().Be((80, 140));
            Slot(compact).Width.Should().Be(0);
            (Slot(status).X, Slot(status).Width).Should().Be((400, 100));
        });
    }

    [Fact]
    public void One_short_of_everything_the_status_goes()
    {
        Sta.Run(() =>
        {
            var (panel, name, capacity, _, status) = Header();

            Layout(panel, 319);

            panel.Stage.Should().Be(HeaderStage.NoStatus);
            (Slot(name).Width, Slot(capacity).Width, Slot(status).Width).Should().Be((80, 140, 0));
        });
    }

    [Fact]
    public void One_short_of_name_and_capacity_the_capacity_turns_compact()
    {
        Sta.Run(() =>
        {
            var (panel, name, capacity, compact, status) = Header();

            Layout(panel, 219);

            panel.Stage.Should().Be(HeaderStage.CompactCapacity);
            (Slot(name).Width, Slot(capacity).Width, Slot(status).Width).Should().Be((80, 0, 0));
            (Slot(compact).X, Slot(compact).Width).Should().Be((80, 40));
        });
    }

    [Fact]
    public void Only_then_the_name_gives_way_and_the_compact_capacity_stays()
    {
        Sta.Run(() =>
        {
            var (panel, name, _, compact, _) = Header();

            Layout(panel, 100);

            panel.Stage.Should().Be(HeaderStage.TrimmedName);
            (Slot(name).Width, Slot(compact).X, Slot(compact).Width).Should().Be((60, 60, 40));
        });
    }

    [Fact]
    public void A_part_that_gave_way_is_clipped_to_nothing()
    {
        // Neither drawn nor hit: its tool tip can't come up over the name.
        Sta.Run(() =>
        {
            var (panel, _, capacity, _, status) = Header();

            Layout(panel, 219);

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
            var (panel, _, _, _, status) = Header();
            status.Visibility = Visibility.Collapsed;
            Layout(panel, 300);
            panel.Stage.Should().Be(HeaderStage.Full);

            status.Visibility = Visibility.Visible;
            Layout(panel, 300);

            panel.Stage.Should().Be(HeaderStage.NoStatus);
        });
    }

    [Theory]
    [InlineData("ARGO RAFT", 192)]
    [InlineData("MISC Hull C", 4608)]
    public void The_ship_name_stands_in_full_in_every_stage_but_the_last(string shipName, int totalScu)
    {
        Sta.Run(() =>
        {
            var texts = Texts(shipName, Capacity(totalScu, waiting: 16), Compact(totalScu, waiting: 16), "All stops delivered.");
            var widths = Widths(texts);
            double[] limits =
            [
                widths.Name + widths.Capacity + widths.Status,
                widths.Name + widths.Capacity,
                widths.Name + widths.CompactCapacity,
            ];

            foreach (var (available, stage) in limits.Zip([HeaderStage.Full, HeaderStage.NoStatus, HeaderStage.CompactCapacity]))
            {
                Layout(texts.Panel, Math.Ceiling(available));

                texts.Panel.Stage.Should().Be(stage);
                Slot(texts.Name).Width.Should().BeGreaterThanOrEqualTo(widths.Name, $"{shipName} untrimmed at {stage}");
            }

            Layout(texts.Panel, Math.Floor(widths.Name + widths.CompactCapacity) - 1);
            texts.Panel.Stage.Should().Be(HeaderStage.TrimmedName);
        });
    }

    [Fact]
    public void At_the_default_width_every_ship_name_stands_in_full_while_locked()
    {
        // Data test over ships/ (A10): which stage the longest names reach. Unlocked, the header buttons take all of the
        // minimum width (7 × 30 = 210 of 190 px), so there every ship's name trims by construction (K1 = A, MinWidth stays).
        // Locked they collapse (D110) and the row has the window's width without padding and the lock window's margin.
        var overlay = OverlayXaml();
        var minWidth = Number(overlay.Attribute("MinWidth"));
        var defaultWidth = Number(overlay.Attribute("Width"));
        var frame = overlay.Elements().Single(e => e.Name.LocalName == "Border");
        var padding = 2 * Number(frame.Attribute("Padding"));
        var header = frame.Descendants().Single(e => e.Name.LocalName == "DockPanel" && (string?)e.Attribute("Height") == "30");
        var lockMargin = Number(header.Attribute("Margin"), part: 2);
        var buttons = header.Elements().Count(e => e.Name.LocalName == "Button") * HeaderButtonWidth;
        double RowWidth(double window) => window - padding - lockMargin;

        RowWidth(minWidth).Should().BeLessThan(buttons, "unlocked at the minimum width nothing is left for name and capacity");

        var ships = ShipLoader.LoadAll(Path.Combine(AppContext.BaseDirectory, "ships")).Ships;
        List<(string Name, double Need)> needs = [];
        Sta.Run(() => needs = [.. ships.Select(ship =>
        {
            var widths = Widths(Texts(ship.Name, Capacity(ship.TotalScu, 0), Compact(ship.TotalScu, 0), "All stops delivered.", locked: true));
            return (ship.Name, Need: widths.Name + widths.CompactCapacity);
        }).OrderByDescending(n => n.Need)]);
        var widest = needs[0];
        var windowForWidest = Math.Ceiling(widest.Need + padding + lockMargin);
        var trimmedAtMinimum = needs.Count(n => n.Need > RowWidth(minWidth));

        output.WriteLine(Inv($"Row width at {minWidth} px: {RowWidth(minWidth)} px locked, {RowWidth(minWidth) - buttons} px unlocked."));
        output.WriteLine(Inv($"Widest locked need: {widest.Name}, name + compact capacity {widest.Need:F1} px → window {windowForWidest} px."));
        output.WriteLine(Inv($"Locked at {minWidth} px, {trimmedAtMinimum} of {needs.Count} ship names trim."));
        widest.Need.Should().BeLessThanOrEqualTo(RowWidth(defaultWidth), "at the default width every name stands in full while locked");
    }

    // --- Real text, built like the header in OverlayWindow.xaml ---

    /// <summary>The header buttons' width (HeaderButtonStyle).</summary>
    private const double HeaderButtonWidth = 30;

    private sealed record HeaderTexts(HeaderPanel Panel, FrameworkElement Name);

    private static string Capacity(int totalScu, int waiting) =>
        Inv($"0/{totalScu:N0} · {totalScu:N0} free") + (waiting > 0 ? Inv($" · {waiting:N0} waiting") : "");

    private static string Compact(int totalScu, int waiting) =>
        Inv($"{totalScu:N0}/{totalScu:N0}") + (waiting > 0 ? Inv($" +{waiting:N0}") : "");

    /// <summary>
    /// The window's default font (the header sets none), the name bold, the capacities 8 px and the status 6 px from what
    /// stands left of them, as in the overlay. Unlocked the name carries the selection's chevron, 10 px glyph with 6 px margin.
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
            Children =
            {
                nameElement,
                Part(new TextBlock { Text = capacity, Margin = new(8, 0, 0, 0), TextWrapping = TextWrapping.NoWrap }, HeaderPart.Capacity),
                Part(new TextBlock { Text = compact, Margin = new(8, 0, 0, 0), TextWrapping = TextWrapping.NoWrap }, HeaderPart.CompactCapacity),
                Part(new TextBlock { Text = status, Margin = new(6, 0, 0, 0), TextWrapping = TextWrapping.NoWrap }, HeaderPart.Status),
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
            c.Measure(new Size(double.PositiveInfinity, 30));
            return c.DesiredSize.Width;
        });
        return new(parts[HeaderPart.Name], parts[HeaderPart.Status], parts[HeaderPart.Capacity], parts[HeaderPart.CompactCapacity]);
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
