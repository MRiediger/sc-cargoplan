using System.Windows;
using System.Windows.Controls;

namespace CargoPlan.App.ShipView;

/// <summary>
/// A legend line or header side by side (K6.1, D69 addendum): every child at its own width while the row fits; short of
/// room, children of rank <see cref="LegendSegment.TrimFirst"/> give up width first, in proportion to theirs, then
/// <see cref="LegendSegment.TrimSecond"/>; <see cref="LegendSegment.Fixed"/> never. A text block given less ends in its
/// own ellipsis. The rank sits on the item container (<c>ItemContainerStyle</c>).
/// Source: https://learn.microsoft.com/dotnet/desktop/wpf/controls/panel (custom panels override MeasureOverride and
/// ArrangeOverride; a horizontal StackPanel is constrained to content, so it never shortens anything).
/// </summary>
public sealed class LegendRowPanel : Panel
{
    public static readonly DependencyProperty RankProperty = DependencyProperty.RegisterAttached(
        "Rank", typeof(int), typeof(LegendRowPanel), new FrameworkPropertyMetadata(LegendSegment.Fixed, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    private static readonly int[] TrimOrder = [LegendSegment.TrimFirst, LegendSegment.TrimSecond];

    /// <summary>Each child's width from the last measure, for the arrange.</summary>
    private double[] widths = [];

    public static int GetRank(UIElement element) => (int)element.GetValue(RankProperty);

    public static void SetRank(UIElement element, int value) => element.SetValue(RankProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement[] children = [.. InternalChildren.Cast<UIElement>()];
        foreach (var child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        widths = [.. children.Select(c => c.DesiredSize.Width)];
        var excess = widths.Sum() - availableSize.Width;
        foreach (var rank in TrimOrder)
        {
            int[] own = [.. Enumerable.Range(0, children.Length).Where(i => GetRank(children[i]) == rank)];
            var total = own.Sum(i => widths[i]);
            if (excess <= 0 || total <= 0)
            {
                continue;
            }

            var cut = Math.Min(excess, total);
            foreach (var i in own)
            {
                widths[i] -= cut * widths[i] / total;
                children[i].Measure(new Size(widths[i], availableSize.Height));
            }

            excess -= cut;
        }

        return new Size(widths.Sum(), children.Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var width = i < widths.Length ? widths[i] : InternalChildren[i].DesiredSize.Width;
            InternalChildren[i].Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }
}
