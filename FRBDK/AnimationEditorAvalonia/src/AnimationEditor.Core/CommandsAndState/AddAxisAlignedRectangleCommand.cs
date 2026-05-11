using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddAxisAlignedRectangleCommand : IUndoableCommand
    {
        private readonly AxisAlignedRectangleSave _rect;
        private readonly AnimationFrameSave _frame;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public AddAxisAlignedRectangleCommand(AxisAlignedRectangleSave rect, AnimationFrameSave frame,
            IAppCommands commands, IApplicationEvents events)
        {
            _rect = rect;
            _frame = frame;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Remove(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
