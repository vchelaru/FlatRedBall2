using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class RenameChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly string _oldName;
        private readonly string _newName;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public RenameChainCommand(AnimationChainSave chain, string oldName, string newName,
            IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _oldName = oldName;
            _newName = newName;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            _chain.Name = _oldName;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _chain.Name = _newName;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
