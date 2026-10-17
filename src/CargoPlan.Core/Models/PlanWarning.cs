namespace CargoPlan.Core.Models;

/// <summary>A planner hint for one rack. <see cref="Message"/> reads on its own, without the rack.</summary>
public sealed record PlanWarning(string RackId, string Message);
