using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The legend's pickup check box (T7.4d): ticked while every crate of its header is selected. Values: the
/// <see cref="LegendEntry"/>, the overlay's <see cref="CrateSelection"/> and its <see cref="ShipScene"/>; the rule is
/// <see cref="SelectionFocus.HasAll"/>. The click runs <c>TogglePickupCommand</c>, which changes the selection, and the
/// binding reads the result.
/// </summary>
// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.imultivalueconverter
public sealed class PickupCheckedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [LegendEntry { PickupCrates: { } crates }, CrateSelection selection, ShipScene scene]
            && SelectionFocus.HasAll(selection.In(scene), crates);

    // TwoWay with nothing written back: the click's own value would replace a one-way binding and end it, so the box
    // keeps following the selection. Source: https://learn.microsoft.com/dotnet/api/system.windows.data.binding.donothing
    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        [.. targetTypes.Select(_ => Binding.DoNothing)];
}
