using AnimationEditor.Core.DragDrop;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Pure drop-target logic for animation-chain (top-level node) drag-and-drop reorder.
/// </summary>
public class ChainDropResolverTests
{
    private static List<AnimationChainSave> Chains(params string[] names)
    {
        var list = new List<AnimationChainSave>();
        foreach (var name in names)
            list.Add(new AnimationChainSave { Name = name });
        return list;
    }

    [Fact]
    public void Resolve_ChainNodeLowerHalf_InsertsAfterThatChain()
    {
        var chains = Chains("A", "B", "C");
        var dragged = chains[0];

        // Drop A onto the lower half of B (index 1) → insert after B.
        var result = ChainDropResolver.Resolve(
            chains[1], FrameRowHalf.Lower, dragged, chains, _ => null);

        Assert.Equal(2, result.InsertIndex);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Resolve_ChainNodeUpperHalf_InsertsBeforeThatChain()
    {
        var chains = Chains("A", "B", "C");
        var dragged = chains[2];

        // Drop C onto the upper half of B (index 1) → insert before B.
        var result = ChainDropResolver.Resolve(
            chains[1], FrameRowHalf.Upper, dragged, chains, _ => null);

        Assert.Equal(1, result.InsertIndex);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Resolve_FrameNode_MapsToAfterOwningChain()
    {
        var chains = Chains("A", "B", "C");
        var dragged = chains[0];
        var frame = new AnimationFrameSave { TextureName = "f.png" };
        chains[1].Frames.Add(frame);

        // Pointer over a frame owned by B (index 1) → land after B.
        var result = ChainDropResolver.Resolve(
            frame, FrameRowHalf.Lower, dragged, chains, _ => chains[1]);

        Assert.Equal(2, result.InsertIndex);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Resolve_NonChainNonFrameNode_None()
    {
        var chains = Chains("A", "B");

        var result = ChainDropResolver.Resolve(
            new AARectSave { Name = "Hit" }, FrameRowHalf.Upper, chains[0], chains, _ => null);

        Assert.False(result.IsValid);
        Assert.Equal(-1, result.InsertIndex);
    }

    [Fact]
    public void Resolve_NullNode_None()
    {
        var chains = Chains("A", "B");

        var result = ChainDropResolver.Resolve(
            null, FrameRowHalf.Upper, chains[0], chains, _ => null);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_OwnIndex_Invalid()
    {
        var chains = Chains("A", "B", "C");
        var dragged = chains[1];

        // Dropping B on its own upper half resolves to its own index → no-op.
        var result = ChainDropResolver.Resolve(
            chains[1], FrameRowHalf.Upper, dragged, chains, _ => null);

        Assert.Equal(1, result.InsertIndex);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_OwnIndexPlusOne_Invalid()
    {
        var chains = Chains("A", "B", "C");
        var dragged = chains[1];

        // Dropping B on its own lower half resolves to index+1 → still a no-op.
        var result = ChainDropResolver.Resolve(
            chains[1], FrameRowHalf.Lower, dragged, chains, _ => null);

        Assert.Equal(2, result.InsertIndex);
        Assert.False(result.IsValid);
    }
}
