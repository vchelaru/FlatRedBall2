using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Replaces one frame's whole <see cref="AnimationFrameSave.Events"/> list, backing add, edit,
    /// and remove alike. Snapshots are cloned so later edits to the live events can't leak into
    /// undo history.
    /// </summary>
    internal sealed class SetFrameEventsCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly AnimationFrameEvent[] _before;
        private readonly AnimationFrameEvent[] _after;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

        public SetFrameEventsCommand(AnimationFrameSave frame, IEnumerable<AnimationFrameEvent> after,
            string description, IAppCommands commands, IApplicationEvents events)
        {
            _frame = frame;
            _before = frame.Events.Select(e => e.Clone()).ToArray();
            _after = after.Select(e => e.Clone()).ToArray();
            Description = description;
            _commands = commands;
            _events = events;
        }

        public bool Do()
        {
            if (_before.Length == _after.Length &&
                _before.Zip(_after).All(p => p.First.Name == p.Second.Name && p.First.Data == p.Second.Data))
                return false;
            Apply(_after);
            return true;
        }

        public void Undo() => Apply(_before);

        public void Redo() => Apply(_after);

        private void Apply(AnimationFrameEvent[] events)
        {
            _frame.Events.Clear();
            _frame.Events.AddRange(events.Select(e => e.Clone()));
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
        }
    }
}
