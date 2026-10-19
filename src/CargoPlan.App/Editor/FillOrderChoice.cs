using CargoPlan.Core.Models;

namespace CargoPlan.App.Editor;

/// <summary>
/// One entry of the editor's fill order list (T4.5): the value for the plan, the text for the player. A record, because
/// an enum shows its code name and a tuple has no properties to bind.
/// </summary>
public sealed record FillOrderChoice(FillOrder Value, string Text);
