namespace CargoPlan.Core.Routing;

/// <summary>
/// One template in one cell, and whether its pickup or drop-off is the board's hub, not the game data (D121), and what
/// the scans saw of it there (T13.3).
/// </summary>
/// <param name="Observed">The scans named this pair for the template; null where only its pools do.</param>
/// <param name="ObservedReward">The reward a scan showed where the data have none (Red Wind).</param>
public sealed record PortCellTemplate(HaulTemplate Template, bool PickupFromHub, bool DropoffFromHub, ObservedEdge? Observed = null, long? ObservedReward = null)
{
    /// <summary>The data's reward, else the one observed; null when neither knows it, never estimated.</summary>
    public long? Reward => Template.Reward ?? ObservedReward;
}

/// <summary>Everything the templates offer from one place to another (design §8.5).</summary>
public sealed class PortCell(IReadOnlyList<PortCellTemplate> templates)
{
    public IReadOnlyList<PortCellTemplate> Templates { get; } = templates;

    public int Count => Templates.Count;

    /// <summary>The sum of the rewards the data or the scans know; null when they know none, never estimated.</summary>
    public long? RewardSum => Templates.Any(t => t.Reward is not null) ? Templates.Sum(t => t.Reward ?? 0) : null;

    /// <summary>How many templates of the cell have no reward, neither in the data nor observed.</summary>
    public int UnknownRewards => Templates.Count(t => t.Reward is null);

    /// <summary>A scan saw at least one template of the cell serve this pair (design §8.3).</summary>
    public bool Observed => Templates.Any(t => t.Observed is not null);

    /// <summary>The most one template of the cell carries.</summary>
    public int MaxScu => Templates.Max(t => t.Template.MaxScu);
}

/// <summary>
/// Pickup × drop-off over the templates a filter lets through (T13.2, design §8.5): a template with n pickups and m
/// drop-offs counts once in each of its n × m cells. Rows and columns are the places that have a cell, in
/// <see cref="PlaceNames.Order"/>.
/// The observed edges (T13.3) add their cells, also for a template without a place in the data; a template with a side
/// still empty after <see cref="BoardHubs"/> and no observed edge has no cell and counts in <see cref="WithoutPlace"/>.
/// </summary>
public sealed class PortMatrix
{
    private readonly Dictionary<(string, string), PortCell> _cells;

    private PortMatrix(IReadOnlyList<string> pickups, IReadOnlyList<string> dropoffs, Dictionary<(string, string), PortCell> cells, int templates, int withoutPlace)
    {
        Pickups = pickups;
        Dropoffs = dropoffs;
        _cells = cells;
        Templates = templates;
        WithoutPlace = withoutPlace;
    }

    public IReadOnlyList<string> Pickups { get; }

    public IReadOnlyList<string> Dropoffs { get; }

    /// <summary>How many templates the filter let through, with a cell or without.</summary>
    public int Templates { get; }

    /// <summary>Templates the filter let through that name no pickup or no drop-off, not even the board's hub, and no scan saw serve a pair.</summary>
    public int WithoutPlace { get; }

    public PortCell? Cell(string pickup, string dropoff) => _cells.GetValueOrDefault((pickup, dropoff));

    /// <param name="observed">The edges and rewards the scans add (T13.3); none without.</param>
    public static PortMatrix Build(IEnumerable<HaulTemplate> templates, RouteFilter filter, ObservedEdges? observed = null)
    {
        observed ??= ObservedEdges.Empty;
        Dictionary<(string, string), List<PortCellTemplate>> cells = [];
        var shown = 0;
        var withoutPlace = 0;
        foreach (var template in templates.Where(filter.Matches))
        {
            shown++;
            var (pickups, pickupFromHub) = BoardHubs.Pickups(template);
            var (dropoffs, dropoffFromHub) = BoardHubs.Dropoffs(template);
            var edges = observed.Of(template);
            if ((pickups.Count == 0 || dropoffs.Count == 0) && edges.Count == 0)
            {
                withoutPlace++;
                continue;
            }

            var reward = observed.RewardOf(template);
            Dictionary<(string, string), PortCellTemplate> entries = [];
            foreach (var pickup in pickups.Distinct(StringComparer.Ordinal))
            {
                // No contract leads back to where it starts.
                foreach (var dropoff in dropoffs.Distinct(StringComparer.Ordinal).Where(d => d != pickup))
                {
                    entries[(pickup, dropoff)] = new PortCellTemplate(template, pickupFromHub, dropoffFromHub, ObservedReward: reward);
                }
            }

            foreach (var edge in edges)
            {
                entries[(edge.From, edge.To)] = entries.TryGetValue((edge.From, edge.To), out var entry)
                    ? entry with { Observed = edge }
                    : new PortCellTemplate(template, false, false, edge, reward);
            }

            foreach (var (pair, entry) in entries)
            {
                if (!cells.TryGetValue(pair, out var list))
                {
                    cells[pair] = list = [];
                }

                list.Add(entry);
            }
        }

        // Hubs, L stations per planet, the rest alphabetical (PlaceNames): culture-free, so every machine orders the same.
        string[] rows = [.. cells.Keys.Select(k => k.Item1).Distinct(StringComparer.Ordinal).Order(PlaceNames.Order)];
        string[] columns = [.. cells.Keys.Select(k => k.Item2).Distinct(StringComparer.Ordinal).Order(PlaceNames.Order)];
        return new PortMatrix(rows, columns, cells.ToDictionary(c => c.Key, c => new PortCell(c.Value)), shown, withoutPlace);
    }
}
