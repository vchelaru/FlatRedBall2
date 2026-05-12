using FlatRedBall.Content.AnimationChain;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteFramesCommand : IUndoableCommand
    {
        private readonly (AnimationFrameSave Frame, int OriginalIndex)[] _entries;
        private readonly AnimationChainSave _chain;

        public DeleteFramesCommand(
            (AnimationFrameSave Frame, int OriginalIndex)[] entries,
            AnimationChainSave chain)
        {
            _entries = entries;
            _chain = chain;
        }

        public void Undo()
        {
            foreach (var (frame, idx) in _entries)
            {
                int safeIdx = Math.Min(idx, _chain.Frames.Count);
                _chain.Frames.Insert(safeIdx, frame);
            }
            AppCommands.Self.RefreshTreeNode(_chain);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            foreach (var (frame, _) in _entries)
                _chain.Frames.Remove(frame);
            AppCommands.Self.RefreshTreeNode(_chain);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
