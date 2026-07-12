using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>Which mirror axis a <see cref="FlipCommand"/> toggles.</summary>
    internal enum FlipAxis { Horizontal, Vertical, Diagonal }

    /// <summary>
    /// Undo/redo record for toggling a flip flag on a set of frames (frame flip, or whole-chain
    /// flip). A flip is its own inverse, so <see cref="Do"/>, <see cref="Undo"/>, and Redo all
    /// re-toggle the same frames.
    /// </summary>
    internal sealed class FlipCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<AnimationFrameSave> _frames;
        private readonly FlipAxis _axis;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly System.Action _refresh;

        public string Description { get; }

        public FlipCommand(
            IReadOnlyList<AnimationFrameSave> frames, FlipAxis axis,
            IAppCommands commands, IApplicationEvents events, System.Action refresh)
        {
            _frames = frames;
            _axis = axis;
            _commands = commands;
            _events = events;
            _refresh = refresh;
            string axisLabel = axis switch
            {
                FlipAxis.Horizontal => "Horizontal",
                FlipAxis.Vertical => "Vertical",
                _ => "Diagonal",
            };
            Description = frames.Count == 1
                ? $"Flip {axisLabel}"
                : $"Flip {frames.Count} Frames {axisLabel}";
        }

        public bool Do() { Toggle(); return true; }
        public void Undo() => Toggle();

        private void Toggle()
        {
            foreach (var frame in _frames)
            {
                switch (_axis)
                {
                    case FlipAxis.Horizontal:
                        frame.FlipHorizontal = !frame.FlipHorizontal;
                        frame.RelativeX = -frame.RelativeX;   // mirror sprite offset about the entity origin
                        break;
                    case FlipAxis.Vertical:
                        frame.FlipVertical = !frame.FlipVertical;
                        frame.RelativeY = -frame.RelativeY;
                        break;
                    case FlipAxis.Diagonal:
                        frame.FlipDiagonal = !frame.FlipDiagonal;
                        // RelativeX/RelativeY are already baked for the frame's current H/V state, so
                        // toggling diagonal must apply the delta between the old and new baked state,
                        // not a fixed transpose: a plain (x,y) -> (y,x) swap when H and V currently
                        // agree, a negated (x,y) -> (-y,-x) swap when exactly one is set. See
                        // ShapeFlip.Transpose's remarks for the derivation — using a fixed swap
                        // regardless of H/V put the offset in the wrong spot whenever diagonal was
                        // toggled after horizontal or vertical (issue #592 follow-up).
                        bool negateRelative = frame.FlipHorizontal ^ frame.FlipVertical;
                        (frame.RelativeX, frame.RelativeY) = negateRelative
                            ? (-frame.RelativeY, -frame.RelativeX)
                            : (frame.RelativeY, frame.RelativeX);
                        break;
                }

                // Mirror/transpose attached shape offsets about the same origin so collision geometry
                // tracks the flipped/transposed sprite. Both operations are self-inverse, so undo/redo
                // (which re-toggle) restore the sprite offset and the shape offsets exactly.
                if (frame.ShapesSave != null)
                    foreach (var shape in frame.ShapesSave.Shapes)
                    {
                        if (_axis == FlipAxis.Diagonal)
                            ShapeFlip.Transpose(shape, frame.FlipHorizontal, frame.FlipVertical);
                        else
                            ShapeFlip.Mirror(shape, _axis == FlipAxis.Horizontal, _axis == FlipAxis.Vertical);
                    }
            }
            _refresh();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
