using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TreeBuilderFrameNamingTests
{
    // ── BuildFrameHeader ──────────────────────────────────────────────────────

    [Fact]
    public void BuildFrameHeader_AnyFrame_ReturnsPositionalLabel()
    {
        // Frame names are not user-overridable: identity is the index. The label is
        // always the computed "Frame N".
        var frame = new AnimationFrameSave { TextureName = "walk.png" };
        Assert.Equal("Frame 1", TreeBuilder.BuildFrameHeader(frame, index: 0));
    }

    [Fact]
    public void BuildFrameHeader_NonZeroIndex_ReturnsDynamicPositionalLabel()
    {
        var frame = new AnimationFrameSave();
        Assert.Equal("Frame 3", TreeBuilder.BuildFrameHeader(frame, index: 2));
    }

    // ── SyncFramesInto ────────────────────────────────────────────────────────

    [Fact]
    public void SyncFramesInto_AfterReorder_DynamicFrameUpdatesLabel()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var f1 = new AnimationFrameSave();
        var f2 = new AnimationFrameSave();
        chain.Frames.Add(f1);
        chain.Frames.Add(f2);

        var chainNode = TreeBuilder.BuildChainNode(chain);

        // Initial labels
        Assert.Equal("Frame 1", chainNode.Children[0].Header);
        Assert.Equal("Frame 2", chainNode.Children[1].Header);

        // Reorder: swap f1 and f2
        chain.Frames.RemoveAt(0);
        chain.Frames.Insert(1, f1);

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        // f2 is now at position 0 → "Frame 1"; f1 is at position 1 → "Frame 2"
        Assert.Equal("Frame 1", chainNode.Children[0].Header);
        Assert.Equal("Frame 2", chainNode.Children[1].Header);
    }

}
