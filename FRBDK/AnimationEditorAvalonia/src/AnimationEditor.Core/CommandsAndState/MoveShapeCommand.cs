using AnimationEditor.Core.CommandsAndState;
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
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public MoveShapeCommand(
            AnimationFrameSave frame, object shape,
            float oldX, float oldY,
            float newX, float newY,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _frame    = frame;
            _shape    = shape;
            _oldX     = oldX;  _oldY  = oldY;
            _newX     = newX;  _newY  = newY;
            _commands = commands;
            _events   = events;
        }

        public void Undo() => Apply(_oldX, _oldY);
        public void Redo() => Apply(_newX, _newY);

        private void Apply(float x, float y)
        {
            if (_shape is AxisAlignedRectangleSave r) { r.X = x; r.Y = y; }
            else if (_shape is CircleSave c)          { c.X = x; c.Y = y; }

            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
