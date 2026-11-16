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
}

/// <summary>
/// The header's text row (B15, D110): ship name at the left, the capacity long or short right of it, "All stops delivered."
/// at the right end. Short of room the status goes first, then the capacity's long form, and only then the name trims
/// (<see cref="HeaderLayout"/>). Every child is measured at its own width; a part that gave way is arranged into an empty
/// rectangle, which clips it, so it is neither drawn nor hit. The panel never touches a child's visibility, so what it
/// measures is always the text's own width and no measure undoes the one before (no layout cycle).
/// Source: https://learn.microsoft.com/dotnet/desktop/wpf/controls/panel (custom panels override MeasureOverride and
/// ArrangeOverride); the clip: FrameworkElement.ArrangeCore sets a layout clip when the slot is smaller than the desired size.
/// </summary>
public sealed class HeaderPanel : Panel
{
    public static readonly DependencyProperty PartProperty = DependencyProperty.RegisterAttached(
        "Part", typeof(HeaderPart), typeof(HeaderPanel), new FrameworkPropertyMetadata(HeaderPart.Name, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>The name's width from the last measure, for the arrange.</summary>
    private double _nameWidth;

    /// <summary>The stage of the last measure.</summary>
    public HeaderStage Stage { get; private set; }

    public static HeaderPart GetPart(UIElement element) => (HeaderPart)element.GetValue(PartProperty);

    public static void SetPart(UIElement element, HeaderPart value) => element.SetValue(PartProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement[] children = [.. InternalChildren.Cast<UIElement>()];
        foreach (var child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        double Width(HeaderPart part) => children.Where(c => GetPart(c) == part).Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max();
        HeaderWidths widths = new(Width(HeaderPart.Name), Width(HeaderPart.Status), Width(HeaderPart.Capacity), Width(HeaderPart.CompactCapacity));
        Stage = HeaderLayout.Stage(widths, availableSize.Width);
        _nameWidth = HeaderLayout.NameWidth(widths, availableSize.Width);
        if (Stage == HeaderStage.TrimmedName)
        {
            foreach (var name in children.Where(c => GetPart(c) == HeaderPart.Name))
            {
                name.Measure(new Size(_nameWidth, availableSize.Height));
            }
        }

        var capacity = Stage < HeaderStage.CompactCapacity ? widths.Capacity : widths.CompactCapacity;
        var status = Stage == HeaderStage.Full ? widths.Status : 0;
        return new Size(_nameWidth + capacity + status, children.Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            var width = child.DesiredSize.Width;
            child.Arrange(GetPart(child) switch
            {
                HeaderPart.Name => new Rect(0, 0, _nameWidth, finalSize.Height),
                HeaderPart.Capacity when Stage < HeaderStage.CompactCapacity => new Rect(_nameWidth, 0, width, finalSize.Height),
                HeaderPart.CompactCapacity when Stage >= HeaderStage.CompactCapacity => new Rect(_nameWidth, 0, width, finalSize.Height),
                HeaderPart.Status when Stage == HeaderStage.Full => new Rect(Math.Max(0, finalSize.Width - width), 0, width, finalSize.Height),
                _ => new Rect(0, 0, 0, 0),
            });
        }

        return finalSize;
    }
}
