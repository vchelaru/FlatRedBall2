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

        public string Description { get; }

        public AddChainCommand(AnimationChainSave chain, AnimationChainListSave acls,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _chain = chain;
            _acls = acls;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preAddChain = selectedState.SelectedChain;
            Description = $"Add Animation '{chain.Name}'";
        }

        public bool Do()
        {
            // Appended at the end; redo (after an undo) re-appends — correct because the
            // undo stack is LIFO, so the list is in the same shape it was the first time.
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
