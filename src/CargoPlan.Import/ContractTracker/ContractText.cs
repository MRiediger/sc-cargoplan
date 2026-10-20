using System.Text.RegularExpressions;

namespace CargoPlan.Import.ContractTracker;

/// <summary>
/// The one place where OCR text becomes a name (D40). The parser and the child-row fallback both call it, so a
/// destination or commodity comes out the same however it was read. Comparing candidates (T5.4) normalises nothing
/// itself; it uses the comparer destinations use everywhere: ordinal, ignoring case.
/// </summary>
// Source-generated regexes on partial properties (.NET 9+):
// https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators
internal static partial class ContractText
{
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '?', '!'];

    /// <summary>
    /// The station name: whitespace collapsed, "LS" read for "L5" corrected, and everything from " at …", " above …"
    /// or the end of a sentence cut off ("Everus Harbor above Hurston" → "Everus Harbor").
    /// </summary>
    public static string NormalizeDestination(string text)
    {
        var name = LagrangeMisread.Replace(Collapse(text), "L5");
        return LocationTail.Replace(name, "").TrimEnd(TrailingPunctuation).TrimEnd();
    }

    /// <summary>The commodity: whitespace collapsed, no punctuation at the end.</summary>
    public static string NormalizeCommodity(string text) => Collapse(text).TrimEnd(TrailingPunctuation).TrimEnd();

    /// <summary>
    /// Whether a destination as read shows where its name ends (" at", " above", "."). Without it the line may have
    /// been cut off in the middle of the name ("to Thundering").
    /// </summary>
    public static bool HasNameEnd(string text) => LocationTail.IsMatch(Collapse(text));

    private static string Collapse(string text) => Whitespace.Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace { get; }

    /// <summary>OCR reads the 5 of a Lagrange point as S: "HUR-LS", "Hurston's LS Lagrange point".</summary>
    [GeneratedRegex(@"\bLS\b", RegexOptions.CultureInvariant)]
    private static partial Regex LagrangeMisread { get; }

    /// <summary>From the first " at", " above" or sentence end to the end of the text.</summary>
    [GeneratedRegex(@"\s+(?:at|above)(?:\s.*)?$|\.(?:\s.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex LocationTail { get; }
}
