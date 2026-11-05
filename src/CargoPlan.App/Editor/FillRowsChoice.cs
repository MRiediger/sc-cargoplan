using CargoPlan.Core.Models;

namespace CargoPlan.App.Editor;

/// <summary>One entry of the editor's rows list (D88): the value for the plan, the text for the player, as <see cref="FillOrderChoice"/>.</summary>
public sealed record FillRowsChoice(FillRows Value, string Text);
