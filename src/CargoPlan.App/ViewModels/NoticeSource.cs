namespace CargoPlan.App.ViewModels;

/// <summary>Who reported a notice line. Each source sets and clears only its own line; lines show in this order (D18).</summary>
public enum NoticeSource
{
    Settings,
    Hotkeys,
    Plan,

    /// <summary>A refused right drag (D24); cleared by the next move or plan.</summary>
    Move,
}
