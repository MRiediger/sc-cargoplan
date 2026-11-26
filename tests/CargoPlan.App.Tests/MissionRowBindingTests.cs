using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace CargoPlan.App.Tests;

/// <summary>
/// A mission's row (T13.7c, D130) binds its [+] by ElementName to the Plan tab's MapView from inside its own template,
/// in the bar (the tab's data context) and in the edge panel (a data context of its own) alike. Proves that the binding
/// resolves across the nested templates (A3: not from memory); OverlayRouteXamlTests checks the XAML has it.
/// </summary>
public sealed class MissionRowBindingTests
{
    private const string Template = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <DockPanel x:Name="MapView">
            <DockPanel.Resources>
              <DataTemplate x:Key="MissionRowTemplate">
                <Button Command="{Binding DataContext.AddMissionCommand, ElementName=MapView}" CommandParameter="{Binding}" />
              </DataTemplate>
            </DockPanel.Resources>
            <ItemsControl DockPanel.Dock="Top" ItemsSource="{Binding Rows}" ItemTemplate="{StaticResource MissionRowTemplate}" />
            <Border DataContext="{Binding Panel}">
              <ItemsControl ItemsSource="{Binding Rows}" ItemTemplate="{StaticResource MissionRowTemplate}" />
            </Border>
          </DockPanel>
        </DataTemplate>
        """;

    [Fact]
    public void A_rows_plus_reaches_the_plan_tabs_command_in_the_bar_and_in_the_panel() => Sta.Run(() =>
    {
        var command = new Probe();
        var plan = new ContentControl
        {
            Content = new Tab(command, ["bar"], new Panel(["panel"])),
            ContentTemplate = (DataTemplate)XamlReader.Parse(Template),
        };

        plan.Measure(new Size(400, 600));
        plan.Arrange(new Rect(0, 0, 400, 600));
        plan.UpdateLayout();

        var buttons = Descendants<Button>(plan).ToList();
        buttons.Select(b => b.CommandParameter).Should().Equal("bar", "panel");
        buttons.Should().OnlyContain(b => ReferenceEquals(b.Command, command));
    });

    // The Plan tab's view model as the template sees it: the command, the bar's rows, the panel with its own.
    public sealed record Tab(ICommand AddMissionCommand, IReadOnlyList<string> Rows, Panel Panel);

    public sealed record Panel(IReadOnlyList<string> Rows);

    private sealed class Probe : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found)
            {
                yield return found;
            }

            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
