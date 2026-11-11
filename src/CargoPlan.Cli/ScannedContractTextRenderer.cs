using System.Globalization;
using System.Text;
using CargoPlan.Core.Missions;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Cli;

/// <summary>A scanned contract as plain text, for checking the parser against screenshots without the overlay (T10.2a).</summary>
internal static class ScannedContractTextRenderer
{
    public static string Render(ScannedContract contract)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"View: {contract.View}");
        text.AppendLine(contract.Pickups.Count == 0 ? "Pickups: none read" : $"Pickups: {Pickups.Text(contract.Pickups)}");
        AppendStops(text, contract.Stops, contract.CrateSizeAssumed, contract.ListedDestinationCount, withPickups: true);
        text.AppendLine(contract.Reward is { } reward
            ? string.Create(CultureInfo.InvariantCulture, $"Reward: {reward:N0}")
            : "Reward: unreadable");
        text.AppendLine($"Key: {contract.Key}");
        return text.ToString();
    }

    /// <summary>
    /// One line per stop with its items, "?" after an assumed size as the legend shows it (T5.4), and a warning when no
    /// destination or fewer than the drop-off list names were read. Shared with <see cref="CandidateTextRenderer"/>.
    /// </summary>
    internal static void AppendStops(StringBuilder text, IReadOnlyList<MissionStop> stops, bool crateSizeAssumed, int? listedDestinations, bool withPickups)
    {
        var assumed = crateSizeAssumed ? "?" : "";
        foreach (var stop in stops)
        {
            var items = stop.Items.Select(i => string.Create(CultureInfo.InvariantCulture,
                $"{i.Commodity} {i.Scu} SCU (max {(int)i.MaxCrateSize} SCU{assumed}){(withPickups && i.Pickup is { } pickup ? $" from {Pickups.Text(pickup)}" : "")}"));
            text.AppendLine(CultureInfo.InvariantCulture, $"  {stop.Order}. {stop.Destination}: {string.Join(", ", items)}");
        }

        if (stops.Count == 0)
        {
            text.AppendLine("  ⚠ no destination read");
        }

        if (listedDestinations > stops.Count)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  ⚠ {stops.Count} of {listedDestinations} destinations");
        }
    }
}
