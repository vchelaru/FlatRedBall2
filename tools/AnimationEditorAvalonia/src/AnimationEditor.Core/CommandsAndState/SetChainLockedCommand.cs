using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class SetChainLockedCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly bool _locked;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

        public SetChainLockedCommand(AnimationChainSave chain, bool locked,
            IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _locked = locked;
            _commands = commands;
            _events = events;
            Description = locked ? $"Lock '{chain.Name}'" : $"Unlock '{chain.Name}'";
        }

        public bool Do()
        {
            _chain.IsLocked = _locked;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            _chain.IsLocked = !_locked;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
