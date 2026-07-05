using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// The resolved landing spot for an animation-chain drag: the index the chain(s) will be
/// inserted at in the chains list, interpreted against the current list before the dragged
/// chains are removed. <see cref="IsValid"/> is false when the drop would land inside the
/// dragged selection's own span (a no-op / ambiguous squash) or has no valid target, in which
/// case no indicator is shown.
/// </summary>
public readonly record struct ChainDropTarget(int InsertIndex, bool IsValid)
{
    public static readonly ChainDropTarget None = new(-1, false);
}

/// <summary>
/// Why a chain drag may or may not be initiated from the current selection. Mirrors
/// <see cref="FrameDragValidity"/>: <see cref="Valid"/> is the only state that starts a drag;
/// <see cref="MixedTypes"/> warrants an error/info toast, while <see cref="Empty"/> and
/// <see cref="NotChains"/> silently suppress the drag.
/// </summary>
public enum ChainDragValidity { Valid, Empty, NotChains, MixedTypes }

/// <summary>
/// The chains a drag would move. <see cref="Chains"/> is non-empty only when
/// <see cref="Validity"/> is <see cref="ChainDragValidity.Valid"/>.
/// </summary>
public readonly record struct ChainDragSource(
    IReadOnlyList<AnimationChainSave> Chains,
    ChainDragValidity Validity)
{
    public bool IsValid => Validity == ChainDragValidity.Valid;
}

/// <summary>
/// Pure drag-and-drop logic for reordering top-level animation chains in the tree. Mirrors
/// <see cref="FrameDropResolver"/> but for chains; side-effect free so it can be unit-tested
/// without Avalonia.
/// </summary>
public static class ChainDropResolver
{
    /// <summary>
    /// Classifies the current tree selection for drag eligibility. A homogeneous set of
    /// chains is <see cref="ChainDragValidity.Valid"/>; any non-chain mixed in is
    /// <see cref="ChainDragValidity.MixedTypes"/>. Pure non-chain or empty selections do
    /// not drag. Mirrors <see cref="FrameDropResolver.ClassifySelection"/>.
    /// </summary>
    public static ChainDragSource ClassifySelection(IReadOnlyList<object>? selectedNodes)
    {
        if (selectedNodes is null || selectedNodes.Count == 0)
            return new(Array.Empty<AnimationChainSave>(), ChainDragValidity.Empty);

        var chains = selectedNodes.OfType<AnimationChainSave>().ToList();

        if (chains.Count == 0)
            return new(chains, ChainDragValidity.NotChains);

        if (chains.Count != selectedNodes.Count)
            return new(chains, ChainDragValidity.MixedTypes);

        return new(chains, ChainDragValidity.Valid);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is one of several chains in a homogeneous
    /// chain multi-selection. Pressing such a chain must NOT collapse the selection (so a
    /// drag can move the whole set); a plain click that never drags collapses on release
    /// instead. Returns false for single selections or any selection containing a non-chain.
    /// Mirrors <see cref="FrameDropResolver.IsFrameMultiSelectionContaining"/>.
    /// </summary>
    public static bool IsChainMultiSelectionContaining(
        IReadOnlyList<object>? selection, AnimationChainSave candidate)
    {
        if (selection is null || selection.Count < 2) return false;
        if (!selection.All(n => n is AnimationChainSave)) return false;
        return selection.Any(n => ReferenceEquals(n, candidate));
    }

    /// <summary>
    /// Resolves where a chain drag would land given the tree node under the pointer.
    /// A chain node resolves to before it (upper half) or after it (lower half); a frame node
    /// resolves to just after its owning chain (so dropping over an expanded chain's frames
    /// lands after that chain); anything else is no drop. A drop landing strictly inside the
    /// index span of <paramref name="draggedChains"/> is rejected as a no-op / ambiguous
    /// squash (mirrors <see cref="FrameDropResolver.Resolve"/>'s same-chain guard).
    /// </summary>
    /// <param name="half">Which half of a chain row the pointer is over (ignored for frame targets).</param>
    /// <param name="draggedChains">The chain(s) being dragged.</param>
    /// <param name="chains">The current top-level chain list.</param>
    /// <param name="getChainContainingFrame">Maps a target frame to its owning chain.</param>
    public static ChainDropTarget Resolve(
        object? nodeData,
        FrameRowHalf half,
        IReadOnlyList<AnimationChainSave> draggedChains,
        IReadOnlyList<AnimationChainSave> chains,
        Func<AnimationFrameSave, AnimationChainSave?> getChainContainingFrame)
    {
        int insertIndex;

        switch (nodeData)
        {
            case AnimationChainSave chain:
                int chainIndex = IndexOf(chains, chain);
                if (chainIndex < 0) return ChainDropTarget.None;
                insertIndex = half == FrameRowHalf.Upper ? chainIndex : chainIndex + 1;
                break;

            case AnimationFrameSave frame:
                var owner = getChainContainingFrame(frame);
                if (owner is null) return ChainDropTarget.None;
                int ownerIndex = IndexOf(chains, owner);
                if (ownerIndex < 0) return ChainDropTarget.None;
                insertIndex = ownerIndex + 1;
                break;

            default:
                return ChainDropTarget.None;
        }

        var indices = draggedChains
            .Select(c => IndexOf(chains, c))
            .Where(i => i >= 0)
            .OrderBy(i => i)
            .ToList();
        if (indices.Count == 0) return ChainDropTarget.None;

        int minSel = indices[0];
        int maxSel = indices[^1];
        bool valid = insertIndex <= minSel || insertIndex >= maxSel + 1;
        return new ChainDropTarget(insertIndex, valid);
    }

    private static int IndexOf(IReadOnlyList<AnimationChainSave> chains, AnimationChainSave chain)
    {
        for (int i = 0; i < chains.Count; i++)
            if (ReferenceEquals(chains[i], chain)) return i;
        return -1;
    }
}
