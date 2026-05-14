using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddCircleCommand : IUndoableCommand
    {
        private readonly CircleSave _circle;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description => "Add Circle";

        public AddCircleCommand(CircleSave circle, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events)
        {
            _circle = circle;
            _frame = frame;
            _commands = commands;
            _events = events;
        }

        public bool Do()
        {
            _frame.ShapesSave!.CircleSaves.Add(_circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            _frame.ShapesSave!.CircleSaves.Remove(_circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
