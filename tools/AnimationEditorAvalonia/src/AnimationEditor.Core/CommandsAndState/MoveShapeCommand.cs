using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Records a drag-move of a single collision shape (circle or axis-aligned rectangle)
    /// so the operation can be undone and redone.
    /// </summary>
    public sealed class MoveShapeCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly object _shape;   // AxisAlignedRectangleSave | CircleSave
        private readonly float _oldX, _oldY;
        private readonly float _newX, _newY;

        public MoveShapeCommand(
            AnimationFrameSave frame, object shape,
            float oldX, float oldY,
            float newX, float newY)
        {
            _frame = frame;
            _shape = shape;
            _oldX  = oldX;  _oldY  = oldY;
            _newX  = newX;  _newY  = newY;
        }

        public void Undo()
        {
            Apply(_oldX, _oldY);
        }

        public void Redo()
        {
            Apply(_newX, _newY);
        }

        private void Apply(float x, float y)
        {
            if (_shape is AxisAlignedRectangleSave r) { r.X = x; r.Y = y; }
            else if (_shape is CircleSave c)          { c.X = x; c.Y = y; }

            AppCommands.Self.RefreshTreeNode(_frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
