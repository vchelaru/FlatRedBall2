using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteCircleCommand : IUndoableCommand
    {
        private readonly CircleSave _circle;
        private readonly AnimationFrameSave _frame;
        private readonly int _originalIndex;

        public DeleteCircleCommand(CircleSave circle, AnimationFrameSave frame, int originalIndex)
        {
            _circle = circle;
            _frame = frame;
            _originalIndex = originalIndex;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapeCollectionSave.CircleSaves.Count);
            _frame.ShapeCollectionSave.CircleSaves.Insert(idx, _circle);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.CircleSaves.Remove(_circle);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
