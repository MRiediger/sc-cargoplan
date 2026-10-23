namespace CargoPlan.App.ViewModels;

/// <summary>Who reported a notice line. Each source sets and clears only its own line; lines show in this order (D18).</summary>
public enum NoticeSource
{
    Settings,
    Hotkeys,
    Plan,

    /// <summary>The chosen ship didn't load and the overlay uses another (T7.2); cleared by a ship change.</summary>
    Ship,

    /// <summary>A plan made for another ship was planned anew (D59); cleared by the next plan or ship change.</summary>
    Replan,

    /// <summary>Why ContractTracker can't be read, or that the sync is paused (T5.4).</summary>
    ContractTracker,

    /// <summary>Contracts deleted in ContractTracker with crates still on board (D39 addendum).</summary>
    DeletedContracts,

    /// <summary>A refused right drag (D24); cleared by the next move or plan.</summary>
    Move,
}
