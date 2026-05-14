using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly AnimationChainListSave _acls;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public AddChainCommand(AnimationChainSave chain, AnimationChainListSave acls,
            IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _acls = acls;
            _commands = commands;
            _events = events;
        }

        public bool Do()
        {
            // Appended at the end; redo (after an undo) re-appends — correct because the
            // undo stack is LIFO, so the list is in the same shape it was the first time.
            _acls.AnimationChains.Add(_chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            _acls.AnimationChains.Remove(_chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
