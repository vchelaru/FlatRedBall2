namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Immutable snapshot of the undo and redo stacks captured at a point in time.
    /// Saved per-tab so history is preserved when switching between open files.
    /// </summary>
    public sealed record UndoSnapshot(
        IReadOnlyList<UndoEntry> UndoStack,
        IReadOnlyList<UndoEntry> RedoStack);

    /// <summary>A command of the history, and the <c>.tsx</c> state around it. Null in another document.</summary>
    /// <param name="TsxBefore">See <see cref="IProjectManager.CaptureTsxState"/>.</param>
    /// <param name="TsxAfter">As of the undo, for the redo.</param>
    public sealed record UndoEntry(IUndoableCommand Command, object? TsxBefore, object? TsxAfter = null);
}
