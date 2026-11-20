using CargoPlan.Core.Models;

namespace CargoPlan.App.Editor;

/// <summary>One entry of the editor's sides list (D116): the value for the plan, the text for the player, as <see cref="FillOrderChoice"/>.</summary>
public sealed record SidesChoice(Sides Value, string Text);
