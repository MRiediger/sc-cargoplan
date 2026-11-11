using System.Globalization;
using System.Text.RegularExpressions;
using CargoPlan.Core.Models;

namespace CargoPlan.Import.Contracts;

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
    private const string DropOffHeading = "DROP OFF LOCATIONS";
    private const string PickUpHeading = "PICK UP LOCATIONS";
    private const string CollectVerb = "Collect";
    private const string ObjectivesHeading = "PRIMARY OBJECTIVES";

    // The buttons of the two views (T10.2a). ContractTracker's text has "TRACK" / "UNTRACK" alone, without "SHARE".
    private const string AcceptOffer = "ACCEPT OFFER";
    private const string ShareTrack = "SHARE TRACK";
    private const string ShareUntrack = "SHARE UNTRACK";

    /// <summary>The last line of a contract: the offers view's button or the accepted view's share line (T10.2a).</summary>
    private static readonly string[] EndHeadings = [AcceptOffer, ShareTrack, ShareUntrack];

    /// <summary>Whole lines and line starts that only the offers view shows.</summary>
    private static readonly (string[] Lines, string[] Starts) OfferSignals = ([AcceptOffer], ["Contract Availability"]);

    /// <summary>Whole lines and line starts that only the accepted view shows.</summary>
    private static readonly (string[] Lines, string[] Starts) AcceptedSignals =
        (["ABANDON", ShareTrack, ShareUntrack, "TRACK", "UNTRACK"], ["Contract Deadline"]);

    public static ParsedContract Parse(string rawText)
    {
        var text = rawText.ReplaceLineEndings("\n");
        var all = text.Split('\n');
        var lines = Block(all);
        return new(Deliveries(lines), MaxCrateSize(string.Join('\n', lines)), ListedDestinationCount(lines), Reward(text), Pickups(lines),
            View(all), all.Any(l => IsHeading(l, ObjectivesHeading)), Unread(lines));
    }

    /// <summary>
    /// Objective lines that read as nothing (T10.2b2): "Deliver 0/9 SCU of Silicon" without " to …", "Collect Silicon from"
    /// without its place. What the game shows while it is still typing the objectives in.
    /// </summary>
    private static IReadOnlyList<string> Unread(string[] lines) =>
    [
        .. lines.Select(l => l.Trim()).Where(l =>
            (DeliveryLike.IsMatch(l) && !DeliveryLine.IsMatch(l))
            || (CollectStub.Match(l) is { Success: true } stub && ContractText.AnchorForm(stub.Groups["verb"].Value) == ContractText.AnchorForm(CollectVerb))),
    ];

    /// <summary>
    /// The view from the whole text, buttons and title stand outside the block: one side's signals only, else unknown. In
    /// the anchor form, so "ACCEPT 0FFER" and "Contract DeadIine" count.
    /// </summary>
    private static ContractView View(string[] lines) =>
        (lines.Any(l => Shows(l, OfferSignals)), lines.Any(l => Shows(l, AcceptedSignals))) switch
        {
            (true, false) => ContractView.Offer,
            (false, true) => ContractView.Accepted,
            _ => ContractView.Unknown,
        };

    private static bool Shows(string line, (string[] Lines, string[] Starts) signals) =>
        signals.Lines.Any(heading => IsHeading(line, heading))
        || signals.Starts.Any(start => ContractText.AnchorForm(line).StartsWith(ContractText.AnchorForm(start), StringComparison.Ordinal));

    /// <summary>
    /// "Collect Stims from Everus Harbor." (T10.2a): one line per pickup, its verb as OCR may read it ("CoIlect"). A place
    /// without a sentence end may go on in the next line, but only by a few words ending one (<see cref="PlaceEnding"/>,
    /// "Station."): ContractTracker's text puts prose, a list or a heading there, our OCR the next delivery or its marker.
    /// </summary>
    private static IReadOnlyList<ParsedPickup> Pickups(string[] lines)
    {
        List<ParsedPickup> pickups = [];
        for (var i = 0; i < lines.Length; i++)
        {
            if (CollectLine.Match(lines[i].Trim()) is not { Success: true } match
                || ContractText.AnchorForm(match.Groups["verb"].Value) != ContractText.AnchorForm(CollectVerb))
            {
                continue;
            }

            var place = match.Groups["place"].Value.Trim();
            if (!place.EndsWith('.') && i + 1 < lines.Length && PlaceEnding.Match(lines[i + 1].Trim()) is { Success: true } ending)
            {
                place = $"{place} {ending.Value}";
            }

            var commodity = ContractText.NormalizeCommodity(match.Groups["commodity"].Value);
            var name = ContractText.NormalizePickup(place);
            if (commodity.Length > 0 && name.Length > 0)
            {
                pickups.Add(new(commodity, name));
            }
        }

        return pickups;
    }

    /// <summary>
    /// The lines between "DETAILS" and the last line (<see cref="EndHeadings"/>): list fragments of the contract list stand
    /// before, digit noise after (D38 addendum). Either heading may be misread or missing (ContractTracker's captures from
    /// the Accepted tab end in neither, D40): then the block runs from the start or to the end, and the delivery pattern
    /// keeps out what doesn't belong.
    /// </summary>
    private static string[] Block(string[] lines)
    {
        var start = Array.FindIndex(lines, l => IsHeading(l, StartHeading)) + 1;
        var end = Array.FindIndex(lines, start, l => EndHeadings.Any(heading => IsHeading(l, heading)));
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

    /// <summary>
    /// The amount after "Reward", separators dropped ("50,250", "50.250"): at the start of the text in ContractTracker's
    /// form, else on a line of its own anywhere (CargoPlan's reading order puts it after the prose, D99). Null if unreadable.
    /// </summary>
    private static long? Reward(string text) =>
        (RewardAtStart.Match(text) is { Success: true } atStart ? atStart : RewardLine.Match(text)) is { Success: true } match
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

    /// <summary>"Collect Waste from HUR-L1 Green Glade Station.": the verb is checked in its anchor form by the caller.</summary>
    [GeneratedRegex(@"^(?<verb>\S+)\s+(?<commodity>.+?)\s+from\s+(?<place>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CollectLine { get; }

    /// <summary>"Collect Silicon from" with nothing after "from": a Collect line cut off (T10.2b2).</summary>
    [GeneratedRegex(@"^(?<verb>\S+)\s+.+?\s+from\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CollectStub { get; }

    /// <summary>A delivery's count, "Deliver O/9 SCU of", whatever follows: what <see cref="DeliveryLine"/> should read.</summary>
    [GeneratedRegex(@"^\S+\s+\S*/\s*\d{1,5}\s+SCU\s+of\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeliveryLike { get; }

    /// <summary>
    /// The rest of a place broken across lines: one to three words of letters and a full stop at the start of the line
    /// ("Station.", "Express Station."). Not a list number ("1. Pick up"), no prose sentence, no marker ("L"), no heading.
    /// </summary>
    [GeneratedRegex(@"^\p{L}[\p{L}'’&-]*(?:\s+\p{L}[\p{L}'’&-]*){0,2}\.", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceEnding { get; }

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
    /// Third wording (B9, issue #22): "8 SCU or smaller", the number first; not after a digit or "/" on its line, so a
    /// delivery's "0/8" or "0/124" never counts. In brackets "(8 SCU or" is enough: OCR lost "smaller)" on the next line
    /// in two of three captures; "or more", "or larger", "or bigger" are no max.
    /// Fourth and fifth (S10.0 "+max2", T10.2a): "At most the containers will be 8 SCU in size." with up to two words of any
    /// spelling before the size ("willbe"), and "their ship couldn't handle 8 SCU containers" (the apostrophe as read).
    /// </summary>
    [GeneratedRegex(@"(?:max\s*size\s+(?:\S{1,8}\s+){0,2}|bigger\s*than\s*)(?<size>[\dOIL|]{1,3})\s*SCU"
        + @"|(?<![\d/][ \t]*)(?<size>[\dOIL|]{1,3})\s*SCU\s*or\s*smaller"
        + @"|\(\s*(?<size>[\dOIL|]{1,3})\s*SCU\s*or\b(?!\s*(?:more|larger|bigger))"
        + @"|at\s*most\s+the\s+containers\s+(?:\S{1,8}\s+){0,2}(?<size>[\dOIL|]{1,3})\s*SCU"
        + @"|couldn\S?t\s+handle\s+(?<size>[\dOIL|]{1,3})\s*SCU",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePhrase { get; }

    /// <summary>
    /// A reward's amount: digits in groups of three with "," or "." between ("81,250"), or digits alone. Anything else
    /// ("8l,250", "81,25") is unreadable rather than a wrong number.
    /// </summary>
    private const string Amount = @"(?<reward>\d{1,3}(?:[,.]\d{3})+|\d+)";

    /// <summary>ContractTracker's form: "Reward" and the amount on its own line at the very start of the text ("Reward\n 50,250").</summary>
    [GeneratedRegex(@"\A\s*Reward\s*" + Amount + @"[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex RewardAtStart { get; }

    /// <summary>
    /// CargoPlan's form: "Reward 81,250" as one whole line anywhere in the text; the label never joins another line. One
    /// sign may stand before the amount: the aUEC sign, read as "±" at 1080p (S10.0 set-1080s c17). Not a letter or a
    /// digit, which would be part of a wrong amount.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*Reward[ \t]+(?:[^\w\s][ \t]*)?" + Amount + @"[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex RewardLine { get; }
}
