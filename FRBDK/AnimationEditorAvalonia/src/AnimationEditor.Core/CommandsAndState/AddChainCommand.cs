using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly AnimationChainListSave _acls;
        private readonly int _insertedAtIndex;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public AddChainCommand(AnimationChainSave chain, AnimationChainListSave acls, int insertedAtIndex,
            IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _acls = acls;
            _insertedAtIndex = insertedAtIndex;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            _acls.AnimationChains.Remove(_chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            int idx = System.Math.Min(_insertedAtIndex, _acls.AnimationChains.Count);
            _acls.AnimationChains.Insert(idx, _chain);
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
