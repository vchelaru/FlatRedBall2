using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddFrameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly AnimationChainSave _chain;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public AddFrameCommand(AnimationFrameSave frame, AnimationChainSave chain,
            IAppCommands commands, IApplicationEvents events)
        {
            _frame = frame;
            _chain = chain;
            _commands = commands;
            _events = events;
        }

        public bool Do()
        {
            _chain.Frames.Add(_frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            _chain.Frames.Remove(_frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
