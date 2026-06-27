using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

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
        float Left, float Right, float Top, float Bottom,
        int? Red, int? Green, int? Blue, int? Alpha,
        ColorOperation? ColorOperation)
    {
        public static FrameFieldSnapshot Capture(AnimationFrameSave f) =>
            new(f, f.FrameLength, f.RelativeX, f.RelativeY,
                f.LeftCoordinate, f.RightCoordinate, f.TopCoordinate, f.BottomCoordinate,
                f.Red, f.Green, f.Blue, f.Alpha, f.ColorOperation);

        public void RestoreToFrame()
        {
            Frame.FrameLength      = FrameLength;
            Frame.RelativeX        = RelativeX;
            Frame.RelativeY        = RelativeY;
            Frame.LeftCoordinate   = Left;
            Frame.RightCoordinate  = Right;
            Frame.TopCoordinate    = Top;
            Frame.BottomCoordinate = Bottom;
            Frame.Red              = Red;
            Frame.Green            = Green;
            Frame.Blue             = Blue;
            Frame.Alpha            = Alpha;
            Frame.ColorOperation   = ColorOperation;
        }
    }

    /// <summary>
    /// Do/undo/redo record for an operation that edits numeric fields across many frames
    /// at once — set-all-frame-lengths, adjust-offsets, scale-frame-times,
    /// adjust-UV-after-resize. <see cref="Do"/> snapshots the affected frames, runs the
    /// supplied mutation, and snapshots them again; undo and redo just replay whichever
    /// snapshot set. <see cref="Do"/> returns <c>false</c> when the mutation changed
    /// nothing, so no empty undo entry is recorded.
    /// </summary>
    internal sealed class BulkFrameEditCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<AnimationFrameSave> _frames;
        private readonly Action _mutate;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly bool _refreshWireframe;

        private FrameFieldSnapshot[] _before = [];
        private FrameFieldSnapshot[] _after = [];

        public string Description { get; }

        public BulkFrameEditCommand(
            IReadOnlyList<AnimationFrameSave> frames, Action mutate,
            IAppCommands commands, IApplicationEvents events, bool refreshWireframe,
            string description = "Edit Frames")
        {
            _frames = frames;
            _mutate = mutate;
            _commands = commands;
            _events = events;
            _refreshWireframe = refreshWireframe;
            Description = description;
        }

        public bool Do()
        {
            _before = _frames.Select(FrameFieldSnapshot.Capture).ToArray();
            _mutate();
            _after = _frames.Select(FrameFieldSnapshot.Capture).ToArray();

            if (_before.SequenceEqual(_after)) return false;

            RaiseSideEffects();
            return true;
        }

        public void Undo() => Apply(_before);
        public void Redo() => Apply(_after);

        private void Apply(FrameFieldSnapshot[] snapshots)
        {
            foreach (var snapshot in snapshots)
                snapshot.RestoreToFrame();
            RaiseSideEffects();
        }

        private void RaiseSideEffects()
        {
            foreach (var f in _frames)
                _commands.RefreshTreeNode(f);
            _events.RaiseAnimationChainsChanged();
            if (_refreshWireframe)
                _commands.RefreshWireframe();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
