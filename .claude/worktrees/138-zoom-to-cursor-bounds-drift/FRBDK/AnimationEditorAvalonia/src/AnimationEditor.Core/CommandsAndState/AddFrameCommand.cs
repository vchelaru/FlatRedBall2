using FlatRedBall.Content.AnimationChain;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddFrameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly AnimationChainSave _chain;
        private readonly int _insertedAtIndex;

        public AddFrameCommand(AnimationFrameSave frame, AnimationChainSave chain, int insertedAtIndex)
        {
            _frame = frame;
            _chain = chain;
            _insertedAtIndex = insertedAtIndex;
        }

        public void Undo()
        {
            _chain.Frames.Remove(_frame);
            AppCommands.Self.RefreshTreeNode(_chain);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            int idx = Math.Min(_insertedAtIndex, _chain.Frames.Count);
            _chain.Frames.Insert(idx, _frame);
            AppCommands.Self.RefreshTreeNode(_chain);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
