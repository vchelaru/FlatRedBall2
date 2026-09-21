using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Keyed on <paramref name="coalesceKind"/> plus the edited frames' own identities, so
        /// consecutive edits to the same field on the same frame set (e.g. one call per keystroke
        /// while typing in the Relative X field) coalesce into a single undo entry -- see
        /// <see cref="IUndoableCommand.CoalesceGroup"/>. Null (the default) never coalesces, for
        /// bulk operations that aren't driven by a single continuously-editable control.
        /// </summary>
        public string? CoalesceGroup { get; }

        public BulkFrameEditCommand(
            IReadOnlyList<AnimationFrameSave> frames, Action mutate,
            IAppCommands commands, IApplicationEvents events, bool refreshWireframe,
            string description = "Edit Frames", string? coalesceKind = null)
            : this(frames, mutate, commands, events, refreshWireframe, description,
                  BuildCoalesceGroup(coalesceKind, frames), [], [])
        {
        }

        private BulkFrameEditCommand(
            IReadOnlyList<AnimationFrameSave> frames, Action mutate,
            IAppCommands commands, IApplicationEvents events, bool refreshWireframe,
            string description, string? coalesceGroup,
            FrameFieldSnapshot[] before, FrameFieldSnapshot[] after)
        {
            _frames = frames;
            _mutate = mutate;
            _commands = commands;
            _events = events;
            _refreshWireframe = refreshWireframe;
            Description = description;
            CoalesceGroup = coalesceGroup;
            _before = before;
            _after = after;
        }

        private static string? BuildCoalesceGroup(string? kind, IReadOnlyList<AnimationFrameSave> frames) =>
            kind is null ? null :
            $"BulkFrameEdit:{kind}:{string.Join(",", frames.Select(RuntimeHelpers.GetHashCode).OrderBy(h => h))}";

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

        /// <summary>
        /// Combines <paramref name="previous"/>'s original "before" snapshot with this command's
        /// already-captured "after" snapshot (see <see cref="IUndoableCommand.CoalesceWith"/>).
        /// </summary>
        public IUndoableCommand CoalesceWith(IUndoableCommand previous)
        {
            var p = (BulkFrameEditCommand)previous;
            return new BulkFrameEditCommand(
                _frames, _mutate, _commands, _events, _refreshWireframe,
                Description, CoalesceGroup, p._before, _after);
        }

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
        }
    }
}
