namespace CargoPlan.Ocr;

/// <summary>
/// Turns OCR blocks into text lines in reading order (S10.0 "+cols"): columns split at the left edges of the headings
/// "DETAILS" and "PRIMARY OBJECTIVES", read one after the other; inside a column the blocks of one screen row become one
/// line, left to right. Sorted by top edge alone, the prose (left) and the objectives (right) interleave (G2: 90.5 % →
/// 100 %). The result has the form of ContractTracker's raw text, which the parser reads.
/// </summary>
public static class ReadingOrder
{
    private static readonly string[] Headings = ["DETAILS", "PRIMARY OBJECTIVES"];

    // An objective marker: at most two characters in an about square box (measured 1.02–1.20 wide per high in the
    // S10.0 captures; "N/A" 1.45–1.75).
    private const int MarkerMaxLength = 2;
    private const float MarkerMaxAspect = 1.3f;

    /// <summary>The lines, or none if neither heading is there: then the region shows no contract.</summary>
    public static string[] Lines(IReadOnlyList<TextBox> blocks)
    {
        var borders = blocks
            .Where(b => Headings.Contains(b.Text.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(b => b.Left)
            .Distinct()
            .Order()
            .ToArray();
        if (borders.Length == 0)
        {
            return [];
        }

        // A line under a heading starts a few pixels left or right of it; half a line height absorbs that.
        var tolerance = blocks.Average(b => b.Height) / 2;
        return [.. blocks
            .GroupBy(b => borders.Count(border => b.Left >= border - tolerance))
            .OrderBy(column => column.Key)
            .SelectMany(Rows)];
    }

    // A block belongs to the row whose first block its middle falls into; the first block's edges, not the row's
    // growing ones, so a tall box can't swallow the next row.
    private static IEnumerable<string> Rows(IEnumerable<TextBox> column)
    {
        List<List<TextBox>> rows = [];
        foreach (var block in column.OrderBy(b => b.Top))
        {
            if (rows.Count > 0 && rows[^1][0] is var first && block.Middle >= first.Top && block.Middle <= first.Bottom)
            {
                rows[^1].Add(block);
            }
            else
            {
                rows.Add([block]);
            }
        }

        return rows.SelectMany(row => Line(row.OrderBy(b => b.Left).ToList()));
    }

    // The objective marker in front of each "Deliver …" reads as "0" or "L"; joined, the delivery no longer starts its
    // line and the parser (anchored to ^Deliver) loses it. So a first block that looks like a marker stays its own line,
    // as in ContractTracker's raw text.
    private static IEnumerable<string> Line(List<TextBox> row)
    {
        if (row.Count > 1 && IsMarker(row[0]))
        {
            yield return row[0].Text.Trim();
            row = row[1..];
        }

        yield return string.Join(' ', row.Select(b => b.Text.Trim()));
    }

    private static bool IsMarker(TextBox block) =>
        block.Text.Trim().Length <= MarkerMaxLength && block.Right - block.Left <= MarkerMaxAspect * block.Height;
}
