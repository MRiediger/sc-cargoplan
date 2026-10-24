namespace CargoPlan.App.ViewModels;

/// <summary>Who reported a notice line. Each source sets and clears only its own line; lines show in this order (D18).</summary>
public enum NoticeSource
{
    Settings,

    /// <summary>ship-outlines.json or some of its entries didn't load (D62); set once at the start.</summary>
    Outlines,

    Hotkeys,
    Plan,

    /// <summary>The chosen ship didn't load and the overlay uses another (T7.2); cleared by a ship change.</summary>
    Ship,

    /// <summary>A plan made for another ship was planned anew (D59); cleared by the next plan or ship change.</summary>
    Replan,

    /// <summary>
    /// Stops of the plan renamed to another name of the same place (B1), on loading or by a sync; cleared by the next plan
    /// and by the next sync that takes something in without a merge.
    /// </summary>
    Stops,

    /// <summary>Why ContractTracker can't be read, or that the sync is paused (T5.4).</summary>
    ContractTracker,

    /// <summary>Contracts deleted in ContractTracker with crates still on board (D39 addendum).</summary>
    DeletedContracts,

    /// <summary>What the last move or Re-plan did, or why a move was refused (D24, D65, D66); cleared by the next move, tick or plan.</summary>
    Move,
}
