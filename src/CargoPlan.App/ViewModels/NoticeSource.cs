namespace CargoPlan.App.ViewModels;

/// <summary>Who reported a notice line. Each source sets and clears only its own line; lines show in this order (D18).</summary>
public enum NoticeSource
{
    Settings,
    Hotkeys,
    Plan,

    /// <summary>Why ContractTracker can't be read, or that the sync is paused (T5.4).</summary>
    ContractTracker,

    /// <summary>Contracts deleted in ContractTracker with crates still on board (D39 addendum).</summary>
    DeletedContracts,

    /// <summary>A refused right drag (D24); cleared by the next move or plan.</summary>
    Move,
}
