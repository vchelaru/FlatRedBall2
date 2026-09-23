using FlatRedBall2.AnimationEditorCommon;

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
        private bool _createdShapesSave;

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
            _createdShapesSave = _frame.ShapesSave is null;
            _frame.ShapesSave ??= new ShapesSave();
            _frame.ShapesSave.Shapes.Add(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _selectedState.SelectedRectangle = _rect;
            return true;
        }

        public void Undo()
        {
            _frame.ShapesSave!.Shapes.Remove(_rect);
            // A frame that had no shapes before goes back to none: an empty collection still
            // serializes as a <ShapeCollectionSave> block, which would make the undo change the file.
            if (_createdShapesSave)
                _frame.ShapesSave = null;
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _selectedState.SelectedRectangle = _preAddRect;
        }
    }
}
