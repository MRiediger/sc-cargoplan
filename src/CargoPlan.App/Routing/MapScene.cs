using CargoPlan.Core.Routing;

namespace CargoPlan.App.Routing;

/// <summary>
/// What the map view draws (T13.7, D128): the graph of the matrix under the filter, every node of the system for the
/// layout (so a filter hides nodes but moves none), the hub the recipe shows (a ring), and the tool tips, built when
/// the pointer asks. A new scene means a new graph; zoom and pan keep it (K4).
/// </summary>
/// <param name="Universe">Every node the system's templates can name: the layout is made of these.</param>
/// <param name="Hub">The recipe's hub (routes.hub or the session's, D123 addendum), ringed; null outside Stanton.</param>
public sealed record MapScene(
    RouteMap Graph,
    IReadOnlyList<MapNode> Universe,
    StarSystem System,
    string? Hub,
    Func<MapEdge, string> EdgeToolTip,
    Func<MapNode, string> NodeToolTip);
