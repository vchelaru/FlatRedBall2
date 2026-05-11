using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;

namespace AnimationEditor.Core
{
    public interface IObjectFinder
    {
        AnimationFrameSave? GetAnimationFrameContaining(AxisAlignedRectangleSave rectangle);
        AnimationFrameSave? GetAnimationFrameContaining(CircleSave circle);
        AnimationChainSave? GetAnimationChainContaining(AnimationFrameSave frame);
    }
}
