using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class SetChainLoopCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly bool _loop;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

        public SetChainLoopCommand(AnimationChainSave chain, bool loop,
            IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _loop = loop;
            _commands = commands;
            _events = events;
            Description = loop ? $"Enable loop on '{chain.Name}'" : $"Disable loop on '{chain.Name}'";
        }

        public bool Do()
        {
            _chain.Loop = _loop;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            _chain.Loop = !_loop;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
