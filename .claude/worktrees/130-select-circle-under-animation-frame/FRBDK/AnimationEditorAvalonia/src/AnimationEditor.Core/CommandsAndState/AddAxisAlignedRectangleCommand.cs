using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddAxisAlignedRectangleCommand : IUndoableCommand
    {
        private readonly AxisAlignedRectangleSave _rect;
        private readonly AnimationFrameSave _frame;

        public AddAxisAlignedRectangleCommand(AxisAlignedRectangleSave rect, AnimationFrameSave frame)
        {
            _rect = rect;
            _frame = frame;
        }

        public void Undo()
        {
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Remove(_rect);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(_rect);
            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
