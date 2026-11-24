using CargoPlan.Core.Routing;

namespace CargoPlan.App.Scanning;

/// <summary>What a scan did to the route (T13.6, D125), for the end of its notice; the route is "plan" there (U4).</summary>
public abstract record RouteNote
{
    private RouteNote()
    {
    }

    /// <summary>" · plan: bulk ✓ 2/10": the scan filled a placeholder of <paramref name="Kind"/>.</summary>
    public sealed record Filled(RecipeKind Kind, int Count, int Total) : RouteNote;

    /// <summary>" · plan ✓": the contract is in the route already.</summary>
    public sealed record AlreadyFilled : RouteNote;

    /// <summary>" · not in plan (extra)": accepted, no placeholder took it; it counts for the route (U9.2).</summary>
    public sealed record Extra : RouteNote;

    /// <summary>" · plan: bulk": an offer of an open placeholder's template, found at the terminal (E(a) of T13.6).</summary>
    public sealed record Open(RecipeKind Kind) : RouteNote;
}
