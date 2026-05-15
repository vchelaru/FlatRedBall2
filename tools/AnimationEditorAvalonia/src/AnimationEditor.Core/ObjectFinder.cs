using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core
{
    public class ObjectFinder : IObjectFinder
    {
        private readonly IProjectManager _pm;

        public ObjectFinder(IProjectManager pm)
        {
            _pm = pm;
        }
        public AnimationFrameSave? GetAnimationFrameContaining(AARectSave rectangle)
        {
            foreach (var chain in _pm.AnimationChainListSave?.AnimationChains ?? [])
            {
                foreach (var frame in chain.Frames)
                {
                    if (frame.ShapesSave?.Shapes.Contains(rectangle) == true)
                        return frame;
                }
            }
            return null;
        }

        public AnimationFrameSave? GetAnimationFrameContaining(CircleSave circle)
        {
            foreach (var chain in _pm.AnimationChainListSave?.AnimationChains ?? [])
            {
                foreach (var frame in chain.Frames)
                {
                    if (frame.ShapesSave?.Shapes.Contains(circle) == true)
                        return frame;
                }
            }
            return null;
        }

        public AnimationChainSave? GetAnimationChainContaining(AnimationFrameSave frame)
        {
            foreach (var chain in _pm.AnimationChainListSave?.AnimationChains ?? [])
            {
                foreach (var possibleFrame in chain.Frames)
                {
                    if (possibleFrame == frame)
                        return chain;
                }
            }
            return null;
        }
    }
}
