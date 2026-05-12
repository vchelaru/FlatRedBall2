using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteFramesCommand : IUndoableCommand
    {
        private readonly (AnimationFrameSave Frame, int OriginalIndex)[] _entries;
        private readonly AnimationChainSave _chain;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public DeleteFramesCommand(
            (AnimationFrameSave Frame, int OriginalIndex)[] entries,
            AnimationChainSave chain,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _entries = entries;
            _chain = chain;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            foreach (var (frame, idx) in _entries)
            {
                int safeIdx = Math.Min(idx, _chain.Frames.Count);
                _chain.Frames.Insert(safeIdx, frame);
            }
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            foreach (var (frame, _) in _entries)
                _chain.Frames.Remove(frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
