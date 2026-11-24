using System.Collections.Concurrent;
using System.Text;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.Core.Routing;

/// <summary>
/// The planner's first-fit answers (<see cref="LoadPlanner.Lays"/>) kept across loop rankings (T13.4, D124): after a scan
/// the loops are worked out anew, and nearly every question is one asked before; the same ship and the same stops always
/// get the same answer. Safe for rankings on several threads at once; at <see cref="MaxEntries"/> it starts over.
/// </summary>
public sealed class FitCache
{
    /// <summary>A Hull B ranking asks a few thousand questions; this keeps dozens of rankings and a few MB.</summary>
    public const int MaxEntries = 100_000;

    private readonly ConcurrentDictionary<string, bool> _answers = new(StringComparer.Ordinal);

    public int Count => _answers.Count;

    /// <summary><see cref="LoadPlanner.Lays"/>, asked once per ship and stops.</summary>
    internal bool Lays(Ship ship, IReadOnlyList<MissionStop> stops, Func<bool> lay)
    {
        var key = Key(ship, stops);
        if (_answers.TryGetValue(key, out var known))
        {
            return known;
        }

        if (_answers.Count >= MaxEntries)
        {
            _answers.Clear();
        }

        return _answers[key] = lay();
    }

    // The ship and every stop with its items, in order: what the first fit sees.
    private static string Key(Ship ship, IReadOnlyList<MissionStop> stops)
    {
        var key = new StringBuilder(ship.Id);
        foreach (var stop in stops)
        {
            key.Append('|').Append(stop.Destination);
            foreach (var item in stop.Items)
            {
                key.Append(';').Append(item.Commodity).Append(':').Append(item.Scu).Append(':').Append((int)item.MaxCrateSize);
            }
        }

        return key.ToString();
    }
}
