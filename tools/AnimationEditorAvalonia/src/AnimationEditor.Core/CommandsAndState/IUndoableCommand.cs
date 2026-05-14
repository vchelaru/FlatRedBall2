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
    }
}
