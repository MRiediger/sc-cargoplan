using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>A destination with its contract items as entered (D23); <see cref="ToStop"/> gives the planner's stop.</summary>
public sealed record MissionStop(string Destination, int Order, IReadOnlyList<MissionItem> Items)
{
    public int TotalScu => Items.Sum(i => i.Scu);

    /// <summary>The planner's stop, derived mixes split up to <paramref name="cap"/> (the ship's, D70).</summary>
    public Stop ToStop(CrateCap cap) => new(Destination, Order, [.. Items.SelectMany(i => i.ToCargoItems(cap))]);
}

// C# 14 extension members: https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/extension
public static class MissionStopExtensions
{
    /// <param name="stops">The route as entered.</param>
    extension(IEnumerable<MissionStop> stops)
    {
        /// <summary>The route for the planner, every item expanded into its crate sizes up to <paramref name="cap"/>.</summary>
        public IReadOnlyList<Stop> ToStops(CrateCap cap) => [.. stops.Select(s => s.ToStop(cap))];
    }
}
