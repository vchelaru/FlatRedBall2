using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Manages unlimited undo/redo history for the animation editor.
    /// Mutate project state by passing a command to <see cref="Execute"/>.
    /// </summary>
    public class UndoManager : IUndoManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>Raised after <see cref="Execute"/>, <see cref="Undo"/>, <see cref="Redo"/>, or <see cref="Clear"/>.</summary>
        public event Action? StackChanged;

        /// <inheritdoc cref="IUndoManager.Execute"/>
        public void Execute(IUndoableCommand cmd)
        {
            if (cmd.Do())
                Record(cmd);
        }

        /// <inheritdoc cref="IUndoManager.Record"/>
        public void Record(IUndoableCommand cmd)
        {
            _undoStack.Push(cmd);
            _redoStack.Clear();
            StackChanged?.Invoke();
        }

        /// <summary>No-op when <see cref="CanUndo"/> is false.</summary>
        public void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);
            StackChanged?.Invoke();
        }

        /// <summary>No-op when <see cref="CanRedo"/> is false.</summary>
        public void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redoStack.Pop();
            cmd.Redo();
            _undoStack.Push(cmd);
            StackChanged?.Invoke();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StackChanged?.Invoke();
        }
    }
}
