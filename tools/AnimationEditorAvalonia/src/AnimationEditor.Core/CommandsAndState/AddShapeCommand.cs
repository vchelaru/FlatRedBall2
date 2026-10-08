using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>Adds one shape (rectangle, circle, or polygon) to the end of a frame's shape list and selects it.</summary>
    internal sealed class AddShapeCommand : IUndoableCommand
    {
        private bool _createdShapesSave;
        private readonly ShapeSave _shape;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly object? _preAddShape;

        public string Description { get; }

        public AddShapeCommand(ShapeSave shape, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _shape = shape;
            _frame = frame;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preAddShape = selectedState.SelectedShape;
            Description = $"Add {ShapeUndoLabel.FormatForAddDelete(shape)}";
        }

        public bool Do()
        {
            _createdShapesSave = _frame.ShapesSave is null;
            _frame.ShapesSave ??= new ShapesSave();
            _frame.ShapesSave.Add(_shape);
            Refresh();
            _selectedState.SelectShape(_shape);
            return true;
        }

        public void Undo()
        {
            _frame.ShapesSave!.Remove(_shape);
            // A frame that had no shapes before goes back to none: an empty collection still
            // serializes as a <ShapeCollectionSave> block, which would make the undo change the file.
            if (_createdShapesSave)
                _frame.ShapesSave = null;
            Refresh();
            _selectedState.SelectShape(_preAddShape);
        }

        private void Refresh()
        {
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
