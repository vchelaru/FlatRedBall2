using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for toggling the horizontal or vertical flip flag on a set
    /// of frames (frame flip, or whole-chain flip). A flip is its own inverse, so
    /// <see cref="Do"/>, <see cref="Undo"/>, and Redo all re-toggle the same frames.
    /// </summary>
    internal sealed class FlipCommand : IUndoableCommand
    {
        private readonly IReadOnlyList<AnimationFrameSave> _frames;
        private readonly bool _horizontal;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly System.Action _refresh;

        public string Description { get; }

        public FlipCommand(
            IReadOnlyList<AnimationFrameSave> frames, bool horizontal,
            IAppCommands commands, IApplicationEvents events, System.Action refresh)
        {
            _frames = frames;
            _horizontal = horizontal;
            _commands = commands;
            _events = events;
            _refresh = refresh;
            Description = horizontal ? "Flip Horizontal" : "Flip Vertical";
        }

        public bool Do() { Toggle(); return true; }
        public void Undo() => Toggle();

        private void Toggle()
        {
            foreach (var frame in _frames)
            {
                if (_horizontal)
                {
                    frame.FlipHorizontal = !frame.FlipHorizontal;
                    frame.RelativeX = -frame.RelativeX;   // mirror sprite offset about the entity origin
                }
                else
                {
                    frame.FlipVertical = !frame.FlipVertical;
                    frame.RelativeY = -frame.RelativeY;
                }

                // Mirror attached shape offsets about the same origin so collision geometry tracks
                // the flipped sprite. Negation is its own inverse, so undo/redo (which re-toggle)
                // restore both the sprite offset and the shape offsets exactly.
                if (frame.ShapesSave != null)
                    foreach (var shape in frame.ShapesSave.Shapes)
                        ShapeFlip.Mirror(shape, _horizontal, !_horizontal);
            }
            _refresh();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
