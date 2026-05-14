using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Unlimited undo/redo history for the animation editor.
    /// <see cref="Execute"/> is the standard path for every project mutation;
    /// <see cref="Record"/> exists only for interactive-gesture commands.
    /// </summary>
    public interface IUndoManager
    {
        bool CanUndo { get; }
        bool CanRedo { get; }

        /// <summary>
        /// Undo-stack entries in chronological order (oldest first, newest last).
        /// Rebuilt on every <see cref="StackChanged"/> event.
        /// </summary>
        IReadOnlyList<IUndoableCommand> UndoHistory { get; }

        /// <summary>
        /// Redo-stack entries in the order they would be re-applied (first-to-redo first,
        /// last-to-redo last). Rebuilt on every <see cref="StackChanged"/> event.
        /// </summary>
        IReadOnlyList<IUndoableCommand> RedoHistory { get; }

        /// <summary>
        /// Current save state of the open document.
        /// Updated by <see cref="MarkSaved"/>, <see cref="MarkSaveFailed"/>, and <see cref="Clear"/>.
        /// </summary>
        SaveState SaveState { get; }

        /// <summary>Raised after <see cref="Execute"/>, <see cref="Record"/>, <see cref="Undo"/>, <see cref="Redo"/>, <see cref="Clear"/>, <see cref="MarkSaved"/>, or <see cref="MarkSaveFailed"/>.</summary>
        event Action? StackChanged;

        /// <summary>
        /// Runs <see cref="IUndoableCommand.Do"/> and, if it reported a real change,
        /// records the command in the undo history (clearing the redo stack).
        /// This is the chokepoint for project mutation — call it instead of mutating
        /// state directly and then calling <see cref="Record"/>.
        /// </summary>
        void Execute(IUndoableCommand cmd);

        /// <summary>
        /// Records an <em>already-applied</em> command in the undo history and clears the
        /// redo stack. Reserved for interactive-gesture commands (shape drag/resize, region
        /// drag) whose mutation is applied incrementally during the gesture and committed on
        /// release — for those, <see cref="Execute"/> does not fit. Everything else must go
        /// through <see cref="Execute"/>.
        /// </summary>
        void Record(IUndoableCommand cmd);

        /// <summary>No-op when <see cref="CanUndo"/> is false.</summary>
        void Undo();

        /// <summary>No-op when <see cref="CanRedo"/> is false.</summary>
        void Redo();

        void Clear();

        /// <summary>Marks the last auto-save as successful, setting <see cref="SaveState"/> to <see cref="SaveState.AutoSaveOn"/>.</summary>
        void MarkSaved();

        /// <summary>Marks the last auto-save as failed, setting <see cref="SaveState"/> to <see cref="SaveState.Failed"/>.</summary>
        void MarkSaveFailed();
    }
}
