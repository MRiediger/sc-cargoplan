using System.Collections;

namespace CargoPlan.App.Editor;

/// <summary>The editor's sortable columns (K6.3); the names are the columns' <c>SortMemberPath</c>.</summary>
public enum EditorSortColumn
{
    Mission,
    Destination,
}

/// <summary>How the editor's view is sorted: a column and its direction (K6.3).</summary>
public sealed record EditorSort(EditorSortColumn Column, bool Descending);

/// <summary>
/// The order of the editor's view (K6.3, D19 addendum). By mission: ContractTracker captures by their first id, as a
/// number; typed rows behind them in both directions, by destination (typed rows of one destination are one mission,
/// D42), since a block without ids that jumped to the top would read like a second list (E1). By destination: the name
/// without case. The direction turns only the order inside those blocks. Blank rows stay last, and equal keys keep the
/// order of <paramref name="rows"/>, the order Plan reads. A non-generic comparer, as <c>ListCollectionView.CustomSort</c>
/// takes one.
/// </summary>
public sealed class MissionRowOrder(IList<MissionRow> rows, EditorSort sort) : IComparer, IComparer<MissionRow>
{
    public int Compare(object? x, object? y) => (x, y) switch
    {
        (MissionRow a, MissionRow b) => Compare(a, b),
        (MissionRow, _) => -1,
        (_, MissionRow) => 1,
        _ => 0,
    };

    public int Compare(MissionRow? x, MissionRow? y)
    {
        if (ReferenceEquals(x, y) || x is null || y is null)
        {
            return ReferenceEquals(x, y) ? 0 : x is null ? 1 : -1;
        }

        var order = x.IsBlank.CompareTo(y.IsBlank);
        if (order == 0 && sort.Column == EditorSortColumn.Mission)
        {
            order = (x.ContractId is null).CompareTo(y.ContractId is null);
        }

        if (order == 0)
        {
            order = Directed(sort.Column == EditorSortColumn.Mission && x.ContractId is { } a && y.ContractId is { } b
                ? a.CompareTo(b)
                : string.Compare(x.Destination.Trim(), y.Destination.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }

        return order != 0 ? order : rows.IndexOf(x).CompareTo(rows.IndexOf(y));
    }

    private int Directed(int order) => sort.Descending ? -order : order;
}
