using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// A legend check box (B6, D69): ticked while all its crates are selected, empty while none is, part (null) in between
/// (<see cref="SelectionFocus.State"/>). Values: the line's or header's crates, the overlay's <see cref="CrateSelection"/>
/// and its <see cref="ShipScene"/>. The click runs <c>ToggleCratesCommand</c>, which changes the selection, and the
/// binding reads the result.
/// </summary>
// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.imultivalueconverter
public sealed class LegendCheckedConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [IReadOnlyList<Crate> crates, CrateSelection selection, ShipScene scene]
            ? SelectionFocus.State(selection.In(scene), crates)
            : false;

    // TwoWay with nothing written back: the click's own value would replace a one-way binding and end it, so the box
    // keeps following the selection. Source: https://learn.microsoft.com/dotnet/api/system.windows.data.binding.donothing
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        [.. targetTypes.Select(_ => Binding.DoNothing)];
}
