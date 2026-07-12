using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddCircleCommand : IUndoableCommand
    {
        private readonly CircleSave _circle;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly CircleSave? _preAddCircle;

        public string Description { get; }

        public AddCircleCommand(CircleSave circle, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _circle = circle;
            _frame = frame;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preAddCircle = selectedState.SelectedCircle;
            Description = $"Add {ShapeUndoLabel.FormatForAddDelete(circle)}";
        }

        public bool Do()
        {
            _frame.ShapesSave!.Shapes.Add(_circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedCircle = _circle;
            return true;
        }

        public void Undo()
        {
            _frame.ShapesSave!.Shapes.Remove(_circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedCircle = _preAddCircle;
        }
    }
}
