namespace CargoPlan.Core.Missions;

/// <summary>
/// Where an item's crates are collected (D68): a list of places without amounts, as contracts name them ("Collect Stims
/// from A, B and C"). One rule for every way in – the editor's text, the mission file, ContractTracker's pickup text –
/// so the same places always give the same list.
/// </summary>
public static class Pickups
{
    /// <summary>Between the places in text: typed in the editor, shown in the info line, used by ContractTracker.</summary>
    public const string Separator = " / ";

    /// <summary>"A / B / a" → [A, B]; null if the text names no place (<see cref="Normalized"/>).</summary>
    public static IReadOnlyList<string>? Parse(string? text) => text is null ? null : Normalized(text.Split('/'));

    /// <summary>
    /// Each place trimmed, blanks left out, a place named again (any case) left out, the first spelling and the order
    /// kept; null if no place is left. What <see cref="MissionItem.Pickup"/> requires.
    /// </summary>
    public static IReadOnlyList<string>? Normalized(IEnumerable<string>? names)
    {
        if (names is null)
        {
            return null;
        }

        string[] places = [.. names.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
        return places.Length == 0 ? null : places;
    }

    /// <summary>The places joined with <see cref="Separator"/>; empty without pickup.</summary>
    public static string Text(IReadOnlyList<string>? pickup) => pickup is null ? "" : string.Join(Separator, pickup);
}
