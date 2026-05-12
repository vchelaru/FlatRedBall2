using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core
{
    public interface IObjectFinder
    {
        AnimationFrameSave? GetAnimationFrameContaining(AARectSave rectangle);
        AnimationFrameSave? GetAnimationFrameContaining(CircleSave circle);
        AnimationChainSave? GetAnimationChainContaining(AnimationFrameSave frame);
    }
}
