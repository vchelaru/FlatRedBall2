using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState
{
    /// <summary>One frame's flip pose before and after a flip, undo and redo included. See <see cref="IApplicationEvents.FramesFlipped"/>.</summary>
    public readonly record struct FrameFlipChange(AnimationFrameSave Frame, FlipPose Before, FlipPose After);
}
