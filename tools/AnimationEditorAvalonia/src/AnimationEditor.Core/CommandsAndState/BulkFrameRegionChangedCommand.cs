using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for a simultaneous region change across multiple frames
    /// (e.g. a bulk handle-drag applied to all frames across selected chains).
    /// </summary>
    public sealed class BulkFrameRegionChangedCommand : IUndoableCommand
    {
        public readonly record struct FrameSnapshot(
            AnimationFrameSave Frame,
            float BL, float BT, float BR, float BB,
            float AL, float AT, float AR, float AB);

        private readonly IReadOnlyList<FrameSnapshot> _snapshots;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public BulkFrameRegionChangedCommand(
            IReadOnlyList<FrameSnapshot> snapshots,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _snapshots = snapshots;
            _commands  = commands;
            _events    = events;
        }

        public void Undo()
        {
            foreach (var s in _snapshots)
            {
                s.Frame.LeftCoordinate   = s.BL;
                s.Frame.TopCoordinate    = s.BT;
                s.Frame.RightCoordinate  = s.BR;
                s.Frame.BottomCoordinate = s.BB;
                _commands.RefreshTreeNode(s.Frame);
            }
            _events.RaiseAnimationChainsChanged();
            _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            foreach (var s in _snapshots)
            {
                s.Frame.LeftCoordinate   = s.AL;
                s.Frame.TopCoordinate    = s.AT;
                s.Frame.RightCoordinate  = s.AR;
                s.Frame.BottomCoordinate = s.AB;
                _commands.RefreshTreeNode(s.Frame);
            }
            _events.RaiseAnimationChainsChanged();
            _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
