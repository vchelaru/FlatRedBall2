using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Records a drag that moved several selected collision shapes together, so the whole
    /// move undoes and redoes as one step. <see cref="MoveShapeCommand"/> covers the single-shape drag.
    /// </summary>
    public sealed class MoveShapesCommand : IUndoableCommand
    {
        public readonly record struct ShapeSnapshot(
            AnimationFrameSave Frame, ShapeSave Shape, float OldX, float OldY, float NewX, float NewY);

        private readonly IReadOnlyList<ShapeSnapshot> _snapshots;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public MoveShapesCommand(
            IReadOnlyList<ShapeSnapshot> snapshots,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _snapshots = snapshots;
            _commands  = commands;
            _events    = events;
        }

        public string Description => $"Move {_snapshots.Count} Shapes";

        public bool Do() { Apply(useNew: true); return true; }
        public void Undo() => Apply(useNew: false);

        private void Apply(bool useNew)
        {
            foreach (var s in _snapshots)
            {
                s.Shape.X = useNew ? s.NewX : s.OldX;
                s.Shape.Y = useNew ? s.NewY : s.OldY;
                _commands.RefreshTreeNode(s.Frame);
            }
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
