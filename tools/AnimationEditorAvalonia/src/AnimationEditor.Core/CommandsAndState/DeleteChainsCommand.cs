using FlatRedBall.Content.AnimationChain;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteChainsCommand : IUndoableCommand
    {
        private readonly (AnimationChainSave Chain, int OriginalIndex)[] _entries;
        private readonly AnimationChainListSave _acls;

        public DeleteChainsCommand(
            (AnimationChainSave Chain, int OriginalIndex)[] entries,
            AnimationChainListSave acls)
        {
            _entries = entries;
            _acls = acls;
        }

        public void Undo()
        {
            // Re-insert in original order so indices remain correct.
            foreach (var (chain, idx) in _entries)
            {
                int safeIdx = Math.Min(idx, _acls.AnimationChains.Count);
                _acls.AnimationChains.Insert(safeIdx, chain);
            }
            AppCommands.Self.RefreshTreeView();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            foreach (var (chain, _) in _entries)
                _acls.AnimationChains.Remove(chain);
            AppCommands.Self.RefreshTreeView();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
