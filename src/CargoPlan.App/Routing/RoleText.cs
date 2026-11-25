using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static System.FormattableString;

namespace CargoPlan.App.Routing;

/// <summary>
/// One line per contract role in the legend's look: the recipe (T13.3b), the loops' tool tips (T13.4) and the route's
/// placeholders (T13.6) read alike, "Bulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · 41–432 SCU in 16s · HUR-L1".
/// </summary>
internal static class RoleText
{
    public static readonly IReadOnlyDictionary<RecipeKind, string> KindWords = new Dictionary<RecipeKind, string>
    {
        [RecipeKind.Bulk] = "Bulk",
        [RecipeKind.Direct] = "Direct",
        [RecipeKind.Small] = "Small",
        [RecipeKind.Return] = "Back",
    };

    /// <summary>"accept at MIC-L1, collect at MIC-L2, MIC-L3" for a way back (one contract, gate T13.4); null for any other role.</summary>
    public static string? Accept(string? acceptAt, IReadOnlyList<string>? collectAt) => acceptAt is not { } at
        ? null
        : collectAt is { Count: > 0 } collect
            ? $"accept at {PlaceNames.Short(at)}, collect at {string.Join(", ", collect.Select(PlaceNames.Short))}"
            : $"accept at {PlaceNames.Short(at)}";

    /// <summary>"325,250 aUEC" from the data, "532,500 aUEC observed" from a scan, "–" without.</summary>
    public static string Reward(long? data, long? seen) => (data, seen) switch
    {
        ({ } fixedReward, _) => Invariant($"{fixedReward:N0} aUEC"),
        (null, { } observed) => Invariant($"{observed:N0} aUEC observed"),
        _ => "–",
    };

    /// <summary>"1 h 20 min", "48 min": a round's time in whole minutes, cut, not rounded (Finish plan's notice, the loops' runs).</summary>
    public static string Duration(TimeSpan time) =>
        Invariant($"{(time.TotalHours >= 1 ? $"{(int)time.TotalHours} h " : "")}{time.Minutes} min");

    /// <param name="commodities">Named where the player looks for the contract at the terminal (the route); null in the recipe.</param>
    /// <param name="where">The places, or where a way back is accepted.</param>
    public static string Line(
        RecipeKind kind, string title, string reward, IReadOnlyList<string>? commodities, int minScu, int maxScu, CrateSize container, string where) =>
        string.Join(" · ", [
            KindWords[kind], title, reward,
            .. commodities is { Count: > 0 } ? [string.Join(", ", commodities)] : Array.Empty<string>(),
            Invariant($"{minScu}–{maxScu} SCU in {(int)container}s"), where,
        ]);
}
