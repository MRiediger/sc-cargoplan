using CargoPlan.Core.Models;

namespace CargoPlan.App.Editor;

/// <summary>One entry of the editor's grouping list (D89): the value for the plan, the text for the player, as <see cref="FillOrderChoice"/>.</summary>
public sealed record GroupingChoice(PlanGrouping Value, string Text);
