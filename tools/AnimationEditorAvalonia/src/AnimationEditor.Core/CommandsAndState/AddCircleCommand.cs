using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddCircleCommand : IUndoableCommand
    {
        private readonly CircleSave _circle;
        private readonly AnimationFrameSave _frame;

        public AddCircleCommand(CircleSave circle, AnimationFrameSave frame)
        {
            _circle = circle;
            _frame = frame;
        }

        public void Undo()
        {
            _frame.ShapeCollectionSave.CircleSaves.Remove(_circle);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.CircleSaves.Add(_circle);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
