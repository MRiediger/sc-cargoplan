using CargoPlan.Core.Models;

namespace CargoPlan.Core.Plans;

/// <summary>File format of a planner hint (D7). Stored, not derived again: the derivation is the planner's own (D18).</summary>
internal sealed record PlanWarningDto(string RackId, string Message)
{
    public PlanWarning ToPlanWarning() => new(RackId, Message);

    public static PlanWarningDto FromPlanWarning(PlanWarning warning) => new(warning.RackId, warning.Message);
}
