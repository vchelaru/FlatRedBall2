using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Records a drag-move of a single frame's display offset
    /// (<see cref="AnimationFrameSave.RelativeX"/>/<see cref="AnimationFrameSave.RelativeY"/>)
    /// so the operation can be undone and redone.
    /// </summary>
    public sealed class MoveFrameOffsetCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly float _oldX, _oldY;
        private readonly float _newX, _newY;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public MoveFrameOffsetCommand(
            AnimationFrameSave frame,
            float oldX, float oldY,
            float newX, float newY,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _frame    = frame;
            _oldX     = oldX;  _oldY  = oldY;
            _newX     = newX;  _newY  = newY;
            _commands = commands;
            _events   = events;
        }

        public string Description => "Move Frame";

        public bool Do() { Apply(_newX, _newY); return true; }
        public void Undo() => Apply(_oldX, _oldY);

        private void Apply(float x, float y)
        {
            _frame.RelativeX = x;
            _frame.RelativeY = y;

            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
