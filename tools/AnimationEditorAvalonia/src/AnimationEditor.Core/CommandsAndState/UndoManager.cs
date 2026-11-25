using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Manages unlimited undo/redo history for the animation editor.
    /// Mutate project state by passing a command to <see cref="Execute"/>.
    /// </summary>
    /// <param name="projectManager">
    /// Whose native-tsx state every animation edit's undo and redo bring back: which tile each animation is on is
    /// decided at every save, so undoing only the edit would not give a tile back. Null when only sequences are edited.
    /// </param>
    public class UndoManager(IProjectManager? projectManager = null) : IUndoManager
    {
        private readonly Stack<UndoEntry> _undoStack = new();
        private readonly Stack<UndoEntry> _redoStack = new();
        private SaveState _saveState = SaveState.Unsaved;
        private string? _openCoalesceGroup;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public SaveState SaveState => _saveState;

        /// <inheritdoc cref="IUndoManager.UndoHistory"/>
        public IReadOnlyList<IUndoableCommand> UndoHistory => _undoStack.Reverse().Select(e => e.Command).ToList();

        /// <inheritdoc cref="IUndoManager.RedoHistory"/>
        public IReadOnlyList<IUndoableCommand> RedoHistory => _redoStack.Select(e => e.Command).ToList();

        /// <summary>Raised after <see cref="Execute"/>, <see cref="Record"/>, <see cref="Undo"/>, <see cref="Redo"/>, <see cref="Clear"/>, <see cref="MarkSaved"/>, or <see cref="MarkSaveFailed"/>.</summary>
        public event Action? StackChanged;

        /// <inheritdoc cref="IUndoManager.Execute"/>
        public void Execute(IUndoableCommand cmd)
        {
            var tsxBefore = CaptureTsx(cmd);
            if (!cmd.Do()) return;

            if (cmd.CoalesceGroup is { } group && group == _openCoalesceGroup &&
                _undoStack.Count > 0 && _undoStack.Peek().Command.CoalesceGroup == group)
            {
                var previous = _undoStack.Pop();
                cmd = cmd.CoalesceWith(previous.Command);
                tsxBefore = previous.TsxBefore;
            }
            _openCoalesceGroup = cmd.CoalesceGroup;
            Push(cmd, tsxBefore);
        }

        /// <inheritdoc cref="IUndoManager.Record"/>
        public void Record(IUndoableCommand cmd) => Push(cmd, CaptureTsx(cmd));

        private void Push(IUndoableCommand cmd, object? tsxBefore)
        {
            _undoStack.Push(new UndoEntry(cmd, tsxBefore));
            _redoStack.Clear();
            StackChanged?.Invoke();
        }

        private object? CaptureTsx(IUndoableCommand cmd) => cmd.EditsAnimations ? projectManager?.CaptureTsxState() : null;

        private void RestoreTsx(object? state)
        {
            if (state is not null) projectManager!.RestoreTsxState(state);
        }

        /// <inheritdoc cref="IUndoManager.SealCoalescing"/>
        public void SealCoalescing() => _openCoalesceGroup = null;

        /// <summary>No-op when <see cref="CanUndo"/> is false.</summary>
        public void Undo()
        {
            if (!CanUndo) return;
            var entry = _undoStack.Pop();
            var tsxAfter = CaptureTsx(entry.Command);
            // First, because the command saves the document.
            RestoreTsx(entry.TsxBefore);
            entry.Command.Undo();
            _redoStack.Push(entry with { TsxAfter = tsxAfter });
            _openCoalesceGroup = null;
            StackChanged?.Invoke();
        }

        /// <summary>No-op when <see cref="CanRedo"/> is false.</summary>
        public void Redo()
        {
            if (!CanRedo) return;
            var entry = _redoStack.Pop();
            RestoreTsx(entry.TsxAfter);
            entry.Command.Redo();
            _undoStack.Push(entry);
            _openCoalesceGroup = null;
            StackChanged?.Invoke();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _saveState = SaveState.Unsaved;
            _openCoalesceGroup = null;
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
            foreach (var entry in snapshot.UndoStack)
                _undoStack.Push(entry);
            _redoStack.Clear();
            // RedoStack is stored LIFO (next-to-redo first); push in reverse to restore that order.
            for (int i = snapshot.RedoStack.Count - 1; i >= 0; i--)
                _redoStack.Push(snapshot.RedoStack[i]);
            _openCoalesceGroup = null;
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
