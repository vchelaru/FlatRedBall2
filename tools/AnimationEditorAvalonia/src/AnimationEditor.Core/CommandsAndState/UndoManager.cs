using System;
using System.Collections.Generic;
using System.Linq;

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
        private SaveState _saveState = SaveState.Unsaved;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public SaveState SaveState => _saveState;

        /// <inheritdoc cref="IUndoManager.UndoHistory"/>
        public IReadOnlyList<IUndoableCommand> UndoHistory => _undoStack.Reverse().ToList();

        /// <inheritdoc cref="IUndoManager.RedoHistory"/>
        public IReadOnlyList<IUndoableCommand> RedoHistory => _redoStack.ToList();

        /// <summary>Raised after <see cref="Execute"/>, <see cref="Record"/>, <see cref="Undo"/>, <see cref="Redo"/>, <see cref="Clear"/>, <see cref="MarkSaved"/>, or <see cref="MarkSaveFailed"/>.</summary>
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
            _saveState = SaveState.Unsaved;
            StackChanged?.Invoke();
        }

        /// <inheritdoc cref="IUndoManager.TakeSnapshot"/>
        public UndoSnapshot TakeSnapshot() =>
            new(_undoStack.Reverse().ToList(), _redoStack.ToList());

        /// <inheritdoc cref="IUndoManager.RestoreSnapshot"/>
        public void RestoreSnapshot(UndoSnapshot snapshot)
        {
            _undoStack.Clear();
            // UndoStack is oldest-first; pushing in that order leaves newest on top.
            foreach (var cmd in snapshot.UndoStack)
                _undoStack.Push(cmd);
            _redoStack.Clear();
            // RedoStack is stored LIFO (next-to-redo first); push in reverse to restore that order.
            for (int i = snapshot.RedoStack.Count - 1; i >= 0; i--)
                _redoStack.Push(snapshot.RedoStack[i]);
            StackChanged?.Invoke();
        }

        /// <summary>Marks the last auto-save as successful, setting <see cref="SaveState"/> to <see cref="SaveState.AutoSaveOn"/>.</summary>
        public void MarkSaved()
        {
            _saveState = SaveState.AutoSaveOn;
            StackChanged?.Invoke();
        }

        /// <summary>Marks the last auto-save as failed, setting <see cref="SaveState"/> to <see cref="SaveState.Failed"/>.</summary>
        public void MarkSaveFailed()
        {
            _saveState = SaveState.Failed;
            StackChanged?.Invoke();
        }
    }
}
