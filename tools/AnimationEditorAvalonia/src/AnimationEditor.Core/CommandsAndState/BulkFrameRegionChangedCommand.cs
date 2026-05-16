using FlatRedBall2.Animation.Content;
using System;
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

        public string Description { get; }

        public BulkFrameRegionChangedCommand(
            IReadOnlyList<FrameSnapshot> snapshots,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _snapshots = snapshots;
            _commands  = commands;
            _events    = events;
            Description = BuildDescription(snapshots);
        }

        private static string BuildDescription(IReadOnlyList<FrameSnapshot> snapshots)
        {
            if (snapshots.Count == 1)
            {
                var s = snapshots[0];
                float bW = s.BR - s.BL, bH = s.BB - s.BT;
                float aW = s.AR - s.AL, aH = s.AB - s.AT;
                bool sizeChanged = Math.Abs(bW - aW) > 0.0001f || Math.Abs(bH - aH) > 0.0001f;
                return sizeChanged ? "Resize Frame" : "Move Frame";
            }

            var first = snapshots[0];
            float dL = first.AL - first.BL;
            float dT = first.AT - first.BT;
            bool isChainDrag = true;
            foreach (var s in snapshots)
            {
                float bW = s.BR - s.BL, bH = s.BB - s.BT;
                float aW = s.AR - s.AL, aH = s.AB - s.AT;
                bool sizeChanged = Math.Abs(bW - aW) > 0.0001f || Math.Abs(bH - aH) > 0.0001f;
                if (sizeChanged ||
                    Math.Abs((s.AL - s.BL) - dL) > 0.0001f ||
                    Math.Abs((s.AT - s.BT) - dT) > 0.0001f)
                {
                    isChainDrag = false;
                    break;
                }
            }
            return isChainDrag
                ? $"Drag Chain ({snapshots.Count} frames)"
                : $"Edit {snapshots.Count} Frame Regions";
        }

        public bool Do()
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
            return true;
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
    }
}
