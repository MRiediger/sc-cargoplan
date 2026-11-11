namespace CargoPlan.Core.Missions;

/// <summary>
/// Who a scanned contract is (D102): its number in the plan and its content key (D101, built by the import). Core
/// compares keys as text and knows nothing of how they are made.
/// </summary>
/// <param name="No">Counted per plan from 1, never given twice in a session (<c>#3</c> in legend and editor).</param>
/// <param name="Key">The content key: pickups, destination–commodity pairs, max size, reward; no SCU (D101).</param>
/// <param name="QuantitiesEstimated">
/// Read from the offers view (or a view not told apart): the game rolls the amounts again on accepting, so they are an
/// estimate until a scan of the accepted contract with the same key (T10.2b2).
/// </param>
public sealed record ScanIdentity(int No, string Key, bool QuantitiesEstimated)
{
    public int No { get; } = No >= 1 ? No : throw new ArgumentOutOfRangeException(nameof(No), No, "A scan number is 1 or more.");

    public string Key { get; } = !string.IsNullOrWhiteSpace(Key) ? Key : throw new ArgumentException("A scan has a content key.", nameof(Key));
}
