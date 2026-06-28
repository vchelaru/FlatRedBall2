using AnimationEditor.Core.DragDrop;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TreePngDropTargetTests
{
    [Fact]
    public void FromNodeData_FrameNode_ReturnsFrameAndParentChain()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "old.png" };
        chain.Frames.Add(frame);

        var (resolvedChain, resolvedFrame) = TreePngDropTarget.FromNodeData(
            frame,
            f => ReferenceEquals(f, frame) ? chain : null);

        Assert.Same(chain, resolvedChain);
        Assert.Same(frame, resolvedFrame);
    }

    [Fact]
    public void FromNodeData_ChainNode_ReturnsChainWithoutFrame()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave());

        var (resolvedChain, resolvedFrame) = TreePngDropTarget.FromNodeData(
            chain,
            _ => null);

        Assert.Same(chain, resolvedChain);
        Assert.Null(resolvedFrame);
    }

    [Fact]
    public void FromNodeData_BlankTreeChrome_ReturnsNullTargets()
    {
        var selectedChain = new AnimationChainSave { Name = "Selected" };
        selectedChain.Frames.Add(new AnimationFrameSave { TextureName = "keep.png" });

        var (resolvedChain, resolvedFrame) = TreePngDropTarget.FromNodeData(
            null,
            _ => selectedChain);

        Assert.Null(resolvedChain);
        Assert.Null(resolvedFrame);
    }

    [Fact]
    public void FromNodeData_BlankTreeChrome_ComputePngDropDoesNotUpdateSelectedChain()
    {
        var selectedChain = new AnimationChainSave { Name = "Selected" };
        var frame = new AnimationFrameSave { TextureName = "keep.png" };
        selectedChain.Frames.Add(frame);
        string png = TestPaths.Abs("Project", "Content", "NewTex.png");
        string achx = TestPaths.Abs("Project", "Animations", "Player.achx");

        var (chain, targetFrame) = TreePngDropTarget.FromNodeData(null, _ => selectedChain);

        var (result, _) = TextureDropProcessor.ComputePngDrop(
            chain,
            targetFrame,
            png,
            achx,
            createFrameOnCtrl: false);

        Assert.Equal(TextureDropResult.NotApplied, result);
        Assert.Equal("keep.png", frame.TextureName);
    }

    [Fact]
    public void FromNodeData_ShapeNode_ReturnsNullTargets()
    {
        var rect = new AARectSave { Name = "Hitbox" };

        var (resolvedChain, resolvedFrame) = TreePngDropTarget.FromNodeData(
            rect,
            _ => throw new InvalidOperationException("Should not resolve chain for shapes"));

        Assert.Null(resolvedChain);
        Assert.Null(resolvedFrame);
    }
}
