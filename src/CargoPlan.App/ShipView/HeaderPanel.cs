using System.Windows;
using System.Windows.Controls;

namespace CargoPlan.App.ShipView;

/// <summary>The part a child plays in the <see cref="HeaderPanel"/>.</summary>
public enum HeaderPart
{
    Name,
    Capacity,
    CompactCapacity,
    Status,

    /// <summary>The tool buttons (split … reset view): the block that goes to a second line.</summary>
    Tools,

    /// <summary>Gear and exit: always at the right end of the first line, next to the padlock.</summary>
    Chrome,
}

/// <summary>
/// The overlay's header (B15, D110): ship name at the left, the capacity long or short right of it, then "All stops
/// delivered.", the tools and gear and exit at the right end. Short of room the status goes first, then the capacity's
/// long form, then the tools move to a second line, left-aligned with the right free to move the window
/// (<see cref="HeaderLayout"/>); gear and exit stay. The name trims only when it, the short capacity and the chrome are
/// wider than the row. Locked, tools and chrome are collapsed, measure 0 and take no line.
/// Every child is measured at its own width; a part that gave way is arranged into an empty rectangle, which clips it, so
/// it is neither drawn nor hit. The panel never touches a child's visibility, so what it measures is always the child's
/// own width and no measure undoes the one before (no layout cycle).
/// Source: https://learn.microsoft.com/dotnet/desktop/wpf/controls/panel (custom panels override MeasureOverride and
/// ArrangeOverride); the clip: FrameworkElement.ArrangeCore sets a layout clip when the slot is smaller than the desired size.
/// </summary>
public sealed class HeaderPanel : Panel
{
    public static readonly DependencyProperty PartProperty = DependencyProperty.RegisterAttached(
        "Part", typeof(HeaderPart), typeof(HeaderPanel), new FrameworkPropertyMetadata(HeaderPart.Name, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>The first line's height: as high as the header was before it could wrap (D16), clear of the padlock.</summary>
    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(HeaderPanel), new FrameworkPropertyMetadata(30.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The name's width from the last measure, for the arrange.</summary>
    private double _nameWidth;

    /// <summary>The first line's height from the last measure.</summary>
    private double _lineHeight;

    /// <summary>The tools stand on a second line: <see cref="HeaderStage.ToolsWrapped"/> with tools to show.</summary>
    private bool _wrapped;

    /// <summary>The stage of the last measure.</summary>
    public HeaderStage Stage { get; private set; }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public static HeaderPart GetPart(UIElement element) => (HeaderPart)element.GetValue(PartProperty);

    public static void SetPart(UIElement element, HeaderPart value) => element.SetValue(PartProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement[] children = [.. InternalChildren.Cast<UIElement>()];
        foreach (var child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        HeaderWidths w = new(WidthOf(children, HeaderPart.Name), WidthOf(children, HeaderPart.Status), WidthOf(children, HeaderPart.Capacity),
            WidthOf(children, HeaderPart.CompactCapacity), WidthOf(children, HeaderPart.Tools), WidthOf(children, HeaderPart.Chrome));
        Stage = HeaderLayout.Stage(w, availableSize.Width);
        _wrapped = Stage == HeaderStage.ToolsWrapped && w.Tools > 0;
        _nameWidth = HeaderLayout.NameWidth(w, availableSize.Width);
        if (_nameWidth < w.Name)
        {
            foreach (var name in children.Where(c => GetPart(c) == HeaderPart.Name))
            {
                name.Measure(new Size(_nameWidth, double.PositiveInfinity));
            }
        }

        var firstLine = children.Where(c => !(_wrapped && GetPart(c) == HeaderPart.Tools));
        _lineHeight = Math.Max(LineHeight, firstLine.Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max());

        var capacity = Stage < HeaderStage.CompactCapacity ? w.Capacity : w.CompactCapacity;
        var status = Stage == HeaderStage.Full ? w.Status : 0;
        var first = _nameWidth + capacity + status + w.Chrome + (_wrapped ? 0 : w.Tools);
        return _wrapped
            ? new Size(Math.Max(first, w.Tools), _lineHeight + HeightOf(children, HeaderPart.Tools))
            : new Size(first, _lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElement[] children = [.. InternalChildren.Cast<UIElement>()];
        var chromeX = Math.Max(0, finalSize.Width - WidthOf(children, HeaderPart.Chrome));
        var toolsX = _wrapped ? 0 : Math.Max(0, chromeX - WidthOf(children, HeaderPart.Tools));
        foreach (var child in children)
        {
            var size = child.DesiredSize;
            child.Arrange(GetPart(child) switch
            {
                HeaderPart.Name => new Rect(0, 0, _nameWidth, _lineHeight),
                HeaderPart.Capacity when Stage < HeaderStage.CompactCapacity => new Rect(_nameWidth, 0, size.Width, _lineHeight),
                HeaderPart.CompactCapacity when Stage >= HeaderStage.CompactCapacity => new Rect(_nameWidth, 0, size.Width, _lineHeight),
                HeaderPart.Status when Stage == HeaderStage.Full => new Rect(Math.Max(0, toolsX - size.Width), 0, size.Width, _lineHeight),
                HeaderPart.Tools when _wrapped => new Rect(0, _lineHeight, size.Width, size.Height),
                HeaderPart.Tools => new Rect(toolsX, 0, size.Width, _lineHeight),
                HeaderPart.Chrome => new Rect(chromeX, 0, size.Width, _lineHeight),
                _ => new Rect(0, 0, 0, 0),
            });
        }

        return finalSize;
    }

    private static double WidthOf(UIElement[] children, HeaderPart part) =>
        children.Where(c => GetPart(c) == part).Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max();

    private static double HeightOf(UIElement[] children, HeaderPart part) =>
        children.Where(c => GetPart(c) == part).Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max();
}
