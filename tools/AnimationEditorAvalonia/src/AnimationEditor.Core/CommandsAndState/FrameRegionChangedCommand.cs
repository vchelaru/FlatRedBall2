using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    public sealed class FrameRegionChangedCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly float _bL, _bT, _bR, _bB;  // before UV
        private readonly float _aL, _aT, _aR, _aB;  // after UV

        public FrameRegionChangedCommand(
            AnimationFrameSave frame,
            float bL, float bT, float bR, float bB,
            float aL, float aT, float aR, float aB)
        {
            _frame = frame;
            _bL = bL; _bT = bT; _bR = bR; _bB = bB;
            _aL = aL; _aT = aT; _aR = aR; _aB = aB;
        }

        public void Undo()
        {
            _frame.LeftCoordinate   = _bL;
            _frame.TopCoordinate    = _bT;
            _frame.RightCoordinate  = _bR;
            _frame.BottomCoordinate = _bB;
            AppCommands.Self.RefreshTreeNode(_frame);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.RefreshWireframe();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.LeftCoordinate   = _aL;
            _frame.TopCoordinate    = _aT;
            _frame.RightCoordinate  = _aR;
            _frame.BottomCoordinate = _aB;
            AppCommands.Self.RefreshTreeNode(_frame);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.RefreshWireframe();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
