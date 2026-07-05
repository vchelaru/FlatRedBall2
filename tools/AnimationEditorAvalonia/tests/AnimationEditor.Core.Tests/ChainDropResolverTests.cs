using AnimationEditor.Core.DragDrop;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Pure drop-target and selection-classification logic for animation-chain (top-level node)
/// drag-and-drop reorder, including multi-chain selections (issue #566).
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
        var dragged = new[] { chains[0] };

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
        var dragged = new[] { chains[2] };

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
        var dragged = new[] { chains[0] };
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
            new AARectSave { Name = "Hit" }, FrameRowHalf.Upper, new[] { chains[0] }, chains, _ => null);

        Assert.False(result.IsValid);
        Assert.Equal(-1, result.InsertIndex);
    }

    [Fact]
    public void Resolve_NullNode_None()
    {
        var chains = Chains("A", "B");

        var result = ChainDropResolver.Resolve(
            null, FrameRowHalf.Upper, new[] { chains[0] }, chains, _ => null);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_MultiChain_DropInsideSelectionSpan_Invalid()
    {
        // Chains A..F; select indices 1,3,4. Span is [1,4]; dropping between 1 and 2
        // (insert index 2) is inside the span and must be rejected.
        var chains = Chains("A", "B", "C", "D", "E", "F");
        var selected = new[] { chains[1], chains[3], chains[4] };

        var result = ChainDropResolver.Resolve(
            chains[2], FrameRowHalf.Upper, selected, chains, _ => null);

        Assert.Equal(2, result.InsertIndex);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_MultiChain_DropAfterLastSelected_Valid()
    {
        // Same setup; dropping after the last chain (index 6) is outside the span → valid.
        var chains = Chains("A", "B", "C", "D", "E", "F");
        var selected = new[] { chains[1], chains[3], chains[4] };

        var result = ChainDropResolver.Resolve(
            chains[5], FrameRowHalf.Lower, selected, chains, _ => null);

        Assert.Equal(6, result.InsertIndex);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Resolve_MultiChain_DropBeforeFirstSelected_Valid()
    {
        var chains = Chains("A", "B", "C", "D", "E", "F");
        var selected = new[] { chains[1], chains[3], chains[4] };

        var result = ChainDropResolver.Resolve(
            chains[0], FrameRowHalf.Upper, selected, chains, _ => null);

        Assert.Equal(0, result.InsertIndex);
        Assert.True(result.IsValid);
    }

    // ── ClassifySelection ────────────────────────────────────────────────────

    [Fact]
    public void ClassifySelection_Empty_NoDrag()
    {
        var result = ChainDropResolver.ClassifySelection(new List<object>());
        Assert.Equal(ChainDragValidity.Empty, result.Validity);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClassifySelection_MultipleChains_Valid()
    {
        var chains = Chains("A", "B", "C");
        var selection = new List<object> { chains[0], chains[2] };

        var result = ChainDropResolver.ClassifySelection(selection);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Chains.Count);
    }

    [Fact]
    public void ClassifySelection_ChainMixedWithFrame_MixedTypes()
    {
        var chains = Chains("A", "B");
        var frame = new AnimationFrameSave { TextureName = "f.png" };
        var selection = new List<object> { chains[0], frame };

        var result = ChainDropResolver.ClassifySelection(selection);

        Assert.Equal(ChainDragValidity.MixedTypes, result.Validity);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClassifySelection_NoChains_NotChains()
    {
        var frame = new AnimationFrameSave { TextureName = "f.png" };
        var selection = new List<object> { frame };

        var result = ChainDropResolver.ClassifySelection(selection);

        Assert.Equal(ChainDragValidity.NotChains, result.Validity);
        Assert.False(result.IsValid);
    }

    // ── IsChainMultiSelectionContaining ──────────────────────────────────────

    [Fact]
    public void IsChainMultiSelectionContaining_ChainInMultiChainSelection_True()
    {
        var chains = Chains("A", "B", "C");
        var selection = new List<object> { chains[0], chains[2] };

        Assert.True(ChainDropResolver.IsChainMultiSelectionContaining(selection, chains[0]));
    }

    [Fact]
    public void IsChainMultiSelectionContaining_SingleSelection_False()
    {
        var chains = Chains("A", "B");
        var selection = new List<object> { chains[0] };

        Assert.False(ChainDropResolver.IsChainMultiSelectionContaining(selection, chains[0]));
    }

    [Fact]
    public void IsChainMultiSelectionContaining_MixedSelection_False()
    {
        var chains = Chains("A", "B");
        var frame = new AnimationFrameSave { TextureName = "f.png" };
        var selection = new List<object> { chains[0], frame };

        Assert.False(ChainDropResolver.IsChainMultiSelectionContaining(selection, chains[0]));
    }

    [Fact]
    public void IsChainMultiSelectionContaining_CandidateNotInSelection_False()
    {
        var chains = Chains("A", "B", "C");
        var selection = new List<object> { chains[0], chains[1] };

        Assert.False(ChainDropResolver.IsChainMultiSelectionContaining(selection, chains[2]));
    }
}
