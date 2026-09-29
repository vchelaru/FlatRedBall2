using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core
{
    public interface IObjectFinder
    {
        AnimationFrameSave? GetAnimationFrameContaining(ShapeSave shape);
        AnimationChainSave? GetAnimationChainContaining(AnimationFrameSave frame);
    }
}
