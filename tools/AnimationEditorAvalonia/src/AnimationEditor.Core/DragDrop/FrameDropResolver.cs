using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.DragDrop;

/// <summary>Which half of a frame row the pointer is over during a frame drag.</summary>
public enum FrameRowHalf { Upper, Lower }

/// <summary>
/// Why a frame drag may or may not be initiated from the current selection.
/// <see cref="Valid"/> is the only state that starts a drag; <see cref="MixedTypes"/>
/// and <see cref="MultipleSourceChains"/> warrant an error/info toast, while
/// <see cref="Empty"/> and <see cref="NotFrames"/> silently suppress the drag.
/// </summary>
public enum FrameDragValidity { Valid, Empty, NotFrames, MixedTypes, MultipleSourceChains }

/// <summary>
/// The frames a drag would move plus where they come from. <see cref="SourceChain"/>
/// is non-null only when <see cref="Validity"/> is <see cref="FrameDragValidity.Valid"/>.
/// </summary>
public readonly record struct FrameDragSource(
    IReadOnlyList<AnimationFrameSave> Frames,
    AnimationChainSave? SourceChain,
    FrameDragValidity Validity)
{
    public bool IsValid => Validity == FrameDragValidity.Valid;
}

/// <summary>
/// The resolved landing spot for a frame drag: the target chain and the index its
/// frames will be inserted at (interpreted against the target chain's current frame
/// list, before any source removal). <see cref="IsValid"/> is false when the drop
/// must be rejected and no indicator line shown.
/// </summary>
public readonly record struct FrameDropTarget(
    AnimationChainSave? Chain, int InsertIndex, bool IsValid)
{
    public static readonly FrameDropTarget None = new(null, -1, false);
}

/// <summary>
/// Pure drag-and-drop logic for reordering / moving animation frames in the tree.
/// Splits into two questions: <see cref="ClassifySelection"/> (can this selection be
/// dragged, and from where) and <see cref="Resolve"/> (where would a drop land).
/// Both are side-effect free so they can be unit-tested without Avalonia.
/// </summary>
public static class FrameDropResolver
{
    /// <summary>
    /// Classifies the current tree selection for drag eligibility. A homogeneous set of
    /// frames from a single source chain is <see cref="FrameDragValidity.Valid"/>; any
    /// non-frame mixed in is <see cref="FrameDragValidity.MixedTypes"/>; frames spanning
    /// more than one chain is <see cref="FrameDragValidity.MultipleSourceChains"/>
    /// (deferred to a follow-up). Pure non-frame or empty selections do not drag.
    /// </summary>
    public static FrameDragSource ClassifySelection(
        IReadOnlyList<object>? selectedNodes,
        Func<AnimationFrameSave, AnimationChainSave?> getChainContainingFrame)
    {
        if (selectedNodes is null || selectedNodes.Count == 0)
            return new(Array.Empty<AnimationFrameSave>(), null, FrameDragValidity.Empty);

        var frames = selectedNodes.OfType<AnimationFrameSave>().ToList();

        if (frames.Count == 0)
            return new(frames, null, FrameDragValidity.NotFrames);

        if (frames.Count != selectedNodes.Count)
            return new(frames, null, FrameDragValidity.MixedTypes);

        var chains = frames.Select(getChainContainingFrame).Distinct().ToList();
        if (chains.Count != 1 || chains[0] is null)
            return new(frames, null, FrameDragValidity.MultipleSourceChains);

        return new(frames, chains[0], FrameDragValidity.Valid);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is one of several frames in a homogeneous
    /// frame multi-selection. Pressing such a frame must NOT collapse the selection (so a
    /// drag can move the whole set); a plain click that never drags collapses on release
    /// instead. Returns false for single selections or any selection containing a non-frame.
    /// </summary>
    public static bool IsFrameMultiSelectionContaining(
        IReadOnlyList<object>? selection, AnimationFrameSave candidate)
    {
        if (selection is null || selection.Count < 2) return false;
        if (!selection.All(n => n is AnimationFrameSave)) return false;
        return selection.Any(n => ReferenceEquals(n, candidate));
    }

    /// <summary>
    /// Resolves where a frame drag would land given the tree node currently under the
    /// pointer. <paramref name="nodeData"/> is the node's underlying model object: a frame
    /// resolves to upper-half-before / lower-half-after that frame; a chain appends to the
    /// end of its frame list; anything else is no drop.
    /// </summary>
    /// <param name="half">Which half of a frame row the pointer is over (ignored for chain targets).</param>
    /// <param name="selectedFrames">The frames being dragged, in any order.</param>
    /// <param name="sourceChain">The chain the dragged frames belong to.</param>
    /// <param name="getChainContainingFrame">Maps the target frame to its owning chain.</param>
    public static FrameDropTarget Resolve(
        object? nodeData,
        FrameRowHalf half,
        IReadOnlyList<AnimationFrameSave> selectedFrames,
        AnimationChainSave sourceChain,
        Func<AnimationFrameSave, AnimationChainSave?> getChainContainingFrame)
    {
        if (selectedFrames is null || selectedFrames.Count == 0)
            return FrameDropTarget.None;

        AnimationChainSave? targetChain;
        int insertIndex;

        switch (nodeData)
        {
            case AnimationFrameSave frame:
                targetChain = getChainContainingFrame(frame);
                if (targetChain is null) return FrameDropTarget.None;
                int frameIndex = targetChain.Frames.IndexOf(frame);
                if (frameIndex < 0) return FrameDropTarget.None;
                insertIndex = half == FrameRowHalf.Upper ? frameIndex : frameIndex + 1;
                break;

            case AnimationChainSave chain:
                targetChain = chain;
                insertIndex = chain.Frames.Count;
                break;

            default:
                return FrameDropTarget.None;
        }

        // Same-chain accidental-squash guard: a drop landing strictly inside the index
        // span of the selection would silently collapse the block. Only allow inserts at
        // or before the first selected frame, or at/after the last selected frame.
        if (ReferenceEquals(targetChain, sourceChain))
        {
            var indices = selectedFrames
                .Select(f => targetChain.Frames.IndexOf(f))
                .Where(i => i >= 0)
                .OrderBy(i => i)
                .ToList();
            if (indices.Count == 0) return FrameDropTarget.None;

            int minSel = indices[0];
            int maxSel = indices[^1];
            bool valid = insertIndex <= minSel || insertIndex >= maxSel + 1;
            return new FrameDropTarget(targetChain, insertIndex, valid);
        }

        return new FrameDropTarget(targetChain, insertIndex, true);
    }
}
