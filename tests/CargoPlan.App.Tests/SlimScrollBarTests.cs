using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>
/// The overlay's slim scroll bar (K7, T13.2) takes a press across its whole width (K of the T13.3a approval): with the
/// track inset by 2 px, only the thumb's middle 4 of 8 px took it, and a press on either edge moved the window (DragMove).
/// The styles come from the overlay's XAML, the brush from App.xaml's value.
/// </summary>
public class SlimScrollBarTests
{
    private const double Thickness = 8;
    private const double Length = 200;

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static ResourceDictionary Styles()
    {
        using var stream = typeof(SlimScrollBarTests).Assembly.GetManifestResourceStream("xaml/OverlayWindow.xaml")
            ?? throw new InvalidOperationException("Test resource 'xaml/OverlayWindow.xaml' not found.");
        var root = XDocument.Load(stream).Root!;
        XElement Keyed(string key) => root.Descendants().Single(e => (string?)e.Attribute(X + "Key") == key);

        var dictionary = new XElement(
            Presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", X),
            new XElement(Presentation + "SolidColorBrush", new XAttribute(X + "Key", "HitTestBackgroundBrush"), new XAttribute("Color", "#01000000")),
            Keyed("SlimScrollPageButtonStyle"),
            Keyed("SlimScrollThumbStyle"),
            Keyed("SlimScrollBarStyle"));
        return (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
    }

    private static ScrollBar Bar(Orientation orientation)
    {
        var styles = Styles();
        var bar = new ScrollBar
        {
            Resources = styles,
            Orientation = orientation,
            Minimum = 0,
            Maximum = 100,
            ViewportSize = 100,
            Value = 0,
            Foreground = Brushes.White,
        };
        bar.Style = (Style)styles["SlimScrollBarStyle"];
        var size = orientation == Orientation.Vertical ? new Size(Thickness, Length) : new Size(Length, Thickness);
        bar.Measure(size);
        bar.Arrange(new Rect(size));
        bar.UpdateLayout();
        return bar;
    }

    // The control a press at this point reaches: the thumb, a page button or nothing (the window's DragMove). The geometry's
    // hit test, which keeps IsHitTestVisible: InputHitTest needs a shown window (IsVisible).
    private static string Hit(ScrollBar bar, Point point)
    {
        for (var element = VisualTreeHelper.HitTest(bar, point)?.VisualHit; element is not null && element != bar; element = VisualTreeHelper.GetParent(element))
        {
            switch (element)
            {
                case Thumb:
                    return "thumb";
                case RepeatButton:
                    return "page";
            }
        }

        return "nothing";
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void Every_column_across_the_bar_takes_a_press(Orientation orientation) => Sta.Run(() =>
    {
        var bar = Bar(orientation);
        // The view is half the extent: the thumb takes the first half of the track, a page button the rest.
        Point At(double across, double along) => orientation == Orientation.Vertical ? new(across, along) : new(along, across);

        var columns = Enumerable.Range(0, (int)Thickness).Select(i => i + 0.5).ToList();
        columns.Select(c => Hit(bar, At(c, Length / 4))).Should().Equal(columns.Select(_ => "thumb"));
        columns.Select(c => Hit(bar, At(c, Length * 3 / 4))).Should().Equal(columns.Select(_ => "page"));
        Hit(bar, At(Thickness / 2, 0.5)).Should().Be("thumb", "the track runs to the bar's end");
    });

    [Fact]
    public void The_thumb_keeps_its_2_px_inset() => Sta.Run(() =>
    {
        var bar = Bar(Orientation.Vertical);
        var thumb = (Thumb)((Track)bar.Template.FindName("PART_Track", bar)).Thumb;
        var grip = (Border)thumb.Template.FindName("Grip", thumb);

        grip.Margin.Should().Be(new Thickness(2));
        grip.ActualWidth.Should().Be(Thickness - 4);
    });
}
