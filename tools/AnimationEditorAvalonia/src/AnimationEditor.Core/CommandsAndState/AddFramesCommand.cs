using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for adding several frames to a chain in one operation
    /// (Add Multiple Frames). Recorded as a single entry so one user action is
    /// one undo step, rather than one step per frame.
    /// </summary>
    internal sealed class AddFramesCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave[] _frames;
        private readonly AnimationChainSave _chain;
        private readonly int _insertedAtIndex;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public AddFramesCommand(
            AnimationFrameSave[] frames, AnimationChainSave chain, int insertedAtIndex,
            IAppCommands commands, IApplicationEvents events)
        {
            _frames = frames;
            _chain = chain;
            _insertedAtIndex = insertedAtIndex;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            foreach (var frame in _frames)
                _chain.Frames.Remove(frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            int idx = Math.Min(_insertedAtIndex, _chain.Frames.Count);
            for (int i = 0; i < _frames.Length; i++)
                _chain.Frames.Insert(idx + i, _frames[i]);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
