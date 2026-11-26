namespace CargoPlan.Core.Routing;

/// <summary>What a node of the map stands for (T13.7): a board's hub, any other station, or a planet's surface.</summary>
public enum MapNodeKind
{
    Hub,
    Station,
    Surface,
}

/// <summary>
/// One node of the map (T13.7, D128): a station, or every place on a planet and its moons as one node at the planet
/// (K1 of the approval: "Hurston (surface)"). Coordinates in metres, the system's.
/// </summary>
/// <param name="Key">The station's position name; for a surface the label.</param>
/// <param name="Label">"HUR-L1", "Everus Harbor", "Hurston (surface)".</param>
/// <param name="Places">The positions it stands for, in place order: the station itself, or the surface places asked for.</param>
public sealed record MapNode(string Key, string Label, MapNodeKind Kind, double X, double Y, double Z, IReadOnlyList<string> Places);

/// <summary>Places to the map's nodes (T13.7): stations stay, the rest joins the nearest planet.</summary>
public static class MapNodes
{
    private const string PlanetType = "Planet";

    private const string StationType = "Manmade";

    /// <summary>A station of the starmap: <c>Manmade</c> or <c>Manmade_VisibleOnInteraction</c>.</summary>
    public static bool IsStation(StarPosition position) => position.Type.StartsWith(StationType, StringComparison.Ordinal);

    /// <summary>"Hurston" → "Hurston (surface)".</summary>
    public static string SurfaceOf(string planet) => $"{planet} (surface)";

    /// <summary>
    /// Each place of <paramref name="places"/> with a position in <paramref name="system"/> to its node: a station to its
    /// own, any other place (a landing zone, an outpost, a moon, the planet itself) to the node of the nearest planet; a
    /// system without planets keeps every place a node of its own. Places of one node share one instance.
    /// </summary>
    public static IReadOnlyDictionary<string, MapNode> For(IEnumerable<string> places, Positions positions, StarSystem system)
    {
        StarPosition[] planets = [.. positions.All.Where(p => p.System == system && p.Type == PlanetType)];
        var byKey = new Dictionary<string, (StarPosition At, MapNodeKind Kind, string Label, SortedSet<string> Places)>(StringComparer.Ordinal);
        var keyOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var place in places.Distinct(StringComparer.Ordinal))
        {
            if (positions.Find(system, place) is not { } position)
            {
                continue;
            }

            (string Key, StarPosition At, MapNodeKind Kind, string Label) node;
            if (IsStation(position) || planets.Length == 0)
            {
                var kind = BoardHubs.ByArea.Values.Contains(place) ? MapNodeKind.Hub : MapNodeKind.Station;
                node = (place, position, kind, PlaceNames.Short(place));
            }
            else
            {
                var planet = Nearest(position, planets);
                node = (SurfaceOf(planet.Name), planet, MapNodeKind.Surface, SurfaceOf(planet.Name));
            }

            if (!byKey.TryGetValue(node.Key, out var entry))
            {
                byKey[node.Key] = entry = (node.At, node.Kind, node.Label, new SortedSet<string>(PlaceNames.Order));
            }

            entry.Places.Add(place);
            keyOf[place] = node.Key;
        }

        var nodes = byKey.ToDictionary(e => e.Key, e => new MapNode(e.Key, e.Value.Label, e.Value.Kind, e.Value.At.X, e.Value.At.Y, e.Value.At.Z, [.. e.Value.Places]), StringComparer.Ordinal);
        return keyOf.OrderBy(k => k.Key, PlaceNames.Order).ToDictionary(k => k.Key, k => nodes[k.Value], StringComparer.Ordinal);
    }

    // The planet with the least distance, the first in place order on a tie.
    private static StarPosition Nearest(StarPosition position, StarPosition[] planets) =>
        planets.OrderBy(p => Positions.DistanceGm(position, p)).ThenBy(p => p.Name, PlaceNames.Order).First();
}
