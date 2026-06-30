using AnimationEditor.Core.DragDrop;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Pure drop-target and selection-classification logic for frame drag-and-drop (issue #500).
/// </summary>
public class FrameDropResolverTests
{
    private static AnimationChainSave ChainWithFrames(int count, out List<AnimationFrameSave> frames)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < count; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"f{i}.png" });
        frames = chain.Frames.ToList();
        return chain;
    }

    [Fact]
    public void ClassifySelection_Empty_NoDrag()
    {
        var result = FrameDropResolver.ClassifySelection(new List<object>(), _ => null);
        Assert.Equal(FrameDragValidity.Empty, result.Validity);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClassifySelection_FramesFromOneChain_Valid()
    {
        var chain = ChainWithFrames(3, out var frames);
        var selection = new List<object> { frames[0], frames[2] };

        var result = FrameDropResolver.ClassifySelection(selection, _ => chain);

        Assert.True(result.IsValid);
        Assert.Same(chain, result.SourceChain);
        Assert.Equal(2, result.Frames.Count);
    }

    [Fact]
    public void ClassifySelection_FrameMixedWithChain_MixedTypes()
    {
        var chain = ChainWithFrames(2, out var frames);
        var selection = new List<object> { frames[0], chain };

        var result = FrameDropResolver.ClassifySelection(selection, _ => chain);

        Assert.Equal(FrameDragValidity.MixedTypes, result.Validity);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClassifySelection_FramesFromTwoChains_MultipleSourceChains()
    {
        var walk = ChainWithFrames(1, out var walkFrames);
        var run = ChainWithFrames(1, out var runFrames);
        var selection = new List<object> { walkFrames[0], runFrames[0] };

        var result = FrameDropResolver.ClassifySelection(
            selection,
            f => walk.Frames.Contains(f) ? walk : run.Frames.Contains(f) ? run : null);

        Assert.Equal(FrameDragValidity.MultipleSourceChains, result.Validity);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsFrameMultiSelectionContaining_FrameInMultiFrameSelection_True()
    {
        var chain = ChainWithFrames(3, out var frames);
        var selection = new List<object> { frames[0], frames[2] };

        Assert.True(FrameDropResolver.IsFrameMultiSelectionContaining(selection, frames[0]));
    }

    [Fact]
    public void IsFrameMultiSelectionContaining_SingleSelection_False()
    {
        var chain = ChainWithFrames(2, out var frames);
        var selection = new List<object> { frames[0] };

        Assert.False(FrameDropResolver.IsFrameMultiSelectionContaining(selection, frames[0]));
    }

    [Fact]
    public void IsFrameMultiSelectionContaining_MixedSelection_False()
    {
        var chain = ChainWithFrames(2, out var frames);
        var selection = new List<object> { frames[0], chain };

        Assert.False(FrameDropResolver.IsFrameMultiSelectionContaining(selection, frames[0]));
    }

    [Fact]
    public void IsFrameMultiSelectionContaining_CandidateNotInSelection_False()
    {
        var chain = ChainWithFrames(3, out var frames);
        var selection = new List<object> { frames[0], frames[1] };

        Assert.False(FrameDropResolver.IsFrameMultiSelectionContaining(selection, frames[2]));
    }

    [Fact]
    public void Resolve_ChainNode_AppendsToEnd()
    {
        var source = ChainWithFrames(2, out var srcFrames);
        var target = ChainWithFrames(3, out _);

        var result = FrameDropResolver.Resolve(
            target, FrameRowHalf.Upper, new[] { srcFrames[0] }, source, _ => source);

        Assert.Same(target, result.Chain);
        Assert.Equal(3, result.InsertIndex);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Resolve_FrameRowUpperHalf_InsertsBeforeThatFrame()
    {
        var source = ChainWithFrames(2, out var srcFrames);
        var target = ChainWithFrames(3, out var targetFrames);

        var result = FrameDropResolver.Resolve(
            targetFrames[1], FrameRowHalf.Upper, new[] { srcFrames[0] }, source,
            f => target.Frames.Contains(f) ? target : null);

        Assert.Equal(1, result.InsertIndex);
    }

    [Fact]
    public void Resolve_FrameRowLowerHalf_InsertsAfterThatFrame()
    {
        var source = ChainWithFrames(2, out var srcFrames);
        var target = ChainWithFrames(3, out var targetFrames);

        var result = FrameDropResolver.Resolve(
            targetFrames[1], FrameRowHalf.Lower, new[] { srcFrames[0] }, source,
            f => target.Frames.Contains(f) ? target : null);

        Assert.Equal(2, result.InsertIndex);
    }

    [Fact]
    public void Resolve_NonFrameNonChainNode_NoDrop()
    {
        var source = ChainWithFrames(1, out var srcFrames);

        var result = FrameDropResolver.Resolve(
            new AARectSave { Name = "Hit" }, FrameRowHalf.Upper, srcFrames, source, _ => source);

        Assert.False(result.IsValid);
        Assert.Null(result.Chain);
    }

    [Fact]
    public void Resolve_SameChainDropInsideSelectionSpan_Invalid()
    {
        // Frames 0..5; select indices 1,3,4. Span is [1,4]; dropping between 1 and 2
        // (insert index 2) is inside the span and must be rejected.
        var chain = ChainWithFrames(6, out var frames);
        var selected = new[] { frames[1], frames[3], frames[4] };

        var result = FrameDropResolver.Resolve(
            frames[2], FrameRowHalf.Upper, selected, chain, _ => chain);

        Assert.Equal(2, result.InsertIndex);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_SameChainDropAfterLastSelected_Valid()
    {
        // Same setup; dropping after the last frame (index 6) is outside the span → valid.
        var chain = ChainWithFrames(6, out var frames);
        var selected = new[] { frames[1], frames[3], frames[4] };

        var result = FrameDropResolver.Resolve(
            frames[5], FrameRowHalf.Lower, selected, chain, _ => chain);

        Assert.Equal(6, result.InsertIndex);
        Assert.True(result.IsValid);
    }
}
