using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Planning;

/// <summary>Decides which rack each stop of a route goes into.</summary>
public static class LoadPlanner
{
    /// <summary>
    /// Plans the stops into the ship's racks. Racks fill from the front in route order, one rack per stop
    /// while racks last. With more stops than racks, only route-neighbours share a rack, spread evenly,
    /// and every shared rack gets a warning. A stop is never split across racks.
    /// </summary>
    /// <exception cref="ArgumentException">Two stops share an order or a destination.</exception>
    /// <exception cref="PlanException">The stops don't fit without splitting a stop.</exception>
    public static LoadPlan Plan(Ship ship, IReadOnlyList<Stop> stops)
    {
        EnsureUnique(stops);

        Stop[] route = [.. stops.OrderBy(s => s.Order)];
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];

        List<Placement> placements = [];
        List<string> warnings = [];
        var first = 0;
        foreach (var (rack, size) in racks.Zip(Partition(ship, racks, route)))
        {
            var group = route[first..(first + size)];
            first += size;

            placements.AddRange(group.SelectMany(stop =>
                stop.Items.Select(item => new Placement(stop.Destination, rack.Id, item))));
            if (group.Length > 1)
            {
                warnings.Add(Invariant(
                    $"Rack {rack.Index} shared by {string.Join(", ", group.Select(s => s.Destination))} – identify crates by count"));
            }
        }

        return new LoadPlan(ship.Id, placements, warnings);
    }

    private static void EnsureUnique(IReadOnlyList<Stop> stops)
    {
        if (stops.GroupBy(s => s.Order).FirstOrDefault(g => g.Count() > 1) is { } order)
        {
            throw new ArgumentException(Invariant($"Order {order.Key} is used by more than one stop."), nameof(stops));
        }

        // Case-insensitive like the mission file: "Riker" and "riker" are the same cargo lift.
        if (stops.GroupBy(s => s.Destination, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } destination)
        {
            throw new ArgumentException($"Destination '{destination.Key}' is used by more than one stop.", nameof(stops));
        }
    }

    /// <summary>
    /// Splits the route into contiguous groups, group r → rack r, and returns the group sizes.
    /// Dynamic programming over (first i stops, first r racks). Best split: fewest stops in the fullest
    /// rack, then the smallest sum of squared group sizes (spreads sharing evenly instead of stacking it).
    /// </summary>
    private static int[] Partition(Ship ship, Rack[] racks, Stop[] route)
    {
        var n = route.Length;
        var prefixScu = new int[n + 1];
        for (var i = 0; i < n; i++)
        {
            prefixScu[i + 1] = prefixScu[i] + route[i].TotalScu;
        }

        // best[i, r]: score of stops [0, i) in racks [0, r), no rack empty; null = impossible.
        var best = new (int MaxStops, int SumOfSquares)?[n + 1, racks.Length + 1];
        var groupStart = new int[n + 1, racks.Length + 1];
        best[0, 0] = (0, 0);

        for (var r = 1; r <= racks.Length; r++)
        {
            for (var i = r; i <= n; i++)
            {
                // Smallest last group first; ties keep it, so sharing ends up near the front.
                for (var j = i - 1; j >= r - 1; j--)
                {
                    if (prefixScu[i] - prefixScu[j] > racks[r - 1].CapacityScu)
                    {
                        break;
                    }

                    if (best[j, r - 1] is not { } previous)
                    {
                        continue;
                    }

                    var size = i - j;
                    var score = (Math.Max(previous.MaxStops, size), previous.SumOfSquares + (size * size));
                    if (best[i, r] is not { } current || score.CompareTo(current) < 0)
                    {
                        best[i, r] = score;
                        groupStart[i, r] = j;
                    }
                }
            }
        }

        // Fewest racks on a tie, so free racks stay at the back.
        var rackCount = Enumerable.Range(0, racks.Length + 1)
            .Where(r => best[n, r] is not null)
            .OrderBy(r => best[n, r])
            .DefaultIfEmpty(-1)
            .First();
        if (rackCount < 0)
        {
            throw Unplannable(ship, racks, route, prefixScu, best);
        }

        var sizes = new int[rackCount];
        for (int r = rackCount, i = n; r > 0; r--)
        {
            sizes[r - 1] = i - groupStart[i, r];
            i = groupStart[i, r];
        }

        return sizes;
    }

    /// <summary>Names the first stop that breaks the plan and why.</summary>
    private static PlanException Unplannable(
        Ship ship, Rack[] racks, Stop[] route, int[] prefixScu, (int, int)?[,] best)
    {
        // Feasibility only gets lost while adding stops, so the first unplaceable prefix pins the culprit.
        var count = Enumerable.Range(1, route.Length)
            .First(i => Enumerable.Range(0, racks.Length + 1).All(r => best[i, r] is null));
        var stop = route[count - 1];
        var largestRack = racks.Select(r => r.CapacityScu).DefaultIfEmpty(0).Max();

        return new PlanException(
            stop.TotalScu > largestRack
                ? Invariant($"Rack capacity exceeded: stop '{stop.Destination}' needs {stop.TotalScu} SCU, the largest rack holds {largestRack} SCU.")
            : prefixScu[count] > ship.TotalScu
                ? Invariant($"Ship capacity exceeded: stops up to '{stop.Destination}' need {prefixScu[count]} SCU, ship '{ship.Id}' holds {ship.TotalScu} SCU.")
            : Invariant($"Rack capacity exceeded: stop '{stop.Destination}' ({stop.TotalScu} SCU) doesn't fit into the remaining racks without splitting a stop."));
    }
}
