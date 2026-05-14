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
        private readonly IList<T> _list;
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
            _list = list;
            _reorder = reorder;
            _commands = commands;
            _events = events;
            _refresh = refresh;
            Description = description;
        }

        public bool Do()
        {
            _before = _list.ToArray();
            _reorder();
            _after = _list.ToArray();

            if (_before.SequenceEqual(_after)) return false;

            RaiseSideEffects();
            return true;
        }

        public void Undo() => Apply(_before);
        public void Redo() => Apply(_after);

        private void Apply(T[] order)
        {
            _list.Clear();
            foreach (var item in order)
                _list.Add(item);
            RaiseSideEffects();
        }

        private void RaiseSideEffects()
        {
            _refresh();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
