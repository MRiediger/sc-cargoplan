using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace CargoPlan.App.Tests;

/// <summary>
/// The custom route's bar under the map (gate finding 7b): its highest height binds, from inside the Plan tab's data
/// template, to the window's OverlayContent by ElementName, as the legend's lines do, and to the tab's own share
/// (routes.mapLegendHeight, its slider in the settings window).
/// Proves that the binding resolves across the template (A3: not from memory); OverlayRouteXamlTests checks the XAML has it.
/// </summary>
public sealed class CustomBarHeightTests
{
    private const string Template = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:sv="clr-namespace:CargoPlan.App.ShipView;assembly=CargoPlan.App">
          <ScrollViewer>
            <ScrollViewer.MaxHeight>
              <MultiBinding Mode="OneWay">
                <MultiBinding.Converter>
                  <sv:LegendMaxHeightConverter />
                </MultiBinding.Converter>
                <Binding ElementName="OverlayContent" Path="ActualHeight" Mode="OneWay" />
                <Binding Path="MapLegendHeight" Mode="OneWay" />
              </MultiBinding>
            </ScrollViewer.MaxHeight>
          </ScrollViewer>
        </DataTemplate>
        """;

    [Fact]
    public void The_bars_highest_height_is_its_own_share_of_the_overlay_from_inside_the_plan_tabs_template() => Sta.Run(() =>
    {
        var window = new Window();
        NameScope.SetNameScope(window, new NameScope());
        var content = new DockPanel();
        window.RegisterName("OverlayContent", content);
        var plan = new ContentControl { Content = new Share(0.4), ContentTemplate = (DataTemplate)XamlReader.Parse(Template) };
        content.Children.Add(plan);
        window.Content = content;

        content.Measure(new Size(400, 600));
        content.Arrange(new Rect(0, 0, 400, 600));
        content.UpdateLayout();

        Descendant<ScrollViewer>(plan)!.MaxHeight.Should().BeApproximately(600 * 0.4, 1e-9);
    });

    // The Plan tab's view model as the template sees it: its own share for the bar (routes.mapLegendHeight).
    public sealed record Share(double MapLegendHeight);

    private static T? Descendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if ((child as T ?? Descendant<T>(child)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
