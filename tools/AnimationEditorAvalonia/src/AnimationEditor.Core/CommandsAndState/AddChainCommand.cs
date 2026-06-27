using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly AnimationChainListSave _acls;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly AnimationChainSave? _preAddChain;
        private readonly int? _insertIndex;

        public string Description { get; }

        /// <param name="insertIndex">Where to place the chain. <c>null</c> appends at the
        /// end (the "Add Animation" case); a value inserts there (Duplicate places the copy
        /// right after its source). Out-of-range values fall back to appending.</param>
        public AddChainCommand(AnimationChainSave chain, AnimationChainListSave acls,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState,
            int? insertIndex = null)
        {
            _chain = chain;
            _acls = acls;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _insertIndex = insertIndex;
            _preAddChain = selectedState.SelectedChain;
            Description = $"Add Animation '{chain.Name}'";
        }

        public bool Do()
        {
            // Redo (after an undo) re-runs this; correct because the undo stack is LIFO,
            // so the list is in the same shape it was the first time.
            if (_insertIndex is int i && i >= 0 && i <= _acls.AnimationChains.Count)
                _acls.AnimationChains.Insert(i, _chain);
            else
                _acls.AnimationChains.Add(_chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedChain = _chain;
            return true;
        }

        public void Undo()
        {
            _acls.AnimationChains.Remove(_chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedChain = _preAddChain;
        }
    }
}
