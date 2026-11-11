using System.Globalization;
using CargoPlan.Core.Missions;

namespace CargoPlan.Import.Contracts;

/// <summary>
/// One contract as CargoPlan's own OCR read it (T10.2a, D99): stops and items as entered (D23), names normalised
/// (<see cref="ContractText"/>), what the legend marks as uncertain, and the content key that tells two scans of one
/// contract from two contracts (D101).
/// </summary>
/// <param name="View">Offers (amounts are an estimate) or accepted; unknown if the text shows neither or both.</param>
/// <param name="Stops">One per place in the order of the text; each item with the pickups of its commodity's Collect lines.
/// Empty if no delivery line was readable.</param>
/// <param name="Pickups">Every place of the Collect lines, in the order of the text, each once.</param>
/// <param name="CrateSizeAssumed">The text names no max crate size; each item has the largest crate that fits its SCU.</param>
/// <param name="ListedDestinationCount">Entries of the contract's drop-off list; null if it has none.</param>
/// <param name="Reward">The reward in aUEC; null if the text had none or it wasn't readable.</param>
/// <param name="Key">The content key (D101): pickups, destination–commodity pairs, max size, reward; no SCU.</param>
/// <param name="Doubts">Why the text may still have been typing in (D103); empty for a text all there.</param>
public sealed record ScannedContract(
    ContractView View,
    IReadOnlyList<MissionStop> Stops,
    IReadOnlyList<string> Pickups,
    bool CrateSizeAssumed,
    int? ListedDestinationCount,
    long? Reward,
    string Key,
    IReadOnlyList<ContractDoubt> Doubts)
{
    /// <summary>A doubt a second read must clear, or the scan is "still loading" (D103).</summary>
    public bool InHardDoubt => Doubts.Any(d => d is ContractDoubt.NoDeliveryRead or ContractDoubt.ObjectiveLineUnread);

    /// <summary>
    /// The contract as Core takes it into the plan (D102): its stops, and a scan identity with <paramref name="scanNo"/>,
    /// estimated unless read from the accepted view (a view not told apart counts as estimated, D102).
    /// </summary>
    public ContractCapture ToCapture(int scanNo) => new(
        Stops,
        ContractSource.Scanned(new ScanIdentity(scanNo, Key, View != ContractView.Accepted), CrateSizeAssumed, ListedDestinationCount, Reward));

    /// <summary>A max size or reward that wasn't readable, in the key: equal only to itself, never to a number (D101).</summary>
    internal const string Unreadable = "?";

    private const string PartSeparator = " | ";
    private const string EntrySeparator = ", ";

    /// <summary>The drop-off list names more destinations than were read.</summary>
    public bool DestinationsIncomplete => ListedDestinationCount > Stops.Count;

    /// <summary>The contract in a scan's raw text (<see cref="ContractTextParser"/>). Never throws; nothing read = no stops.</summary>
    public static ScannedContract Read(string rawText)
    {
        var parsed = ContractTextParser.Parse(rawText);
        var pickupsByCommodity = parsed.Pickups
            .GroupBy(p => p.Commodity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Core.Missions.Pickups.Normalized(g.Select(p => p.Place)), StringComparer.OrdinalIgnoreCase);

        var stops = ContractStops.Of(parsed.Deliveries, parsed.MaxCrateSize, pickupsByCommodity);
        return new(
            parsed.View,
            stops,
            Core.Missions.Pickups.Normalized(parsed.Pickups.Select(p => p.Place)) ?? [],
            CrateSizeAssumed: parsed.MaxCrateSize is null,
            parsed.ListedDestinationCount,
            parsed.Reward,
            KeyOf(parsed),
            DoubtsOf(parsed, stops.Count));
    }

    /// <summary>
    /// Hard: the objectives without a delivery, or an objective line read as nothing. Soft, only with objectives: no
    /// reward; a delivered commodity without a Collect line though others have one; fewer destinations than listed (E1).
    /// </summary>
    private static IReadOnlyList<ContractDoubt> DoubtsOf(ParsedContract parsed, int stops)
    {
        if (!parsed.HasObjectives && parsed.Deliveries.Count == 0)
        {
            return [];
        }

        HashSet<string> collected = new(parsed.Pickups.Select(p => p.Commodity), StringComparer.OrdinalIgnoreCase);
        List<ContractDoubt> doubts = [];
        if (parsed.Deliveries.Count == 0)
        {
            doubts.Add(ContractDoubt.NoDeliveryRead);
        }

        if (parsed.Unread is [_, ..])
        {
            doubts.Add(ContractDoubt.ObjectiveLineUnread);
        }

        if (parsed.Reward is null)
        {
            doubts.Add(ContractDoubt.NoReward);
        }

        if (collected.Count > 0 && parsed.Deliveries.Any(d => !collected.Contains(d.Commodity)))
        {
            doubts.Add(ContractDoubt.CommodityWithoutPickup);
        }

        if (parsed.ListedDestinationCount > stops)
        {
            doubts.Add(ContractDoubt.FewerDestinationsThanListed);
        }

        return doubts;
    }

    /// <summary>
    /// "EVERUSHARBOR | FAITHFUIDREAM:QUANTUMFUEI, GREENGIADE:HYDROGENFUEI | 16 | 191500": places as one place is compared
    /// (<see cref="DestinationNames.Key"/>, B1), commodities in their anchor form, each part sorted and without repeats, so
    /// neither the order of the text nor the amounts matter. A destination with its commodity, not two lists: two contracts
    /// that swap their commodities between two places stay two (H1).
    /// </summary>
    private static string KeyOf(ParsedContract parsed) => string.Join(PartSeparator,
        Sorted(parsed.Pickups.Select(p => DestinationNames.Key(p.Place))),
        Sorted(parsed.Deliveries.Select(d => $"{DestinationNames.Key(d.Destination)}:{ContractText.AnchorForm(d.Commodity)}")),
        parsed.MaxCrateSize is { } max ? ((int)max).ToString(CultureInfo.InvariantCulture) : Unreadable,
        parsed.Reward?.ToString(CultureInfo.InvariantCulture) ?? Unreadable);

    private static string Sorted(IEnumerable<string> entries) =>
        string.Join(EntrySeparator, entries.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
