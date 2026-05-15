using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteAxisAlignedRectangleCommand : IUndoableCommand
    {
        private readonly AARectSave _rect;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        private int _originalIndex = -1;  // captured by Do()

        public string Description { get; }

        public DeleteAxisAlignedRectangleCommand(
            AARectSave rect,
            AnimationFrameSave frame,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _rect = rect;
            _frame = frame;
            _commands = commands;
            _events = events;
            Description = $"Delete Rectangle '{rect.Name}'";
        }

        public bool Do()
        {
            _originalIndex = _frame.ShapesSave!.Shapes.IndexOf(_rect);
            if (_originalIndex < 0) return false;

            _frame.ShapesSave!.Shapes.RemoveAt(_originalIndex);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapesSave!.Shapes.Count);
            _frame.ShapesSave!.Shapes.Insert(idx, _rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapesSave!.Shapes.Remove(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
