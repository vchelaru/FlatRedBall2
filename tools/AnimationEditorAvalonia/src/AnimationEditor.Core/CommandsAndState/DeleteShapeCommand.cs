using FlatRedBall2.AnimationEditorCommon;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>Removes one shape from a frame; undo puts it back at its original index.</summary>
    internal sealed class DeleteShapeCommand : IUndoableCommand
    {
        private readonly ShapeSave _shape;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;

        private int _originalIndex = -1;  // captured by Do()

        public string Description { get; }

        public DeleteShapeCommand(ShapeSave shape, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _shape = shape;
            _frame = frame;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            Description = $"Delete {ShapeUndoLabel.FormatForAddDelete(shape)}";
        }

        public bool Do()
        {
            _originalIndex = _frame.ShapesSave!.IndexOf(_shape);
            if (_originalIndex < 0) return false;

            _frame.ShapesSave!.Remove(_shape);
            Refresh();
            _selectedState.SelectShape(null);
            return true;
        }

        public void Undo()
        {
            _frame.ShapesSave!.Insert(_originalIndex, _shape);
            Refresh();
            _selectedState.SelectShape(_shape);
        }

        public void Redo()
        {
            _frame.ShapesSave!.Remove(_shape);
            Refresh();
            _selectedState.SelectShape(null);
        }

        private void Refresh()
        {
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
