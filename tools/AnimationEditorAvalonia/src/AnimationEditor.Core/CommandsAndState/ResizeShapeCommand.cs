using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Records a handle-resize of a single collision shape (circle or axis-aligned rectangle)
    /// so the operation can be undone and redone.
    /// For rectangles: param1 = ScaleX, param2 = ScaleY.
    /// For circles:    param1 = Radius,  param2 = 0.
    /// </summary>
    public sealed class ResizeShapeCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly object _shape;   // AARectSave | CircleSave
        private readonly float _oldX, _oldY, _oldParam1, _oldParam2;
        private readonly float _newX, _newY, _newParam1, _newParam2;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public ResizeShapeCommand(
            AnimationFrameSave frame, object shape,
            float oldX, float oldY, float oldParam1, float oldParam2,
            float newX, float newY, float newParam1, float newParam2,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _frame     = frame;
            _shape     = shape;
            _oldX      = oldX;      _oldY      = oldY;
            _oldParam1 = oldParam1; _oldParam2 = oldParam2;
            _newX      = newX;      _newY      = newY;
            _newParam1 = newParam1; _newParam2 = newParam2;
            _commands  = commands;
            _events    = events;
        }

        public bool Do() { Apply(_newX, _newY, _newParam1, _newParam2); return true; }
        public void Undo() => Apply(_oldX, _oldY, _oldParam1, _oldParam2);

        private void Apply(float x, float y, float p1, float p2)
        {
            if (_shape is AARectSave r)
            {
                r.X = x; r.Y = y; r.ScaleX = p1; r.ScaleY = p2;
            }
            else if (_shape is CircleSave c)
            {
                c.X = x; c.Y = y; c.Radius = p1;
            }

            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
