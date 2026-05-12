using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    public sealed class FrameRegionChangedCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly float _bL, _bT, _bR, _bB;  // before UV
        private readonly float _aL, _aT, _aR, _aB;  // after UV
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public FrameRegionChangedCommand(
            AnimationFrameSave frame,
            float bL, float bT, float bR, float bB,
            float aL, float aT, float aR, float aB,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _frame = frame;
            _bL = bL; _bT = bT; _bR = bR; _bB = bB;
            _aL = aL; _aT = aT; _aR = aR; _aB = aB;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            _frame.LeftCoordinate   = _bL;
            _frame.TopCoordinate    = _bT;
            _frame.RightCoordinate  = _bR;
            _frame.BottomCoordinate = _bB;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
            _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.LeftCoordinate   = _aL;
            _frame.TopCoordinate    = _aT;
            _frame.RightCoordinate  = _aR;
            _frame.BottomCoordinate = _aB;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
            _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
