using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Picks the frame a project-tree thumbnail (issue #839) is rendered from: the first frame of the
/// first chain that actually has frames. A chain can be empty (just created, or all frames deleted),
/// so this does not simply take <c>AnimationChains[0].Frames[0]</c>.
/// </summary>
public static class AchxThumbnailFrameSelector
{
    public static AnimationFrameSave? SelectFirstFrame(AnimationChainListSave acls)
    {
        foreach (var chain in acls.AnimationChains)
            if (chain.Frames.Count > 0)
                return chain.Frames[0];
        return null;
    }
}
