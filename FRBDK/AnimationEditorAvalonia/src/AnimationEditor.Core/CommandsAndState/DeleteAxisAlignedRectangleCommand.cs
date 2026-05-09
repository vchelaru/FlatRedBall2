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

        public DeleteAxisAlignedRectangleCommand(
            AxisAlignedRectangleSave rect,
            AnimationFrameSave frame,
            int originalIndex)
        {
            _rect = rect;
            _frame = frame;
            _originalIndex = originalIndex;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Count);
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Insert(idx, _rect);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Remove(_rect);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
