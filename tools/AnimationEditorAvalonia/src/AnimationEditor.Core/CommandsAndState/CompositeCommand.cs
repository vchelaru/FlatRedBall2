using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Groups several commands so one user action that touches multiple items
    /// (e.g. "delete the selected shapes") is a single undo step rather than one
    /// step per item. Sub-commands whose <see cref="IUndoableCommand.Do"/> reports
    /// no change are dropped, so they are never undone or redone.
    /// </summary>
    internal sealed class CompositeCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<IUndoableCommand> _commands;
        private IReadOnlyList<IUndoableCommand> _executed = [];

        public CompositeCommand(IReadOnlyList<IUndoableCommand> commands) => _commands = commands;

        public bool Do()
        {
            _executed = _commands.Where(c => c.Do()).ToArray();
            return _executed.Count > 0;
        }

        public void Undo()
        {
            for (int i = _executed.Count - 1; i >= 0; i--)
                _executed[i].Undo();
        }

        public void Redo()
        {
            foreach (var command in _executed)
                command.Redo();
        }
    }
}
