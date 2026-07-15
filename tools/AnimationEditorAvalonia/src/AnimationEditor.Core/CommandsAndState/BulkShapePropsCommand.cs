using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Captured Name/X/Y/P1/P2 of one shape (<see cref="AARectSave"/> or <see cref="CircleSave"/>),
    /// where P1/P2 are ScaleX/ScaleY for a rectangle or Radius/(unused) for a circle. Used by
    /// <see cref="BulkShapePropsCommand"/> to snapshot shapes before and after a batch edit.
    /// </summary>
    internal readonly record struct ShapeFieldSnapshot(object Shape, string Name, float X, float Y, float P1, float P2)
    {
        public static ShapeFieldSnapshot Capture(object shape) => shape switch
        {
            AARectSave r => new(shape, r.Name, r.X, r.Y, r.ScaleX, r.ScaleY),
            CircleSave c => new(shape, c.Name, c.X, c.Y, c.Radius, 0f),
            _ => throw new ArgumentException($"Unsupported shape type: {shape.GetType()}", nameof(shape)),
        };

        public void RestoreToShape()
        {
            switch (Shape)
            {
                case AARectSave r: r.Name = Name; r.X = X; r.Y = Y; r.ScaleX = P1; r.ScaleY = P2; break;
                case CircleSave c: c.Name = Name; c.X = X; c.Y = Y; c.Radius = P1; break;
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

        public BulkShapePropsCommand(
            IReadOnlyList<object> shapes, Action mutate,
            IAppCommands commands, IApplicationEvents events, IObjectFinder objectFinder,
            string description)
        {
            _shapes = shapes;
            _mutate = mutate;
            _commands = commands;
            _events = events;
            _objectFinder = objectFinder;
            Description = description;
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
                var frame = shape switch
                {
                    AARectSave r => _objectFinder.GetAnimationFrameContaining(r),
                    CircleSave c => _objectFinder.GetAnimationFrameContaining(c),
                    _ => null,
                };
                if (frame is not null) _commands.RefreshTreeNode(frame);
            }
            _commands.RefreshAnimationFrameDisplay();
            _commands.RefreshWireframe();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
