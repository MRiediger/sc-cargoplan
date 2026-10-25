using System.Globalization;
using System.Windows.Data;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The legend's line when colours repeat: the scene's stop count and the chosen palette (K4.2), so a palette with fewer
/// colours says so at once without a new scene. Empty when nothing repeats; the line's style collapses an empty text.
/// </summary>
// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.imultivalueconverter
public sealed class PaletteNoticeConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [int stops, StopPalette palette] ? palette.RepeatNotice(stops) ?? "" : "";

    // One-way only: a notice never writes back.
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
