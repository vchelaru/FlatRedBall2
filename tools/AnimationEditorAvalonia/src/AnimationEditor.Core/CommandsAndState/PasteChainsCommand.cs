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
        private readonly ISelectedState _selectedState;
        private readonly List<object> _preSelection;
        private readonly List<AnimationChainSave> _anchorChains;

        private int[] _insertedIndices = [];

        public string Description { get; }

        public PasteChainsCommand(
            AnimationChainListSave acls,
            IReadOnlyList<AnimationChainSave> chains,
            IAppCommands commands,
            IApplicationEvents events,
            ISelectedState selectedState)
        {
            _acls = acls;
            _chains = chains;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preSelection = new List<object>(selectedState.SelectedNodes);
            _anchorChains = selectedState.SelectedChains;
            Description = chains.Count == 1
                ? $"Paste Animation '{chains[0].Name}'"
                : $"Paste {chains.Count} Animations";
        }

        public bool Do()
        {
            if (_chains.Count == 0) return false;

            ChainPasteLogic.InsertPastedChains(_acls, _chains, _anchorChains);
            _insertedIndices = _chains
                .Select(c => _acls.AnimationChains.IndexOf(c))
                .ToArray();

            RaiseSideEffects();
            _selectedState.SelectedNodes = _chains.Cast<object>().ToList();
            _selectedState.SelectedChain = _chains[^1];
            return true;
        }

        public void Undo()
        {
            foreach (var chain in _chains)
                _acls.AnimationChains.Remove(chain);
            RaiseSideEffects();
            _selectedState.SelectedNodes = _preSelection;
            _selectedState.SelectedChain = _preSelection.OfType<AnimationChainSave>().LastOrDefault();
        }

        public void Redo()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                int idx = Math.Min(_insertedIndices[i], _acls.AnimationChains.Count);
                _acls.AnimationChains.Insert(idx, _chains[i]);
            }
            RaiseSideEffects();
            _selectedState.SelectedNodes = _chains.Cast<object>().ToList();
            _selectedState.SelectedChain = _chains[^1];
        }

        private void RaiseSideEffects()
        {
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
