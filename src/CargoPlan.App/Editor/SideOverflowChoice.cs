namespace CargoPlan.App.Editor;

/// <summary>One entry of the editor's side overflow list (D116): whether a destination crosses to the other side, and the text.</summary>
public sealed record SideOverflowChoice(bool Value, string Text);
