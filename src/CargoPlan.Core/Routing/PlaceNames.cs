using System.Text.RegularExpressions;

namespace CargoPlan.Core.Routing;

/// <summary>
/// How the planner orders and names places (T13.2, picture gate): the board hubs first, then the Lagrange stations planet
/// by planet in the order of the boards (Hurston, Crusader, ArcCorp, microTech; Stanton1–4), each by its number, the
/// rest alphabetical. An L station shows as its code ("HUR-L1"), every other place in full. The matrix uses both, the
/// map (T13.7) will.
/// </summary>
public static partial class PlaceNames
{
    // The planets' codes in the L stations' names, in the order of BoardHubs (Stanton1–4).
    private static readonly string[] PlanetCodes = ["HUR", "CRU", "ARC", "MIC"];

    private static readonly string[] Hubs = [.. BoardHubs.ByArea.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => h.Value)];

    /// <summary>Hubs, L stations per planet, the rest; case-insensitive and culture-free, ordinal on a tie.</summary>
    public static IComparer<string> Order { get; } = Comparer<string>.Create(Compare);

    /// <summary>"HUR-L1 Green Glade Station" → "HUR-L1"; any other name as it is.</summary>
    public static string Short(string name) => LStation().Match(name) is { Success: true } match ? match.Groups["code"].Value : name;

    private static int Compare(string? a, string? b)
    {
        var (ka, kb) = (Key(a ?? ""), Key(b ?? ""));
        var byKey = (ka.Group, ka.Planet, ka.Number).CompareTo((kb.Group, kb.Planet, kb.Number));
        if (byKey != 0)
        {
            return byKey;
        }

        var byName = StringComparer.OrdinalIgnoreCase.Compare(a, b);
        return byName != 0 ? byName : StringComparer.Ordinal.Compare(a, b);
    }

    // Group 0 the hubs in board order, 1 the L stations (planet, number; an unknown planet code after the known ones), 2 the rest.
    private static (int Group, int Planet, int Number) Key(string name)
    {
        var hub = Array.IndexOf(Hubs, name);
        if (hub >= 0)
        {
            return (0, hub, 0);
        }

        if (LStation().Match(name) is { Success: true } match)
        {
            var planet = Array.IndexOf(PlanetCodes, match.Groups["planet"].Value);
            return (1, planet >= 0 ? planet : PlanetCodes.Length, int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return (2, 0, 0);
    }

    // Three capitals, "-L", a number, then the end or a space: "HUR-L1 Green Glade Station".
    [GeneratedRegex(@"^(?<code>(?<planet>[A-Z]{3})-L(?<number>[0-9]{1,3}))(?:\s|\z)", RegexOptions.CultureInvariant)]
    private static partial Regex LStation();
}
