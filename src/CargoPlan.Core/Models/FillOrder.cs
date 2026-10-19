namespace CargoPlan.Core.Models;

/// <summary>
/// Which end of a rack's length the crate layout fills from (D21). Stored in the plan, so everyone loading it sees the
/// same layout (D24).
/// </summary>
public enum FillOrder
{
    BowFirst,
    SternFirst,
}
