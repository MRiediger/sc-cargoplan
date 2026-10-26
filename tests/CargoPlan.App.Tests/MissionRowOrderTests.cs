using CargoPlan.App.Editor;

namespace CargoPlan.App.Tests;

/// <summary>
/// The editor's sort (K6.3, D19 addendum): only the view is sorted, by mission (ContractTracker's first id, numeric; typed
/// rows after, by destination) or by destination. Rows without an id and blank rows keep their place behind the others in
/// both directions; equal keys keep the order of first appearance.
/// </summary>
public class MissionRowOrderTests
{
    private static MissionRow Row(string destination, long? contract = null, string commodity = "Stims") =>
        new() { Destination = destination, Commodity = commodity, Scu = "4", ContractId = contract };

    private static readonly MissionRow Blank = new();

    private static string[] Sorted(IReadOnlyList<MissionRow> rows, EditorSortColumn column, bool descending) =>
        [.. rows.Order(new MissionRowOrder(rows, new EditorSort(column, descending)))
            .Select(r => r.IsBlank ? "_" : r.ContractId is { } id ? $"{r.Destination} #{id}" : r.Destination)];

    /// <summary>Contracts 37, 4 and 12, typed Riker and Area18, a blank row in the middle.</summary>
    private static MissionRow[] Mixed() =>
        [Row("Orison", 37), Row("Riker"), Blank, Row("Shubin", 4), Row("Area18"), Row("Lorville", 12)];

    [Fact]
    public void By_mission_contract_ids_go_by_number_then_typed_rows_by_destination()
    {
        Sorted(Mixed(), EditorSortColumn.Mission, descending: false)
            .Should().Equal("Shubin #4", "Lorville #12", "Orison #37", "Area18", "Riker", "_");
    }

    [Fact]
    public void By_mission_descending_typed_rows_stay_behind_the_contracts()
    {
        // E1: rows without an id are a null key; a block that moved to the top would read like a second list.
        Sorted(Mixed(), EditorSortColumn.Mission, descending: true)
            .Should().Equal("Orison #37", "Lorville #12", "Shubin #4", "Riker", "Area18", "_");
    }

    [Fact]
    public void By_destination_both_ways_blank_rows_last()
    {
        Sorted(Mixed(), EditorSortColumn.Destination, descending: false)
            .Should().Equal("Area18", "Lorville #12", "Orison #37", "Riker", "Shubin #4", "_");
        Sorted(Mixed(), EditorSortColumn.Destination, descending: true)
            .Should().Equal("Shubin #4", "Riker", "Orison #37", "Lorville #12", "Area18", "_");
    }

    [Fact]
    public void Destinations_compare_without_case()
    {
        MissionRow[] rows = [Row("riker"), Row("Area18"), Row("Orison")];

        Sorted(rows, EditorSortColumn.Destination, descending: false).Should().Equal("Area18", "Orison", "riker");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Equal_keys_keep_the_order_of_first_appearance_both_ways(bool descending)
    {
        // Two rows of capture 37 and two typed Riker rows: within each pair the standard order stays.
        var (first, second) = (Row("Orison", 37, "Gold"), Row("Orison", 37, "Quartz"));
        var (third, fourth) = (Row("Riker", commodity: "Titanium"), Row("Riker", commodity: "Tin"));
        MissionRow[] rows = [first, third, second, fourth];

        var order = rows.Order(new MissionRowOrder(rows, new EditorSort(EditorSortColumn.Mission, descending))).ToArray();

        order.Should().Equal(first, second, third, fourth);
    }

    [Fact]
    public void A_typed_row_has_no_contract_id()
    {
        new MissionRow().ContractId.Should().BeNull();
    }
}
