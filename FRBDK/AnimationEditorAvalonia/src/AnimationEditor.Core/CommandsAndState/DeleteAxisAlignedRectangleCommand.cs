using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteAxisAlignedRectangleCommand : IUndoableCommand
    {
        private readonly AxisAlignedRectangleSave _rect;
        private readonly AnimationFrameSave _frame;
        private readonly int _originalIndex;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public DeleteAxisAlignedRectangleCommand(
            AxisAlignedRectangleSave rect,
            AnimationFrameSave frame,
            int originalIndex,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _rect = rect;
            _frame = frame;
            _originalIndex = originalIndex;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Count);
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Insert(idx, _rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Remove(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
