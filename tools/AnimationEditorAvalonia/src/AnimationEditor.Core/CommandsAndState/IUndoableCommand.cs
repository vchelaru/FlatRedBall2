namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// A reversible project mutation. The command owns the <em>do</em> as well as the
    /// undo/redo: <see cref="Do"/> performs the mutation, so the only way to change
    /// project state is to construct a command and run it through
    /// <see cref="IUndoManager.Execute"/>. That makes "forgetting to record undo"
    /// structurally impossible rather than convention-enforced.
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// Short human-readable label shown in the History panel (e.g. "Add Frame",
        /// "Rename 'Walk' → 'Run'"). Should be set at construction time so it is
        /// readable before and after <see cref="Do"/> is called.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Performs the mutation for the first time. Returns <c>false</c> when the command
        /// turned out to be a no-op (e.g. a reorder that produced an identical list), so
        /// <see cref="IUndoManager.Execute"/> can skip recording an empty undo entry.
        /// </summary>
        bool Do();

        void Undo();

        /// <summary>
        /// Re-applies the mutation after an <see cref="Undo"/>. Defaults to <see cref="Do"/>,
        /// which is correct for any command whose redo is identical to its first do; commands
        /// that replay a captured "after" snapshot (reorder, bulk edit) override this.
        /// </summary>
        void Redo() => Do();

        /// <summary>
        /// Identifies the logical edit target this command mutates (e.g. one rectangle's props,
        /// one frame's offset). When two commands run back-to-back through
        /// <see cref="IUndoManager.Execute"/> share the same non-null group and coalescing hasn't
        /// been sealed since (see <see cref="IUndoManager.SealCoalescing"/>), they merge into a
        /// single undo entry instead of stacking one entry per call — e.g. every keystroke or
        /// mouse-wheel tick while editing one NumericUpDown collapses to one entry, sealed on
        /// focus loss. Null (the default) never coalesces.
        /// </summary>
        string? CoalesceGroup => null;

        /// <summary>
        /// Called on the command that was just executed (<c>this</c>, already applied via
        /// <see cref="Do"/>) when it shares <see cref="CoalesceGroup"/> with the command still on
        /// top of the undo stack (<paramref name="previous"/>). Returns the single command that
        /// should replace both on the stack — typically <paramref name="previous"/>'s original
        /// "before" state combined with this command's already-applied "after" state, so undoing
        /// the merged entry restores the value from before the whole coalesced edit, not just the
        /// last increment. Null when that "before" equals the "after" (wheel up, then down): the whole
        /// edit changed nothing and leaves no entry. Only called when <see cref="CoalesceGroup"/> is non-null.
        /// </summary>
        IUndoableCommand? CoalesceWith(IUndoableCommand previous) => this;
    }
}
