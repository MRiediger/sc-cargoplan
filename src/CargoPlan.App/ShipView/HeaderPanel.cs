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
    Buttons,
}

/// <summary>
/// The overlay's header (B15, D110): ship name at the left, the capacity long or short right of it, "All stops
/// delivered." and the button block at the right end. Short of room the status goes first, then the capacity's long form,
/// then the buttons move to a second line at the same x (<see cref="HeaderLayout"/>); the name trims only when it and the
/// short capacity alone are wider than the row. Locked, the button block is collapsed, measures 0 and takes no line.
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

    /// <summary>The buttons stand on a second line: <see cref="HeaderStage.ButtonsWrapped"/> with buttons to show.</summary>
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

        UIElement[] Of(HeaderPart part) => [.. children.Where(c => GetPart(c) == part)];
        double Width(HeaderPart part) => Of(part).Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max();
        double Height(HeaderPart part) => Of(part).Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max();

        HeaderWidths widths = new(Width(HeaderPart.Name), Width(HeaderPart.Status), Width(HeaderPart.Capacity), Width(HeaderPart.CompactCapacity), Width(HeaderPart.Buttons));
        Stage = HeaderLayout.Stage(widths, availableSize.Width);
        _wrapped = Stage == HeaderStage.ButtonsWrapped && widths.Buttons > 0;
        _nameWidth = HeaderLayout.NameWidth(widths, availableSize.Width);
        if (_nameWidth < widths.Name)
        {
            foreach (var name in Of(HeaderPart.Name))
            {
                name.Measure(new Size(_nameWidth, double.PositiveInfinity));
            }
        }

        HeaderPart[] firstLine = _wrapped
            ? [HeaderPart.Name, HeaderPart.Capacity, HeaderPart.CompactCapacity, HeaderPart.Status]
            : [HeaderPart.Name, HeaderPart.Capacity, HeaderPart.CompactCapacity, HeaderPart.Status, HeaderPart.Buttons];
        _lineHeight = Math.Max(LineHeight, firstLine.Max(Height));

        var capacity = Stage < HeaderStage.CompactCapacity ? widths.Capacity : widths.CompactCapacity;
        var status = Stage == HeaderStage.Full ? widths.Status : 0;
        var first = _nameWidth + capacity + status + (_wrapped ? 0 : widths.Buttons);
        return _wrapped
            ? new Size(Math.Max(first, widths.Buttons), _lineHeight + Height(HeaderPart.Buttons))
            : new Size(first, _lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var buttons = InternalChildren.Cast<UIElement>().Where(c => GetPart(c) == HeaderPart.Buttons).Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max();
        var buttonsX = Math.Max(0, finalSize.Width - buttons);
        var statusRight = _wrapped ? finalSize.Width : buttonsX;
        foreach (UIElement child in InternalChildren)
        {
            var size = child.DesiredSize;
            child.Arrange(GetPart(child) switch
            {
                HeaderPart.Name => new Rect(0, 0, _nameWidth, _lineHeight),
                HeaderPart.Capacity when Stage < HeaderStage.CompactCapacity => new Rect(_nameWidth, 0, size.Width, _lineHeight),
                HeaderPart.CompactCapacity when Stage >= HeaderStage.CompactCapacity => new Rect(_nameWidth, 0, size.Width, _lineHeight),
                HeaderPart.Status when Stage == HeaderStage.Full => new Rect(Math.Max(0, statusRight - size.Width), 0, size.Width, _lineHeight),
                HeaderPart.Buttons when _wrapped => new Rect(buttonsX, _lineHeight, size.Width, size.Height),
                HeaderPart.Buttons => new Rect(buttonsX, 0, size.Width, _lineHeight),
                _ => new Rect(0, 0, 0, 0),
            });
        }

        return finalSize;
    }
}
