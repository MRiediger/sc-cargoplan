using System.Globalization;
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
    // Headings as the game prints them, compared in their anchor form (see ContractText.AnchorForm).
    private const string StartHeading = "DETAILS";
    private const string EndHeading = "ACCEPT OFFER";
    private const string DropOffHeading = "DROP OFF LOCATIONS";
    private const string PickUpHeading = "PICK UP LOCATIONS";

    public static ParsedContract Parse(string rawText)
    {
        var text = rawText.ReplaceLineEndings("\n");
        var lines = Block(text.Split('\n'));
        return new(Deliveries(lines), MaxCrateSize(string.Join('\n', lines)), ListedDestinationCount(lines), Reward(text));
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

        // A line without a name end may break anywhere, also mid-word ("to Thunde"). Pickups never complete.
        // Step 1: the drop-off list; step 2: "to a freight elevator at …" in the prose (T5.3b). Null = still a fragment.
        var dropOffNames = DropOffEntries(lines).Select(l => FreightElevatorName.Match(l)).Where(m => m.Success)
            .Select(m => ContractText.NormalizeDestination(m.Groups["name"].Value))
            .ToList();
        var proseNames = ProseDestination.Matches(string.Join('\n', lines))
            .Select(m => ContractText.NormalizeDestination(m.Groups["name"].Value))
            .ToList();
        var listed = read
            .Select(d => d.Complete ? d.Destination : CompletedFrom(d.Destination, dropOffNames) ?? CompletedFrom(d.Destination, proseNames))
            .ToList();

        // Step 3: a fragment left over takes a longer destination of the same capture ("Thundering" → "Thundering Express",
        // ids 28/29 have no list), otherwise it stays.
        var names = read.Select((d, i) => listed[i] ?? d.Destination).ToList();
        return
        [
            .. read.Select((d, i) => new ParsedDelivery(
                listed[i] ?? CompletedFrom(d.Destination, names.Where(n => n.Length > d.Destination.Length)) ?? d.Destination,
                d.Commodity,
                d.Scu)),
        ];
    }

    /// <summary>
    /// The one name that starts with the fragment, character by character ("Thunde" → "Thundering Express Station");
    /// null if none or several do. Both read as OCR may read them (<see cref="ContractText.AnchorForm"/>), so "Melodic
    /// Fieids" finds "Melodic Fields Station" and takes its spelling (B1).
    /// </summary>
    private static string? CompletedFrom(string fragment, IEnumerable<string> names) =>
        names.Where(n => ContractText.AnchorForm(n).StartsWith(ContractText.AnchorForm(fragment), StringComparison.Ordinal))
            .DistinctBy(ContractText.AnchorForm, StringComparer.Ordinal)
            .ToList() is [var only] ? only : null;

    /// <summary>
    /// The size, its digits corrected (<see cref="OcrDigits"/>); null if the result is no crate size: a "?" in the
    /// legend is better than a wrong size without one.
    /// </summary>
    private static CrateSize? MaxCrateSize(string text) =>
        SizePhrase.Match(text) is { Success: true } match
        && int.TryParse(OcrDigits(match.Groups["size"].Value), NumberStyles.None, CultureInfo.InvariantCulture, out var size)
        && Enum.IsDefined((CrateSize)size)
            ? (CrateSize)size
            : null;

    /// <summary>
    /// Digits as OCR misreads them in the size, which the game prints in blue: O for 0; I, L and | for 1 ("l SCU").
    /// </summary>
    private static string OcrDigits(string text) =>
        string.Concat(text.Select(c => char.ToUpperInvariant(c) switch
        {
            'O' => '0',
            'I' or 'L' or '|' => '1',
            _ => c,
        }));

    /// <summary>The amount after "Reward" at the start of the text, separators dropped ("50,250"); null if unreadable.</summary>
    private static long? Reward(string text) =>
        RewardLine.Match(text) is { Success: true } match
        && long.TryParse(match.Groups["reward"].Value.Replace(",", "").Replace(".", ""), NumberStyles.None,
            CultureInfo.InvariantCulture, out var reward)
            ? reward
            : null;

    /// <summary>Entries of the drop-off list; null without one.</summary>
    private static int? ListedDestinationCount(string[] lines) => HasDropOffList(lines) ? DropOffEntries(lines).Count() : null;

    private static bool HasDropOffList(string[] lines) => Array.FindIndex(lines, IsDropOffHeading) >= 0;

    /// <summary>"- Freight elevator at …" lines after the drop-off heading, up to the pick-up heading.</summary>
    private static IEnumerable<string> DropOffEntries(string[] lines) =>
        lines.SkipWhile(l => !IsDropOffHeading(l)).Skip(1)
            .TakeWhile(l => !ContractText.AnchorForm(l).StartsWith(ContractText.AnchorForm(PickUpHeading), StringComparison.Ordinal))
            .Where(l => ListEntry.IsMatch(l));

    private static bool IsDropOffHeading(string line) =>
        ContractText.AnchorForm(line).StartsWith(ContractText.AnchorForm(DropOffHeading), StringComparison.Ordinal);

    private static bool IsHeading(string line, string heading) =>
        ContractText.AnchorForm(line).Equals(ContractText.AnchorForm(heading), StringComparison.Ordinal);

    /// <summary>
    /// "Deliver 0/4 SCU of Stims to Thundering Express Station": anchored on "/N SCU of … to …". The word before and
    /// the delivered count are whatever OCR made of them ("Dellver", "O/3", "O0/5").
    /// </summary>
    [GeneratedRegex(@"^\S+\s+\S*/\s*(?<scu>\d{1,5})\s+SCU\s+of\s+(?<commodity>.+?)\s+to\s+(?<destination>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeliveryLine { get; }

    /// <summary>
    /// "freight elevator at Faithful Dream Station at Hurston's L2": a full name, in lists and prose. The first letter of
    /// "elevator" may be misread or lost ("Freight alevator at", B1), here and in the two patterns below.
    /// </summary>
    [GeneratedRegex(@"freight\s+\w?levator\s+at\s+(?<name>.+?)\s+(?:at|above)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FreightElevatorName { get; }

    /// <summary>"- Freight elevator at …", the dash sometimes lost.</summary>
    [GeneratedRegex(@"^\s*[-•·]?\s*Freight\s+\w?levator\s+at\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListEntry { get; }

    /// <summary>
    /// "to a freight elevator at High Course Station at …": a destination named in the prose, whatever the verb
    /// ("Deliver to", "taken to"). "from a freight elevator" is a pickup and doesn't match. The name may break across
    /// lines and ends before " at", " above" or the end of the sentence.
    /// </summary>
    [GeneratedRegex(@"\bto\s+a\s+freight\s+\w?levator\s+at\s+(?<name>[^.]+?)(?:\s+(?:at|above)\b|\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProseDestination { get; }

    /// <summary>
    /// "Max size will be 1 SCU." / "none of the containers are bigger than 4 SCU." (D38 addendum), line breaks allowed.
    /// Tolerant (T5.3b): up to two words of any spelling after "Max size" ("willb"), only "bigger than" of the second
    /// phrase, and digits OCR may read as letters (<see cref="OcrDigits"/>).
    /// </summary>
    [GeneratedRegex(@"(?:max\s*size\s+(?:\S{1,8}\s+){0,2}|bigger\s*than\s*)(?<size>[\dOIL|]{1,3})\s*SCU",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePhrase { get; }

    /// <summary>"Reward" and the amount on its own line at the very start of the text ("Reward\n 50,250").</summary>
    [GeneratedRegex(@"\A\s*Reward\s*(?<reward>[\d,.]+)[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex RewardLine { get; }
}
