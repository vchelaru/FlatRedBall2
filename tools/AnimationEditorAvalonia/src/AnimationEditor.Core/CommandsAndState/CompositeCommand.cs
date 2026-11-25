using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Groups several commands so one user action that touches multiple items
    /// (e.g. "delete the selected shapes") is a single undo step rather than one
    /// step per item. Sub-commands whose <see cref="IUndoableCommand.Do"/> reports
    /// no change are dropped, so they are never undone or redone. The document is saved
    /// once for all of them: a native tsx gives out its tiles at every save, so a save
    /// between two sub-commands could give a tile away.
    /// </summary>
    internal sealed class CompositeCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<IUndoableCommand> _commands;
        private readonly IApplicationEvents _events;
        private IReadOnlyList<IUndoableCommand> _executed = [];

        public string Description { get; }

        public CompositeCommand(IReadOnlyList<IUndoableCommand> commands, string description, IApplicationEvents events)
        {
            _commands = commands;
            _events = events;
            Description = description;
        }

        /// <summary>The deletes as one step that <see cref="IDeleteCommand.Remove"/> already did. Null when there was nothing to remove.</summary>
        public static CompositeCommand? Removed(IEnumerable<IDeleteCommand> deletes, string description, IApplicationEvents events)
        {
            var removed = deletes.Where(d => d.Remove()).ToArray();
            return removed.Length == 0 ? null : new CompositeCommand(removed, description, events) { _executed = removed };
        }

        public bool Do()
        {
            _events.RaiseAnimationChainsChangedOnce(() => _executed = _commands.Where(c => c.Do()).ToArray());
            return _executed.Count > 0;
        }

        public void Undo() => _events.RaiseAnimationChainsChangedOnce(() =>
        {
            for (int i = _executed.Count - 1; i >= 0; i--)
                _executed[i].Undo();
        });

        public void Redo() => _events.RaiseAnimationChainsChangedOnce(() =>
        {
            foreach (var command in _executed)
                command.Redo();
        });
    }
}
