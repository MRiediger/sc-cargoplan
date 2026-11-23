namespace CargoPlan.Core.Routing;

/// <summary>A hauling contract's size class from its title, smallest first (docs/hauling-data-format.md, <c>size</c>).</summary>
public enum HaulSize
{
    /// <summary>"Extra Small".</summary>
    Xs,
    Small,
    Medium,
    Large,
    Bulk,
}
