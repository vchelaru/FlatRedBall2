using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteCircleCommand : IUndoableCommand
    {
        private readonly CircleSave _circle;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;

        private int _originalIndex = -1;  // captured by Do()

        public string Description { get; }

        public DeleteCircleCommand(CircleSave circle, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _circle = circle;
            _frame = frame;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            Description = $"Delete Circle '{circle.Name}'";
        }

        public bool Do()
        {
            _originalIndex = _frame.ShapesSave!.Shapes.IndexOf(_circle);
            if (_originalIndex < 0) return false;

            _frame.ShapesSave!.Shapes.RemoveAt(_originalIndex);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedCircle = null;
            return true;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapesSave!.Shapes.Count);
            _frame.ShapesSave!.Shapes.Insert(idx, _circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedCircle = _circle;
        }

        public void Redo()
        {
            _frame.ShapesSave!.Shapes.Remove(_circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedCircle = null;
        }
    }
}
