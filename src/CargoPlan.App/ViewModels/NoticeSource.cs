namespace CargoPlan.App.ViewModels;

/// <summary>Who reported a notice line. Each source sets and clears only its own line; lines show in this order (D18).</summary>
public enum NoticeSource
{
    Settings,

    /// <summary>
    /// A preset applied, a look file imported or exported, or why not (K4.3); its own line, because a settings save
    /// rewrites <see cref="Settings"/>. Cleared by the next change in the settings window and when it closes.
    /// </summary>
    Look,

    /// <summary>ship-outlines.json or some of its entries didn't load (D62); set once at the start.</summary>
    Outlines,

    /// <summary>The first hologram that couldn't be shown in this run, and why (D63); cleared when one shows or the look is no hologram.</summary>
    Holo,

    Hotkeys,
    Plan,

    /// <summary>The chosen ship didn't load and the overlay uses another (T7.2); cleared by a ship change.</summary>
    Ship,

    /// <summary>A plan made for another ship was planned anew (D59); cleared by the next plan or ship change.</summary>
    Replan,

    /// <summary>A derived crate mix on board is split into smaller crates than its contract allows (D70); follows the plan.</summary>
    CrateCap,

    /// <summary>
    /// Stops of the plan renamed to another name of the same place (B1), on loading or by a sync; cleared by the next plan
    /// and by the next sync that takes something in without a merge.
    /// </summary>
    Stops,

    /// <summary>
    /// ContractTracker pickups of a loaded plan cut to their names (B6); cleared by the next plan and by the next sync that
    /// takes something in.
    /// </summary>
    Pickups,

    /// <summary>Why ContractTracker can't be read, or that the sync is paused (T5.4).</summary>
    ContractTracker,

    /// <summary>What the last move or Re-plan did, or why a move was refused (D24, D65, D66); cleared by the next move, tick or plan.</summary>
    Move,
}
