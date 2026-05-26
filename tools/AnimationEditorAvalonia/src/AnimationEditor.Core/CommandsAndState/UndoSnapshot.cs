namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Immutable snapshot of the undo and redo stacks captured at a point in time.
    /// Saved per-tab so history is preserved when switching between open files.
    /// </summary>
    public sealed record UndoSnapshot(
        IReadOnlyList<IUndoableCommand> UndoStack,
        IReadOnlyList<IUndoableCommand> RedoStack);
}
