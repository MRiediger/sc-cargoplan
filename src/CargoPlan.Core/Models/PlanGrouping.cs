namespace CargoPlan.Core.Models;

/// <summary>
/// Whether a plan lays each destination as a block of its own in a rack it shares (D89). Stored in the plan, as
/// <see cref="FillOrder"/> and <see cref="FillRows"/>; together with those it says which way the blocks run.
/// </summary>
public enum PlanGrouping
{
    /// <summary>Largest crates first, across the destinations (D64).</summary>
    None,

    /// <summary>In a rack with more than one destination, each gets its section: a block of whole rows.</summary>
    ByDestination,
}
