using FlatRedBall.Content.AnimationChain;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteChainsCommand : IUndoableCommand
    {
        private readonly (AnimationChainSave Chain, int OriginalIndex)[] _entries;
        private readonly AnimationChainListSave _acls;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public DeleteChainsCommand(
            (AnimationChainSave Chain, int OriginalIndex)[] entries,
            AnimationChainListSave acls,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _entries = entries;
            _acls = acls;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            // Re-insert in original order so indices remain correct.
            foreach (var (chain, idx) in _entries)
            {
                int safeIdx = Math.Min(idx, _acls.AnimationChains.Count);
                _acls.AnimationChains.Insert(safeIdx, chain);
            }
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            foreach (var (chain, _) in _entries)
                _acls.AnimationChains.Remove(chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
