using System.Globalization;
using System.Windows;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class LegendMaxHeightConverterTests
{
    private static readonly LegendMaxHeightConverter Converter = new();

    private static object Convert(params object?[] values) =>
        Converter.Convert(values, typeof(double), null, CultureInfo.InvariantCulture);

    [Fact]
    public void The_stop_lines_get_their_share_of_the_overlay_height()
    {
        Convert(400.0, 0.5).Should().Be(200.0);
        Convert(460.0, 0.25).Should().Be(115.0);
    }

    [Fact]
    public void Without_a_height_there_is_no_limit()
    {
        // Before the first layout pass or while a binding is unset: MaxHeight's own default, not 0, which would hide the lines.
        Convert(DependencyProperty.UnsetValue, 0.5).Should().Be(double.PositiveInfinity);
        Convert(400.0, DependencyProperty.UnsetValue).Should().Be(double.PositiveInfinity);
    }
}
