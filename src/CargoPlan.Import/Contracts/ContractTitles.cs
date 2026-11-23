using System.Text.RegularExpressions;
using CargoPlan.Core.Routing;
using CargoPlan.Core.Text;

namespace CargoPlan.Import.Contracts;

/// <summary>
/// The contract title (T13.1) in three forms, each line compared in its anchor form (<see cref="OcrText.AnchorForm"/>:
/// no spaces, capitals, "l", "1" and "|" as "I"), so spacing, the bar and OCR's usual swaps need no case of their own:
/// <list type="bullet">
/// <item>Covalex: "Senior Rank - Direct Medium Cargo Haul";</item>
/// <item>Red Wind as the game data name it: "Senior Hauler Needed for Direct Large Shipment" (no capture has it);</item>
/// <item>Red Wind as the card shows it: "Member | Stellar Medium Haul | to Ruin Station" (issue #22), where "Direct" is
/// open unless it stands there.</item>
/// </list>
/// Rank and size are words the parser knows; any other word, and the line is no title. The first title of the text counts.
/// </summary>
// Source-generated regexes on partial properties (.NET 9+):
// https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators
internal static partial class ContractTitles
{
    // Words in their anchor form: "Small" is "SMAII", "Large" "IARGE".
    private static readonly Dictionary<string, HaulRank> Ranks = new(StringComparer.Ordinal)
    {
        ["TRAINEE"] = HaulRank.Trainee,
        ["ROOKIE"] = HaulRank.Rookie,
        ["JUNIOR"] = HaulRank.Junior,
        ["MEMBER"] = HaulRank.Member,
        ["EXPERIENCED"] = HaulRank.Experienced,
        ["SENIOR"] = HaulRank.Senior,
        ["MASTER"] = HaulRank.Master,
    };

    private static readonly Dictionary<string, HaulSize> Sizes = new(StringComparer.Ordinal)
    {
        ["EXTRASMAII"] = HaulSize.Xs,
        ["SMAII"] = HaulSize.Small,
        ["MEDIUM"] = HaulSize.Medium,
        ["IARGE"] = HaulSize.Large,
        ["BUIK"] = HaulSize.Bulk,
    };

    private static readonly Dictionary<string, AreaLevel> Areas = new(StringComparer.Ordinal)
    {
        ["PIANETARY"] = AreaLevel.Planetary,
        ["STEIIAR"] = AreaLevel.Stellar,
    };

    private const string Rank = "(?<rank>TRAINEE|ROOKIE|JUNIOR|MEMBER|EXPERIENCED|SENIOR|MASTER)";

    // "Extra Small" before "Small", or the card's area would take "EXTRA".
    private const string Size = "(?<size>EXTRASMAII|SMAII|MEDIUM|IARGE|BUIK)";

    /// <summary>The first title in <paramref name="lines"/>; null if no line is one.</summary>
    public static ContractTitle? Read(IEnumerable<string> lines) =>
        lines.Select(line => Of(OcrText.AnchorForm(line))).FirstOrDefault(title => title is not null);

    private static ContractTitle? Of(string anchor)
    {
        if (Covalex.Match(anchor) is { Success: true } covalex)
        {
            return Title(HaulContractor.Covalex, covalex, covalex.Groups["direct"].Success, area: null);
        }

        if (RedWindData.Match(anchor) is { Success: true } data)
        {
            return Title(HaulContractor.RedWind, data, data.Groups["direct"].Success, area: null);
        }

        if (RedWindCard.Match(anchor) is { Success: true } card)
        {
            var area = Areas.TryGetValue(card.Groups["area"].Value, out var known) ? known : (AreaLevel?)null;
            return Title(HaulContractor.RedWind, card, card.Groups["direct"].Success ? true : null, area);
        }

        return null;
    }

    private static ContractTitle Title(HaulContractor contractor, Match match, bool? direct, AreaLevel? area) =>
        new(contractor, Ranks[match.Groups["rank"].Value], Sizes[match.Groups["size"].Value], direct, area);

    /// <summary>"JUNIORRANK-SMAIICARGOHAUI": the dash as OCR may read it, or lost.</summary>
    [GeneratedRegex("^" + Rank + "RANK[-–—_~]?(?<direct>DIRECT)?" + Size + "CARGOHAUI$", RegexOptions.CultureInvariant)]
    private static partial Regex Covalex { get; }

    /// <summary>"SENIORHAUIERNEEDEDFORIARGESHIPMENT".</summary>
    [GeneratedRegex("^" + Rank + "HAUIERNEEDEDFOR(?<direct>DIRECT)?" + Size + "SHIPMENT$", RegexOptions.CultureInvariant)]
    private static partial Regex RedWindData { get; }

    /// <summary>
    /// "MEMBERISTEIIARMEDIUMHAUIITORUINSTATION": the bars as "I" or lost, the area one word, the place after "to" or
    /// "from" whatever it is, or cut off.
    /// </summary>
    [GeneratedRegex("^" + Rank + "I?(?<area>[A-Z]+?)(?<direct>DIRECT)?" + Size + "HAUI(?:I?(?:TO|FROM).*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RedWindCard { get; }
}
