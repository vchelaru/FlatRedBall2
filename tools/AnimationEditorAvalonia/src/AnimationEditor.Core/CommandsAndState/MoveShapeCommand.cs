using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Records a drag-move of a single collision shape (rectangle, circle, or polygon origin)
    /// so the operation can be undone and redone.
    /// </summary>
    public sealed class MoveShapeCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly object _shape;   // a ShapeSave
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

        public string Description => $"Move {ShapeUndoLabel.Format(_shape)}";

        public bool Do() { Apply(_newX, _newY); return true; }
        public void Undo() => Apply(_oldX, _oldY);

        private void Apply(float x, float y)
        {
            if (_shape is ShapeSave s) { s.X = x; s.Y = y; }

            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
