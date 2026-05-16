using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    public sealed class FrameRegionChangedCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly float _bL, _bT, _bR, _bB;  // before UV
        private readonly float _aL, _aT, _aR, _aB;  // after UV
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

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
            float bW = bR - bL, bH = bB - bT;
            float aW = aR - aL, aH = aB - aT;
            bool sizeChanged = Math.Abs(bW - aW) > 0.0001f || Math.Abs(bH - aH) > 0.0001f;
            Description = sizeChanged ? "Resize Frame" : "Move Frame";
        }

        public bool Do() { Apply(_aL, _aT, _aR, _aB); return true; }
        public void Undo() => Apply(_bL, _bT, _bR, _bB);

        private void Apply(float left, float top, float right, float bottom)
        {
            _frame.LeftCoordinate   = left;
            _frame.TopCoordinate    = top;
            _frame.RightCoordinate  = right;
            _frame.BottomCoordinate = bottom;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
            _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
