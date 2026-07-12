using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddAxisAlignedRectangleCommand : IUndoableCommand
    {
        private readonly AARectSave _rect;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly AARectSave? _preAddRect;

        public string Description { get; }

        public AddAxisAlignedRectangleCommand(AARectSave rect, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _rect = rect;
            _frame = frame;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preAddRect = selectedState.SelectedRectangle;
            Description = $"Add {ShapeUndoLabel.FormatForAddDelete(rect)}";
        }

        public bool Do()
        {
            _frame.ShapesSave!.Shapes.Add(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedRectangle = _rect;
            return true;
        }

        public void Undo()
        {
            _frame.ShapesSave!.Shapes.Remove(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedRectangle = _preAddRect;
        }
    }
}
