using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Captured values of every numeric field a bulk frame-edit operation can touch
    /// (frame length, offsets, UV coordinates). Used by <see cref="BulkFrameEditCommand"/>
    /// to snapshot a frame before and after the operation.
    /// </summary>
    internal readonly record struct FrameFieldSnapshot(
        AnimationFrameSave Frame,
        float FrameLength,
        float RelativeX, float RelativeY,
        float Left, float Right, float Top, float Bottom)
    {
        public static FrameFieldSnapshot Capture(AnimationFrameSave f) =>
            new(f, f.FrameLength, f.RelativeX, f.RelativeY,
                f.LeftCoordinate, f.RightCoordinate, f.TopCoordinate, f.BottomCoordinate);

        public void RestoreToFrame()
        {
            Frame.FrameLength      = FrameLength;
            Frame.RelativeX        = RelativeX;
            Frame.RelativeY        = RelativeY;
            Frame.LeftCoordinate   = Left;
            Frame.RightCoordinate  = Right;
            Frame.TopCoordinate    = Top;
            Frame.BottomCoordinate = Bottom;
        }
    }

    /// <summary>
    /// Undo/redo record for an operation that edits numeric fields across many frames
    /// at once — set-all-frame-lengths, adjust-offsets, scale-frame-times,
    /// adjust-UV-after-resize. The caller snapshots the affected frames before and
    /// after running the operation; this command just replays whichever snapshot set.
    /// </summary>
    internal sealed class BulkFrameEditCommand : IUndoableCommand
    {
        private readonly FrameFieldSnapshot[] _before;
        private readonly FrameFieldSnapshot[] _after;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly bool _refreshWireframe;

        public BulkFrameEditCommand(
            FrameFieldSnapshot[] before, FrameFieldSnapshot[] after,
            IAppCommands commands, IApplicationEvents events, bool refreshWireframe)
        {
            _before = before;
            _after = after;
            _commands = commands;
            _events = events;
            _refreshWireframe = refreshWireframe;
        }

        public void Undo() => Apply(_before);
        public void Redo() => Apply(_after);

        private void Apply(FrameFieldSnapshot[] snapshots)
        {
            foreach (var snapshot in snapshots)
                snapshot.RestoreToFrame();
            _events.RaiseAnimationChainsChanged();
            if (_refreshWireframe)
                _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
