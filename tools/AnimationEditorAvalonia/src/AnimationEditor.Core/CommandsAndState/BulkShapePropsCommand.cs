using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Captured Name/X/Y/P1/P2 of one shape (<see cref="AARectSave"/> or <see cref="CircleSave"/>),
    /// where P1/P2 are ScaleX/ScaleY for a rectangle or Radius/(unused) for a circle, plus its
    /// keyframe flags and curves (#1478; the size ones are unused for a polygon). Used by
    /// <see cref="BulkShapePropsCommand"/> to snapshot shapes before and after a batch edit.
    /// </summary>
    internal readonly record struct ShapeFieldSnapshot(
        object Shape, string Name, float X, float Y, float P1, float P2,
        bool SkipOffset, string? OffsetType, string? OffsetEasing,
        bool SkipSize, string? SizeType, string? SizeEasing)
    {
        public static ShapeFieldSnapshot Capture(object shape)
        {
            var sized = shape as SizedShapeSave;
            var s = shape as ShapeSave ?? throw new ArgumentException($"Unsupported shape type: {shape.GetType()}", nameof(shape));
            var (p1, p2) = shape switch
            {
                AARectSave r => (r.ScaleX, r.ScaleY),
                CircleSave c => (c.Radius, 0f),
                _ => (0f, 0f),
            };
            return new(shape, s.Name, s.X, s.Y, p1, p2,
                s.SkipOffsetKeyframe, s.OffsetInterpolation, s.OffsetEasing,
                sized?.SkipSizeKeyframe ?? false, sized?.SizeInterpolation, sized?.SizeEasing);
        }

        public void RestoreToShape()
        {
            var s = (ShapeSave)Shape;
            s.Name = Name; s.X = X; s.Y = Y;
            s.SkipOffsetKeyframe = SkipOffset; s.OffsetInterpolation = OffsetType; s.OffsetEasing = OffsetEasing;
            switch (Shape)
            {
                case AARectSave r: r.ScaleX = P1; r.ScaleY = P2; break;
                case CircleSave c: c.Radius = P1; break;
            }
            if (Shape is SizedShapeSave sized)
            {
                sized.SkipSizeKeyframe = SkipSize; sized.SizeInterpolation = SizeType; sized.SizeEasing = SizeEasing;
            }
        }
    }

    /// <summary>
    /// Do/undo/redo record for an operation that edits Name/X/Y/ScaleOrRadius across many shapes
    /// at once — the multi-select counterpart to <see cref="SetShapePropsCommand"/>. Shapes may
    /// belong to different frames (a multi-select can span frames); each affected frame's tree
    /// node is refreshed. <see cref="Do"/> snapshots the shapes, runs the supplied mutation, and
    /// snapshots them again; undo and redo just replay whichever snapshot set. <see cref="Do"/>
    /// returns <c>false</c> when the mutation changed nothing, so no empty undo entry is recorded.
    /// </summary>
    internal sealed class BulkShapePropsCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<object> _shapes;
        private readonly Action _mutate;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly IObjectFinder _objectFinder;

        private ShapeFieldSnapshot[] _before = [];
        private ShapeFieldSnapshot[] _after = [];

        public string Description { get; }

        /// <summary>
        /// Keyed on the edited shapes' own identities, so consecutive edits to the same
        /// selection (e.g. one call per keystroke while typing in the multi-select X field)
        /// coalesce into a single undo entry -- see <see cref="IUndoableCommand.CoalesceGroup"/>.
        /// </summary>
        public string? CoalesceGroup { get; }

        /// <param name="coalesce">
        /// <c>false</c> for a discrete edit (a keyframe toggle or curve pick) that must never merge into
        /// the field-typing undo entry before or after it.
        /// </param>
        public BulkShapePropsCommand(
            IReadOnlyList<object> shapes, Action mutate,
            IAppCommands commands, IApplicationEvents events, IObjectFinder objectFinder,
            string description, bool coalesce = true)
            : this(shapes, mutate, commands, events, objectFinder, description,
                  coalesce ? $"BulkShapeProps:{string.Join(",", shapes.Select(RuntimeHelpers.GetHashCode).OrderBy(h => h))}" : null,
                  [], [])
        {
        }

        private BulkShapePropsCommand(
            IReadOnlyList<object> shapes, Action mutate,
            IAppCommands commands, IApplicationEvents events, IObjectFinder objectFinder,
            string description, string? coalesceGroup,
            ShapeFieldSnapshot[] before, ShapeFieldSnapshot[] after)
        {
            _shapes = shapes;
            _mutate = mutate;
            _commands = commands;
            _events = events;
            _objectFinder = objectFinder;
            Description = description;
            CoalesceGroup = coalesceGroup;
            _before = before;
            _after = after;
        }

        public bool Do()
        {
            _before = _shapes.Select(ShapeFieldSnapshot.Capture).ToArray();
            _mutate();
            _after = _shapes.Select(ShapeFieldSnapshot.Capture).ToArray();

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
            var p = (BulkShapePropsCommand)previous;
            return new BulkShapePropsCommand(
                _shapes, _mutate, _commands, _events, _objectFinder,
                Description, CoalesceGroup, p._before, _after);
        }

        private void Apply(ShapeFieldSnapshot[] snapshots)
        {
            foreach (var snapshot in snapshots)
                snapshot.RestoreToShape();
            RaiseSideEffects();
        }

        private void RaiseSideEffects()
        {
            foreach (var shape in _shapes)
            {
                var frame = shape is ShapeSave s ? _objectFinder.GetAnimationFrameContaining(s) : null;
                if (frame is not null) _commands.RefreshTreeNode(frame);
            }
            _commands.RefreshAnimationFrameDisplay();
            _commands.RefreshWireframe();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
