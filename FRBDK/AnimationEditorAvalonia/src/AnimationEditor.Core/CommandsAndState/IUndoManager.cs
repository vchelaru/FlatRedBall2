using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Unlimited undo/redo history for the animation editor.
    /// Call <see cref="Record"/> after every mutating operation.
    /// </summary>
    public interface IUndoManager
    {
        bool CanUndo { get; }
        bool CanRedo { get; }

        /// <summary>Raised after <see cref="Record"/>, <see cref="Undo"/>, <see cref="Redo"/>, or <see cref="Clear"/>.</summary>
        event Action? StackChanged;

        /// <summary>
        /// Records a command in the undo history and clears the redo stack.
        /// Call this immediately after a mutating operation completes.
        /// </summary>
        void Record(IUndoableCommand cmd);

        /// <summary>No-op when <see cref="CanUndo"/> is false.</summary>
        void Undo();

        /// <summary>No-op when <see cref="CanRedo"/> is false.</summary>
        void Redo();

        void Clear();
    }
}
