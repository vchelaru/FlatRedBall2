using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for pasting one or more clipboard chains into the project.
    /// <see cref="Do"/> delegates placement (rename-to-unique + insert below the source
    /// block) to <see cref="ChainPasteLogic.InsertPastedChains"/> and remembers where the
    /// chains landed; redo re-inserts at those positions without re-running the rename so
    /// the names stay stable across an undo/redo cycle.
    /// </summary>
    internal sealed class PasteChainsCommand : IUndoableCommand
    {
        private readonly AnimationChainListSave _acls;
        private readonly IReadOnlyList<AnimationChainSave> _chains;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        private int[] _insertedIndices = [];

        public PasteChainsCommand(
            AnimationChainListSave acls,
            IReadOnlyList<AnimationChainSave> chains,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _acls = acls;
            _chains = chains;
            _commands = commands;
            _events = events;
        }

        public bool Do()
        {
            if (_chains.Count == 0) return false;

            ChainPasteLogic.InsertPastedChains(_acls, _chains);
            _insertedIndices = _chains
                .Select(c => _acls.AnimationChains.IndexOf(c))
                .ToArray();

            RaiseSideEffects();
            return true;
        }

        public void Undo()
        {
            foreach (var chain in _chains)
                _acls.AnimationChains.Remove(chain);
            RaiseSideEffects();
        }

        public void Redo()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                int idx = Math.Min(_insertedIndices[i], _acls.AnimationChains.Count);
                _acls.AnimationChains.Insert(idx, _chains[i]);
            }
            RaiseSideEffects();
        }

        private void RaiseSideEffects()
        {
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
