using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Records a drag-move that shifts every frame of an animation chain by the same delta
    /// (<see cref="AnimationFrameSave.RelativeX"/>/<see cref="AnimationFrameSave.RelativeY"/>),
    /// preserving each frame's own starting offset, so the whole shift undoes/redoes as one step.
    /// Mirrors <see cref="MoveFrameOffsetCommand"/> for the single-frame drag, and
    /// <see cref="BulkFrameRegionChangedCommand"/> for the bulk-snapshot shape.
    /// </summary>
    public sealed class MoveFrameOffsetBulkCommand : IUndoableCommand
    {
        public readonly record struct FrameSnapshot(
            AnimationFrameSave Frame, float OldX, float OldY, float NewX, float NewY);

        private readonly IReadOnlyList<FrameSnapshot> _snapshots;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public MoveFrameOffsetBulkCommand(
            IReadOnlyList<FrameSnapshot> snapshots,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _snapshots = snapshots;
            _commands  = commands;
            _events    = events;
        }

        public string Description => "Move Animation";

        public bool Do() { Apply(useNew: true); return true; }
        public void Undo() => Apply(useNew: false);

        private void Apply(bool useNew)
        {
            foreach (var s in _snapshots)
            {
                s.Frame.RelativeX = useNew ? s.NewX : s.OldX;
                s.Frame.RelativeY = useNew ? s.NewY : s.OldY;
                _commands.RefreshTreeNode(s.Frame);
            }
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
