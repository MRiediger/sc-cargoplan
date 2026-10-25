using System.Text;
using System.Text.RegularExpressions;

namespace CargoPlan.Import.ContractTracker;

/// <summary>
/// The one place where OCR text becomes a name (D40). The parser and the child-row fallback both call it, so a
/// destination or commodity comes out the same however it was read. Which names are one place is decided once where
/// they enter the plan (<see cref="DestinationNames"/>, B1); behind that, names compare ordinal, ignoring case.
/// </summary>
// Source-generated regexes on partial properties (.NET 9+):
// https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators
internal static partial class ContractText
{
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '?', '!'];

    private const string Above = "above";

    /// <summary>
    /// The station name: whitespace collapsed, "LS" read for "L5" corrected, and everything from " at …", " above …"
    /// or the end of a sentence cut off ("Everus Harbor above Hurston" → "Everus Harbor"); without such an end, an
    /// "above" torn off by the line break goes too ("Everus Harbor abov" → "Everus Harbor", T5.3b).
    /// </summary>
    public static string NormalizeDestination(string text)
    {
        var name = LagrangeMisread.Replace(Collapse(text), "L5");
        var cut = LocationTail.IsMatch(name) ? LocationTail.Replace(name, "") : WithoutTornAbove(name);
        return cut.TrimEnd(TrailingPunctuation).TrimEnd();
    }

    /// <summary>
    /// A pickup place (B6, D69): ContractTracker's pickup field can run into the next list line ("Everus Harbor. - Freight
    /// elevator at …") or a heading ("… DROP OFF LOCATIONS (ANY ORDER)"). Both are cut off, then the name goes through
    /// <see cref="NormalizeDestination"/> like any place. Empty if no name is left.
    /// </summary>
    public static string NormalizePickup(string text) =>
        NormalizeDestination(ListHeading.Replace(ListDash.Replace(Collapse(text), ""), ""));

    /// <summary>The commodity: whitespace collapsed, no punctuation at the end.</summary>
    public static string NormalizeCommodity(string text) => Collapse(text).TrimEnd(TrailingPunctuation).TrimEnd();

    /// <summary>
    /// Whether a destination as read shows where its name ends (" at", " above", ".", a torn "abov"). Without it the
    /// line may have been cut off in the middle of the name ("to Thundering").
    /// </summary>
    public static bool HasNameEnd(string text)
    {
        var name = Collapse(text);
        return LocationTail.IsMatch(name) || WithoutTornAbove(name).Length < name.Length;
    }

    /// <summary>
    /// Text as OCR may read it: no spaces, upper case, O and 0 alike, I, L, 1 and | alike ("DETAlLS", "ACCEPT 0FFER",
    /// "Melodic Fieids"). Applied to both sides of a comparison: headings, name completion, names of one place (B1).
    /// </summary>
    public static string AnchorForm(string text)
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

    private static string Collapse(string text) => Whitespace.Replace(text, " ").Trim();

    /// <summary>
    /// The name without a last word that is what's left of "above" at a line break: its start from two characters on
    /// ("ab", "abo", "abov"), or from four characters on with one character misread ("aboy"). Shorter words with a
    /// misread character stay, they may be part of the name ("St"). A whole " at" is the location tail already.
    /// </summary>
    private static string WithoutTornAbove(string name) =>
        LastWord.Match(name) is { Success: true } last && IsTornAbove(last.Groups["word"].Value) ? name[..last.Index] : name;

    private static bool IsTornAbove(string word)
    {
        if (word.Length is < 2 or > 5)
        {
            return false;
        }

        var misread = word.Where((c, i) => char.ToLowerInvariant(c) != Above[i]).Count();
        return misread == 0 || (misread == 1 && word.Length >= 4);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace { get; }

    /// <summary>OCR reads the 5 of a Lagrange point as S: "HUR-LS", "Hurston's LS Lagrange point".</summary>
    [GeneratedRegex(@"\bLS\b", RegexOptions.CultureInvariant)]
    private static partial Regex LagrangeMisread { get; }

    /// <summary>From the first " at", " above" or sentence end to the end of the text.</summary>
    [GeneratedRegex(@"\s+(?:at|above)(?:\s.*)?$|\.(?:\s.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex LocationTail { get; }

    /// <summary>
    /// A list line's dash from its start or after a space to the end: "- Freight elevator at …". A dash inside a word stays
    /// ("HUR-L1").
    /// </summary>
    [GeneratedRegex(@"(?:^|\s)[-–—](?:\s.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ListDash { get; }

    /// <summary>The contract's list headings to the end, as the game prints them (see ContractTextParser).</summary>
    [GeneratedRegex(@"(?:^|\s)(?:DROP\s+OFF|PICK\s+UP)\s+LOCATIONS\b.*$", RegexOptions.CultureInvariant)]
    private static partial Regex ListHeading { get; }

    /// <summary>The last word after at least one other ("Everus Harbor abov" → "abov").</summary>
    [GeneratedRegex(@"\s(?<word>\S+)$", RegexOptions.CultureInvariant)]
    private static partial Regex LastWord { get; }
}
