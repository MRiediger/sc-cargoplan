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
    public void The_max_crate_column_and_the_button_click_handler_are_what_MaxCrateDropdownTests_rebuilds()
    {
        // B8: MaxCrateDropdownTests builds this column and this handler in code; if the markup changes, they test the past.
        var grid = Grid();
        var column = Columns(grid).Single(c => (string?)c.Attribute("Header") == "Max size");

        column.Name.LocalName.Should().Be("DataGridComboBoxColumn");
        column.Attribute("ItemsSource")!.Value.Should().Be("{x:Static ed:MissionRows.MaxCrateSizes}");
        column.Attribute("SelectedItemBinding")!.Value.Should().Be("{Binding MaxCrateSize, UpdateSourceTrigger=PropertyChanged}");
        grid.Parent!.Attribute("ButtonBase.Click")!.Value.Should().Be("OnAnyButtonClick");
    }

    [Fact]
    public void The_max_size_combo_box_takes_the_tool_windows_combo_box_style()
    {
        // Session 4d addendum: the column's own default style has no BasedOn and pushed Fluent's implicit one aside; the
        // layout drop-downs use the implicit one, so this one is based on it, no style of our own (A4). Only while
        // editing: Fluent's combo box keeps 38 px for its arrow, so the closed cell stays text, in the cell's colour.
        var column = Columns(Grid()).Single(c => (string?)c.Attribute("Header") == "Max size");
        var style = column.Elements().Single(e => e.Name.LocalName == "DataGridComboBoxColumn.EditingElementStyle").Elements().Single();

        style.Attribute("TargetType")!.Value.Should().Be("ComboBox");
        style.Attribute("BasedOn")!.Value.Should().Be("{StaticResource ToolComboBoxStyle}");
        Setter(style, "IsSynchronizedWithCurrentItem").Should().Be("False", "the column's default style sets it");
        column.Elements().Should().NotContain(e => e.Name.LocalName == "DataGridComboBoxColumn.ElementStyle");
        column.Attribute("MinWidth")!.Value.Should().Be("72", "the open combo box shows 32 next to its arrow");
    }

    [Fact]
    public void The_closed_editing_combo_box_takes_the_selected_rows_colours()
    {
        // Second round at the picture (4d): after a pick the cell stays in edit mode; Fluent's own text and background
        // colours read grey on cyan. An edited cell lies in the selected row, so its colours are the selected row's.
        // In the grid's resources, not the style's: a brush's DynamicResource colour resolves only in the element tree.
        var resources = Grid().Elements().Single(e => e.Name.LocalName == "DataGrid.Resources").Elements().ToList();
        string? Brush(string key) => (string?)resources.SingleOrDefault(e => (string?)e.Attribute(X + "Key") == key)?.Attribute("Color");

        string[] text = ["ComboBoxForeground", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed", "ComboBoxForegroundFocused",
            "ComboBoxForegroundFocusedPressed", "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundFocused",
            "ComboBoxDropDownGlyphForegroundFocusedPressed"];
        string[] clear = ["ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundPressed",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed"];
        text.Select(Brush).Should().AllBe("{DynamicResource ToolSelectedRowTextColor}", "the selected row's text in either mode");
        clear.Select(Brush).Should().AllBe("Transparent");
    }

    [Fact]
    public void A_selected_row_shows_the_bar_in_the_pill_colour_where_the_mode_gives_it_a_width()
    {
        // Variant C (4d): dark gives the bar 3 px, light 0 px, so light stays Fluent's.
        var rowStyle = Grid().Elements().Single(e => e.Name.LocalName == "DataGrid.RowStyle").Elements().Single();
        var selected = rowStyle.Descendants().Single(e => e.Name.LocalName == "Trigger" && (string?)e.Attribute("Property") == "IsSelected");

        Setter(rowStyle, "BorderThickness").Should().Be("{DynamicResource ToolRowBarThickness}");
        Setter(selected, "BorderBrush").Should().Be("{DynamicResource ComboBoxItemPillFillBrush}");
    }

    [Fact]
    public void The_layout_drop_downs_items_build_on_the_tool_windows_item_style()
    {
        var root = Grid().Ancestors().Last();
        var itemStyles = root.Descendants().Where(e => e.Name.LocalName == "ComboBox.ItemContainerStyle").Select(e => e.Elements().Single()).ToList();

        itemStyles.Should().HaveCount(4);
        itemStyles.Should().OnlyContain(s => (string?)s.Attribute("BasedOn") == "{StaticResource ToolComboBoxItemStyle}");
        root.Descendants().Where(e => e.Name.LocalName == "ComboBox").Should().OnlyContain(c => c.Attribute("Style") == null, "the style is the window's implicit one");
    }

    [Fact]
    public void The_max_size_header_names_the_whole_term_in_its_tool_tip()
    {
        var column = Columns(Grid()).Single(c => (string?)c.Attribute("Header") == "Max size");
        var style = column.Elements().Single(e => e.Name.LocalName == "DataGridComboBoxColumn.HeaderStyle").Elements().Single();

        Setter(style, "ToolTip").Should().Be("{x:Static ed:MissionRows.MaxCrateSizeToolTipText}");
        style.Attribute("BasedOn")!.Value.Should().Be("{StaticResource EditorColumnHeaderStyle}");
    }

    [Fact]
    public void Column_headers_wrap_instead_of_being_cut()
    {
        var grid = Grid();
        var header = grid.Ancestors().Last().Descendants()
            .Single(e => e.Name.LocalName == "Style" && (string?)e.Attribute(X + "Key") == "EditorColumnHeaderStyle");

        grid.Attribute("ColumnHeaderStyle")!.Value.Should().Be("{StaticResource EditorColumnHeaderStyle}");
        header.Attribute("BasedOn")!.Value.Should().Be("{StaticResource DefaultDataGridColumnHeaderStyle}");
        header.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("TextWrapping")!.Value.Should().Be("Wrap");
        var crates = Columns(grid).Single(c => (string?)c.Attribute("Header") == "Crates");
        crates.Descendants().Single(e => e.Name.LocalName == "Style" && (string?)e.Attribute("TargetType") == "DataGridColumnHeader")
            .Attribute("BasedOn")!.Value.Should().Be("{StaticResource EditorColumnHeaderStyle}", "a column's own header style replaces the grid's");
    }

    [Fact]
    public void A_long_crate_list_wraps_in_its_cell_and_edits_on_one_line()
    {
        var crates = Columns(Grid()).Single(c => (string?)c.Attribute("Header") == "Crates");
        var shown = crates.Elements().Single(e => e.Name.LocalName == "DataGridTemplateColumn.CellTemplate").Descendants().First(e => e.Name.LocalName == "TextBlock");
        var edited = crates.Elements().Single(e => e.Name.LocalName == "DataGridTemplateColumn.CellEditingTemplate").Descendants().Single(e => e.Name.LocalName == "TextBox");

        shown.Attribute("TextWrapping")!.Value.Should().Be("Wrap");
        edited.Attribute("TextWrapping").Should().BeNull("the box stays one line");
    }

    private static string? Setter(XElement style, string property) =>
        (string?)style.Elements().SingleOrDefault(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == property)?.Attribute("Value");

    [Fact]
    public void Re_plan_shows_the_tool_tip_of_the_header_button()
    {
        // One text for both buttons (T7.5a2): the editor's had a wording of its own.
        var button = Grid().Ancestors().Last().Descendants()
            .Single(e => e.Name.LocalName == "Button" && (string?)e.Attribute("Content") == "Re-plan");

        button.Attribute("ToolTip")!.Value.Should().Be("{x:Static vm:OverlayViewModel.ReplanToolTipText}");
    }

    [Fact]
    public void Only_mission_and_destination_sort_each_under_its_key()
    {
        var sortable = Columns(Grid()).Where(c => (string?)c.Attribute("CanUserSort") != "False").ToArray();

        sortable.Select(c => ((string?)c.Attribute("Header"), (string?)c.Attribute("SortMemberPath"))).Should().Equal(
            ("Mission", nameof(EditorSortColumn.Mission)), ("Destination", nameof(EditorSortColumn.Destination)));
    }

    [Fact]
    public void Add_to_mission_runs_the_view_models_command()
    {
        // K6.5: next to Add, its own button; Add stays a typed row.
        using var stream = typeof(MissionEditorXamlTests).Assembly.GetManifestResourceStream(ResourceName)!;
        var button = XDocument.Load(stream).Root!.Descendants().Single(e => e.Name.LocalName == "Button" && (string?)e.Attribute("Content") == "Add to mission");

        button.Attribute("Command")!.Value.Should().Be("{Binding AddToMissionCommand}");
    }

    [Fact]
    public void The_mission_column_shows_the_contract_numbers()
    {
        var mission = Columns(Grid()).Single(c => (string?)c.Attribute("Header") == "Mission");

        mission.Attribute("Binding")!.Value.Should().Be("{Binding MissionLabel, Mode=OneWay}");
        Columns(Grid()).Should().NotContain(c => (string?)c.Attribute("Header") == "Source");
    }
}
