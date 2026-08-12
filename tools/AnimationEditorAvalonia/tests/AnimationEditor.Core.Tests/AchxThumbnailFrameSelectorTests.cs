using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #839: project-tree thumbnails render the first frame of the first non-empty chain.
public class AchxThumbnailFrameSelectorTests
{
    [Fact]
    public void SelectFirstFrame_FirstChainEmpty_ReturnsFrameFromNextNonEmptyChain()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Empty" });
        var expected = new AnimationFrameSave { TextureName = "walk.png" };
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk", Frames = { expected } });

        var result = AchxThumbnailFrameSelector.SelectFirstFrame(acls);

        Assert.Same(expected, result);
    }

    [Fact]
    public void SelectFirstFrame_FirstChainHasFrames_ReturnsItsFirstFrame()
    {
        var acls = new AnimationChainListSave();
        var expected = new AnimationFrameSave { TextureName = "idle.png" };
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle", Frames = { expected, new AnimationFrameSave() } });

        var result = AchxThumbnailFrameSelector.SelectFirstFrame(acls);

        Assert.Same(expected, result);
    }

    [Fact]
    public void SelectFirstFrame_NoChainsHaveFrames_ReturnsNull()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Empty" });

        var result = AchxThumbnailFrameSelector.SelectFirstFrame(acls);

        Assert.Null(result);
    }
}
