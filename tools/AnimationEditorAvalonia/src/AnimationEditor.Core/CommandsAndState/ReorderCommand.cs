using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for any operation that reorders the elements of a list
    /// (move up/down, move to top/bottom, invert, sort). Snapshots the full
    /// before/after order, so it is correct regardless of how the reorder was
    /// computed — the command never has to know the specific move that happened.
    /// </summary>
    internal sealed class ReorderCommand<T> : IUndoableCommand
    {
        private readonly IList<T> _list;
        private readonly T[] _before;
        private readonly T[] _after;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly System.Action _refresh;

        public ReorderCommand(
            IList<T> list, T[] before, T[] after,
            IAppCommands commands, IApplicationEvents events, System.Action refresh)
        {
            _list = list;
            _before = before;
            _after = after;
            _commands = commands;
            _events = events;
            _refresh = refresh;
        }

        public void Undo() => Apply(_before);
        public void Redo() => Apply(_after);

        private void Apply(T[] order)
        {
            _list.Clear();
            foreach (var item in order)
                _list.Add(item);
            _refresh();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
