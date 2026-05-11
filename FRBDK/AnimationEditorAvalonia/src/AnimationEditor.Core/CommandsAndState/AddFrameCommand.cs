using FlatRedBall.Content.AnimationChain;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddFrameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly AnimationChainSave _chain;
        private readonly int _insertedAtIndex;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public AddFrameCommand(AnimationFrameSave frame, AnimationChainSave chain, int insertedAtIndex,
            IAppCommands commands, IApplicationEvents events)
        {
            _frame = frame;
            _chain = chain;
            _insertedAtIndex = insertedAtIndex;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            _chain.Frames.Remove(_frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            int idx = Math.Min(_insertedAtIndex, _chain.Frames.Count);
            _chain.Frames.Insert(idx, _frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
