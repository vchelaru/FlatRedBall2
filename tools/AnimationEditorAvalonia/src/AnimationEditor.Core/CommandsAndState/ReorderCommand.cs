using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Do/undo/redo record for any operation that reorders the elements of a list
    /// (move up/down, move to top/bottom, invert, sort). <see cref="Do"/> runs the
    /// supplied reorder action and snapshots the before/after order, so undo and redo
    /// are correct regardless of how the reorder was computed — the command never has
    /// to know the specific move that happened. <see cref="Do"/> returns <c>false</c>
    /// when the reorder left the list unchanged, so no empty undo entry is recorded.
    /// </summary>
    internal sealed class ReorderCommand<T> : IUndoableCommand
    {
        private readonly Func<T[]> _snapshot;
        private readonly Action<T[]> _restore;
        private readonly Action _reorder;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly Action _refresh;

        private T[] _before = [];
        private T[] _after = [];

        public string Description { get; }

        public ReorderCommand(
            IList<T> list, Action reorder,
            IAppCommands commands, IApplicationEvents events, Action refresh,
            string description = "Reorder")
        {
            _snapshot = list.ToArray;
            _restore = order =>
            {
                list.Clear();
                foreach (var item in order)
                    list.Add(item);
            };
            _reorder = reorder;
            _commands = commands;
            _events = events;
            _refresh = refresh;
            Description = description;
        }

        /// <summary>For lists that cannot be mutated directly: <paramref name="snapshot"/> reads the order, <paramref name="restore"/> writes one back.</summary>
        public ReorderCommand(
            Func<T[]> snapshot, Action<T[]> restore, Action reorder,
            IAppCommands commands, IApplicationEvents events, Action refresh,
            string description = "Reorder")
        {
            _snapshot = snapshot;
            _restore = restore;
            _reorder = reorder;
            _commands = commands;
            _events = events;
            _refresh = refresh;
            Description = description;
        }

        public bool Do()
        {
            _before = _snapshot();
            _reorder();
            _after = _snapshot();

            if (_before.SequenceEqual(_after)) return false;

            RaiseSideEffects();
            return true;
        }

        public void Undo() => Apply(_before);
        public void Redo() => Apply(_after);

        private void Apply(T[] order)
        {
            _restore(order);
            RaiseSideEffects();
        }

        private void RaiseSideEffects()
        {
            _refresh();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
