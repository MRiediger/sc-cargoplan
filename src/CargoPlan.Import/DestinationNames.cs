using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.Import;

/// <summary>
/// Which names are one place (B1): "Thundering Express" and "Thundering Express Station", "Melodic Fieids" and "Melodic
/// Fields Station". Used where names enter the plan – the reader, the sync, the editor, loading a plan – so the plan
/// holds one name per place and everything behind it compares as before (D40). Core only renames when told to.
/// </summary>
public static class DestinationNames
{
    /// <summary>Suffixes a name may lose without naming another place. Only "Station" is seen in the game so far.</summary>
    private static readonly string[] Suffixes = [ContractText.AnchorForm("Station")];

    /// <summary>
    /// The name as OCR may read it (<see cref="ContractText.AnchorForm"/>, so i/l/1 and o/0 alike), without a last word
    /// that reads as a known suffix. Word by word, so "Everus Harborstation" keeps its "station".
    /// </summary>
    public static string Key(string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var kept = words is [_, .., var last] && Suffixes.Contains(ContractText.AnchorForm(last)) ? words[..^1] : words;
        return ContractText.AnchorForm(string.Concat(kept));
    }

    /// <summary>Names of one place are equal (<see cref="Key"/>).</summary>
    // EqualityComparer<T>.Create (.NET 8+):
    // https://learn.microsoft.com/dotnet/api/system.collections.generic.equalitycomparer-1.create
    public static IEqualityComparer<string> Comparer { get; } = EqualityComparer<string>.Create(
        (first, second) => first is null || second is null ? first == second : string.Equals(Key(first), Key(second), StringComparison.Ordinal),
        name => Key(name).GetHashCode(StringComparison.Ordinal));

    /// <summary>The name a stop keeps: the longer one, it says more ("… Station"); on a tie the first.</summary>
    public static string Preferred(string first, string second) => second.Length > first.Length ? second : first;

    /// <summary>
    /// One name per place in the plan and in the captures about to come in: every stop of the plan whose place has a
    /// preferred name elsewhere is renamed (<see cref="LoadPlanner.RenameStop"/>, listed in <see cref="UnifiedNames.Merged"/>),
    /// the captures take the plan's names. Without a change it returns <paramref name="saved"/> and
    /// <paramref name="captures"/> themselves, so a sync without news stays one (D38).
    /// </summary>
    public static UnifiedNames Unify(SavedPlan saved, Ship ship, IReadOnlyList<ContractCapture> captures)
    {
        string[] planNames = [.. saved.Missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct(StringComparer.Ordinal)];
        var preferred = planNames.Concat(captures.SelectMany(c => c.Stops).Select(s => s.Destination))
            .GroupBy(Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Aggregate(Preferred), StringComparer.Ordinal);
        string NameOf(string name) => preferred[Key(name)];

        (string From, string To)[] merged = [.. planNames.Select(n => (From: n, To: NameOf(n))).Where(r => r.From != r.To)];
        var unified = merged.Aggregate(saved, (current, rename) => LoadPlanner.RenameStop(current, ship, rename.From, rename.To));
        var renamed = captures.Any(c => c.Stops.Any(s => s.Destination != NameOf(s.Destination)))
            ? [.. captures.Select(c => Renamed(c, NameOf))]
            : captures;
        return new(unified, renamed, merged);
    }

    /// <summary>The capture with the preferred names; stops that become one place become one stop.</summary>
    private static ContractCapture Renamed(ContractCapture capture, Func<string, string> nameOf) => capture with
    {
        Stops =
        [
            .. capture.Stops.OrderBy(s => s.Order)
                .GroupBy(s => nameOf(s.Destination), StringComparer.Ordinal)
                .Select((stop, index) => new MissionStop(stop.Key, index + 1, [.. stop.SelectMany(s => s.Items)])),
        ],
    };
}

/// <param name="Merged">Stops of the plan that got another name, for the notice.</param>
public sealed record UnifiedNames(SavedPlan Saved, IReadOnlyList<ContractCapture> Captures, IReadOnlyList<(string From, string To)> Merged);
