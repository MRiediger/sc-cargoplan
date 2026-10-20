using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CargoPlan.Core.Models;

namespace CargoPlan.Import.ContractTracker;

/// <summary>
/// Reads a contract from ContractTracker's raw OCR text (D39): commodities, destinations with SCU, the max crate size
/// and the length of the drop-off list. Tolerant of OCR errors; works without the database.
/// </summary>
// Source-generated regexes on partial properties (.NET 9+):
// https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators
internal static partial class ContractTextParser
{
    // Headings as the game prints them, compared in their anchor form (see AnchorForm).
    private const string StartHeading = "DETAILS";
    private const string EndHeading = "ACCEPT OFFER";
    private const string DropOffHeading = "DROP OFF LOCATIONS";
    private const string PickUpHeading = "PICK UP LOCATIONS";

    public static ParsedContract Parse(string rawText)
    {
        var lines = Block(rawText.ReplaceLineEndings("\n").Split('\n'));
        return new(Deliveries(lines), MaxCrateSize(string.Join('\n', lines)), ListedDestinationCount(lines));
    }

    /// <summary>
    /// The lines between "DETAILS" and "ACCEPT OFFER": list fragments of the contract list stand before, digit noise after
    /// (D38 addendum). Either heading may be misread or missing (a capture from the Accepted tab has no "ACCEPT OFFER",
    /// D40): then the block runs from the start or to the end, and the delivery pattern keeps out what doesn't belong.
    /// </summary>
    private static string[] Block(string[] lines)
    {
        var start = Array.FindIndex(lines, l => IsHeading(l, StartHeading)) + 1;
        var end = Array.FindIndex(lines, start, l => IsHeading(l, EndHeading));
        return lines[start..(end < 0 ? lines.Length : end)];
    }

    private static IReadOnlyList<ParsedDelivery> Deliveries(string[] lines)
    {
        var read = lines.Select(l => DeliveryLine.Match(l.Trim()))
            .Where(m => m.Success)
            .Select(m => (
                Destination: ContractText.NormalizeDestination(m.Groups["destination"].Value),
                Complete: ContractText.HasNameEnd(m.Groups["destination"].Value),
                Commodity: ContractText.NormalizeCommodity(m.Groups["commodity"].Value),
                Scu: int.Parse(m.Groups["scu"].Value, CultureInfo.InvariantCulture)))
            .Where(d => d.Scu > 0 && d.Destination.Length > 0 && d.Commodity.Length > 0)
            .ToList();

        // Names a cut-off destination can be completed from: the drop-off list, the prose, the other deliveries.
        List<string> names =
        [
            .. lines.Select(l => FreightElevatorName.Match(l)).Where(m => m.Success)
                .Select(m => ContractText.NormalizeDestination(m.Groups["name"].Value)),
            .. read.Select(d => d.Destination),
        ];

        return [.. read.Select(d => new ParsedDelivery(d.Complete ? d.Destination : Completed(d.Destination, names), d.Commodity, d.Scu))];
    }

    /// <summary>
    /// The longest name that starts with the fragment ("Thundering" → "Thundering Express Station"), as long as all such
    /// names are starts of each other; otherwise, or without one, the fragment stays.
    /// </summary>
    private static string Completed(string fragment, IReadOnlyList<string> names)
    {
        var longer = names.Where(n => n.StartsWith(fragment + " ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.Length)
            .ToList();
        return longer is [var longest, ..] && longer.All(n => longest.StartsWith(n, StringComparison.OrdinalIgnoreCase))
            ? longest
            : fragment;
    }

    private static CrateSize? MaxCrateSize(string text) =>
        SizePhrase.Match(text) is { Success: true } match
        && int.TryParse(match.Groups["size"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var size)
        && Enum.IsDefined((CrateSize)size)
            ? (CrateSize)size
            : null;

    /// <summary>Entries after the drop-off heading up to the pick-up heading; null without a drop-off list.</summary>
    private static int? ListedDestinationCount(string[] lines)
    {
        var heading = Array.FindIndex(lines, l => AnchorForm(l).StartsWith(AnchorForm(DropOffHeading), StringComparison.Ordinal));
        if (heading < 0)
        {
            return null;
        }

        return lines.Skip(heading + 1)
            .TakeWhile(l => !AnchorForm(l).StartsWith(AnchorForm(PickUpHeading), StringComparison.Ordinal))
            .Count(l => ListEntry.IsMatch(l));
    }

    private static bool IsHeading(string line, string heading) =>
        AnchorForm(line).Equals(AnchorForm(heading), StringComparison.Ordinal);

    /// <summary>
    /// A heading as OCR may read it: no spaces, upper case, O and 0 alike, I, L, 1 and | alike
    /// ("DETAlLS", "ACCEPT 0FFER"). Applied to both sides of a comparison.
    /// </summary>
    private static string AnchorForm(string text)
    {
        var form = new StringBuilder(text.Length);
        foreach (var c in text.Where(c => !char.IsWhiteSpace(c)))
        {
            form.Append(char.ToUpperInvariant(c) switch
            {
                '0' => 'O',
                'L' or '1' or '|' => 'I',
                var upper => upper,
            });
        }

        return form.ToString();
    }

    /// <summary>
    /// "Deliver 0/4 SCU of Stims to Thundering Express Station": anchored on "/N SCU of … to …". The word before and
    /// the delivered count are whatever OCR made of them ("Dellver", "O/3", "O0/5").
    /// </summary>
    [GeneratedRegex(@"^\S+\s+\S*/\s*(?<scu>\d{1,5})\s+SCU\s+of\s+(?<commodity>.+?)\s+to\s+(?<destination>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeliveryLine { get; }

    /// <summary>"freight elevator at Faithful Dream Station at Hurston's L2": a full name, in lists and prose.</summary>
    [GeneratedRegex(@"freight\s+elevator\s+at\s+(?<name>.+?)\s+(?:at|above)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FreightElevatorName { get; }

    /// <summary>"- Freight elevator at …", the dash sometimes lost.</summary>
    [GeneratedRegex(@"^\s*[-•·]?\s*Freight\s+elevator\s+at\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListEntry { get; }

    /// <summary>"Max size will be 1 SCU." / "none of the containers are bigger than 4 SCU." (D38 addendum), line breaks allowed.</summary>
    [GeneratedRegex(@"(?:max\s+size\s+will\s+be|none\s+of\s+the\s+containers\s+are\s+bigger\s+than)\s+(?<size>\d{1,3})\s*SCU",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePhrase { get; }
}
