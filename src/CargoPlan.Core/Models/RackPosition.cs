namespace CargoPlan.Core.Models;

/// <summary>
/// Where a rack sits in player words ("port upper"). The geometry is <see cref="Rack.Origin"/>; neither is derived
/// from the other (D14). The planner uses neither, it works in rack order.
/// </summary>
public sealed record RackPosition(RackSide Side, RackLevel Level);
