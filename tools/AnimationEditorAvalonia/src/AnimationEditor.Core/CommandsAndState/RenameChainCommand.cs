using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class RenameChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly string _oldName;
        private readonly string _newName;

        public RenameChainCommand(AnimationChainSave chain, string oldName, string newName)
        {
            _chain = chain;
            _oldName = oldName;
            _newName = newName;
        }

        public void Undo()
        {
            _chain.Name = _oldName;
            AppCommands.Self.RefreshTreeNode(_chain);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _chain.Name = _newName;
            AppCommands.Self.RefreshTreeNode(_chain);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
