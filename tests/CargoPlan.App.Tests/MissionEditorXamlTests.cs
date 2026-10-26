using System.Xml.Linq;
using CargoPlan.App.Editor;

namespace CargoPlan.App.Tests;

/// <summary>The editor's markup read as XML, like <see cref="OverlayLegendXamlTests"/>: which columns sort (K6.3).</summary>
public class MissionEditorXamlTests
{
    private const string ResourceName = "xaml/MissionEditorWindow.xaml";

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Grid()
    {
        using var stream = typeof(MissionEditorXamlTests).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");
        return XDocument.Load(stream).Root!.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "Grid");
    }

    private static XElement[] Columns(XElement grid) =>
        [.. grid.Elements().Single(e => e.Name.LocalName == "DataGrid.Columns").Elements()];

    [Fact]
    public void The_grid_shows_the_sorted_view_and_hands_header_clicks_to_the_view_model()
    {
        var grid = Grid();

        grid.Attribute("ItemsSource")!.Value.Should().Be("{Binding RowsView}");
        grid.Attribute("CanUserSortColumns")!.Value.Should().Be("True");
        grid.Attribute("Sorting")!.Value.Should().Be("OnSorting");
    }

    [Fact]
    public void Only_mission_and_destination_sort_each_under_its_key()
    {
        var sortable = Columns(Grid()).Where(c => (string?)c.Attribute("CanUserSort") != "False").ToArray();

        sortable.Select(c => ((string?)c.Attribute("Header"), (string?)c.Attribute("SortMemberPath"))).Should().Equal(
            ("Mission", nameof(EditorSortColumn.Mission)), ("Destination", nameof(EditorSortColumn.Destination)));
    }

    [Fact]
    public void The_mission_column_shows_the_ContractTracker_ids()
    {
        var mission = Columns(Grid()).Single(c => (string?)c.Attribute("Header") == "Mission");

        mission.Attribute("Binding")!.Value.Should().Be("{Binding Source, Mode=OneTime}");
        Columns(Grid()).Should().NotContain(c => (string?)c.Attribute("Header") == "Source");
    }
}
