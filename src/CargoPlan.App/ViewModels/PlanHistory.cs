using CargoPlan.Core.Plans;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// The overlay's undo and redo (K2.4): snapshots of the whole plan, not diffs, and not saved, so a restart starts without.
/// An entry is the plan to go back to (<see cref="Entry.Target"/>) and the plan that came right after it
/// (<see cref="Entry.Reference"/>): what changed between that and the current plan without being a step (ticks, syncs)
/// carries over to the target (Core's <c>CarryTicks</c> and <c>Resync</c>). The plans are immutable records, so a snapshot
/// shares every crate it didn't change.
/// </summary>
public sealed class PlanHistory
{
    /// <summary>
    /// Steps kept per stack, the oldest dropped first. Measured (K2.4): a whole plan of 19–23 crates holds 8–11 KB, a move
    /// adds about 2 KB to the plan before; 100 steps are at most about 1 MB, typically some 200 KB, and more undos than
    /// that nobody takes at the cargo lift.
    /// </summary>
    public const int MaxSteps = 100;

    public sealed record Entry(SavedPlan Target, SavedPlan Reference);

    // Lists as stacks: the end is the top, the start the oldest, which goes once MaxSteps is passed.
    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>A step of the player: <paramref name="before"/> can be gone back to; a new step drops what was undone.</summary>
    public void Record(SavedPlan before, SavedPlan after)
    {
        PushUndo(before, after);
        _redo.Clear();
    }

    public Entry PopUndo() => Pop(_undo);

    public Entry PopRedo() => Pop(_redo);

    /// <summary>Undone: the plan before the undo can be gone forward to again.</summary>
    public void PushRedo(SavedPlan target, SavedPlan reference) => Push(_redo, new(target, reference));

    /// <summary>Redone: the plan before the redo can be gone back to again; the redo stack stays.</summary>
    public void PushUndo(SavedPlan target, SavedPlan reference) => Push(_undo, new(target, reference));

    /// <summary>Both stacks empty, after a ship change: the snapshots are another ship's layouts.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private static void Push(List<Entry> stack, Entry entry)
    {
        stack.Add(entry);
        if (stack.Count > MaxSteps)
        {
            stack.RemoveAt(0);
        }
    }

    private static Entry Pop(List<Entry> stack)
    {
        var entry = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return entry;
    }
}
