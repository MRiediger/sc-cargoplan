namespace CargoPlan.Core.Routing;

/// <summary>One template in one cell, and whether its pickup or drop-off is the board's hub, not the game data (D121).</summary>
public sealed record PortCellTemplate(HaulTemplate Template, bool PickupFromHub, bool DropoffFromHub);

/// <summary>Everything the templates offer from one place to another (design §8.5).</summary>
public sealed class PortCell(IReadOnlyList<PortCellTemplate> templates)
{
    public IReadOnlyList<PortCellTemplate> Templates { get; } = templates;

    public int Count => Templates.Count;

    /// <summary>The sum of the rewards the data know; null when they know none (Red Wind), never estimated.</summary>
    public long? RewardSum => Templates.Any(t => t.Template.Reward is not null) ? Templates.Sum(t => t.Template.Reward ?? 0) : null;

    /// <summary>How many templates of the cell have no reward in the data.</summary>
    public int UnknownRewards => Templates.Count(t => t.Template.Reward is null);

    /// <summary>The most one template of the cell carries.</summary>
    public int MaxScu => Templates.Max(t => t.Template.MaxScu);
}

/// <summary>
/// Pickup × drop-off over the templates a filter lets through (T13.2, design §8.5): a template with n pickups and m
/// drop-offs counts once in each of its n × m cells. Rows and columns are the places that have a cell, in
/// <see cref="PlaceNames.Order"/>.
/// A template with a side still empty after <see cref="BoardHubs"/> has no cell and counts in <see cref="WithoutPlace"/>.
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

    /// <summary>Templates the filter let through that name no pickup or no drop-off, not even the board's hub.</summary>
    public int WithoutPlace { get; }

    public PortCell? Cell(string pickup, string dropoff) => _cells.GetValueOrDefault((pickup, dropoff));

    public static PortMatrix Build(IEnumerable<HaulTemplate> templates, RouteFilter filter)
    {
        Dictionary<(string, string), List<PortCellTemplate>> cells = [];
        var shown = 0;
        var withoutPlace = 0;
        foreach (var template in templates.Where(filter.Matches))
        {
            shown++;
            var (pickups, pickupFromHub) = BoardHubs.Pickups(template);
            var (dropoffs, dropoffFromHub) = BoardHubs.Dropoffs(template);
            if (pickups.Count == 0 || dropoffs.Count == 0)
            {
                withoutPlace++;
                continue;
            }

            var entry = new PortCellTemplate(template, pickupFromHub, dropoffFromHub);
            foreach (var pickup in pickups.Distinct(StringComparer.Ordinal))
            {
                // No contract leads back to where it starts.
                foreach (var dropoff in dropoffs.Distinct(StringComparer.Ordinal).Where(d => d != pickup))
                {
                    if (!cells.TryGetValue((pickup, dropoff), out var list))
                    {
                        cells[(pickup, dropoff)] = list = [];
                    }

                    list.Add(entry);
                }
            }
        }

        // Hubs, L stations per planet, the rest alphabetical (PlaceNames): culture-free, so every machine orders the same.
        string[] rows = [.. cells.Keys.Select(k => k.Item1).Distinct(StringComparer.Ordinal).Order(PlaceNames.Order)];
        string[] columns = [.. cells.Keys.Select(k => k.Item2).Distinct(StringComparer.Ordinal).Order(PlaceNames.Order)];
        return new PortMatrix(rows, columns, cells.ToDictionary(c => c.Key, c => new PortCell(c.Value)), shown, withoutPlace);
    }
}
