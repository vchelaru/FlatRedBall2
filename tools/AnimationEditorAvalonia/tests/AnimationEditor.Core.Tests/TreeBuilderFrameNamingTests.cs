using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TreeBuilderFrameNamingTests
{
    // ── BuildFrameHeader ──────────────────────────────────────────────────────

    [Fact]
    public void BuildFrameHeader_HasCustomName_ReturnsCustomName()
    {
        var frame = new AnimationFrameSave { HasCustomName = true, Name = "Hit Frame" };
        Assert.Equal("Hit Frame", TreeBuilder.BuildFrameHeader(frame, index: 0));
    }

    [Fact]
    public void BuildFrameHeader_NoCustomName_ReturnsDynamicPositionalLabel()
    {
        var frame = new AnimationFrameSave { HasCustomName = false };
        Assert.Equal("Frame 3", TreeBuilder.BuildFrameHeader(frame, index: 2));
    }

    [Fact]
    public void BuildFrameHeader_NoCustomNameButNameSet_IgnoresNameAndReturnsDynamic()
    {
        // Old persisted data: Name is set but HasCustomName was not (pre-migration).
        // The editor must display positional label, not the stale Name.
        var frame = new AnimationFrameSave { HasCustomName = false, Name = "Frame 1" };
        Assert.Equal("Frame 3", TreeBuilder.BuildFrameHeader(frame, index: 2));
    }

    // ── BuildFrameNode ────────────────────────────────────────────────────────

    [Fact]
    public void BuildFrameNode_AutoNamed_DoesNotPersistNameToModel()
    {
        var frame = new AnimationFrameSave();

        TreeBuilder.BuildFrameNode(frame, 0);

        Assert.False(frame.HasCustomName);
        Assert.Equal(string.Empty, frame.Name);
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

    [Fact]
    public void SyncFramesInto_CustomNamedFrame_LabelDoesNotChangeOnReorder()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var f1 = new AnimationFrameSave { HasCustomName = true, Name = "Jump Frame" };
        var f2 = new AnimationFrameSave();
        chain.Frames.Add(f1);
        chain.Frames.Add(f2);

        var chainNode = TreeBuilder.BuildChainNode(chain);

        // Reorder: move f2 to front
        chain.Frames.RemoveAt(1);
        chain.Frames.Insert(0, f2);

        TreeBuilder.SyncFramesInto(chainNode, chain.Frames);

        // f2 at index 0 → dynamic "Frame 1"
        Assert.Equal("Frame 1", chainNode.Children[0].Header);
        // f1 at index 1 → custom name survives
        Assert.Equal("Jump Frame", chainNode.Children[1].Header);
    }
}
