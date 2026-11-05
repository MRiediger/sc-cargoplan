namespace CargoPlan.App.Editor;

/// <summary>One entry of the editor's section gap list (D89): the value for the plan, the text for the player, as <see cref="FillOrderChoice"/>.</summary>
public sealed record SectionGapChoice(int Value, string Text);
