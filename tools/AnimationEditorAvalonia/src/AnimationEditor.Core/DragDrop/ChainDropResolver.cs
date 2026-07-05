using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// The resolved landing spot for an animation-chain drag: the index the chain will be
/// inserted at in the chains list, interpreted against the current list before the dragged
/// chain is removed. <see cref="IsValid"/> is false when the drop is a no-op (landing on the
/// dragged chain's own slot) or has no valid target, in which case no indicator is shown.
/// </summary>
public readonly record struct ChainDropTarget(int InsertIndex, bool IsValid)
{
    public static readonly ChainDropTarget None = new(-1, false);
}

/// <summary>
/// Pure drag-and-drop logic for reordering top-level animation chains in the tree. Mirrors
/// <see cref="FrameDropResolver"/> but for chains; side-effect free so it can be unit-tested
/// without Avalonia.
/// </summary>
public static class ChainDropResolver
{
    /// <summary>
    /// Resolves where a single-chain drag would land given the tree node under the pointer.
    /// A chain node resolves to before it (upper half) or after it (lower half); a frame node
    /// resolves to just after its owning chain (so dropping over an expanded chain's frames
    /// lands after that chain); anything else is no drop. Dropping onto the dragged chain's
    /// own slot (its index or index+1) is a no-op and returns <see cref="ChainDropTarget.None"/>'s
    /// invalidity with the computed index.
    /// </summary>
    /// <param name="half">Which half of a chain row the pointer is over (ignored for frame targets).</param>
    /// <param name="draggedChain">The chain being dragged.</param>
    /// <param name="chains">The current top-level chain list.</param>
    /// <param name="getChainContainingFrame">Maps a target frame to its owning chain.</param>
    public static ChainDropTarget Resolve(
        object? nodeData,
        FrameRowHalf half,
        AnimationChainSave draggedChain,
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

        // Squash guard: an insert at the dragged chain's own index or index+1 leaves the list
        // unchanged for a single-item move, so surface the index but mark it invalid.
        int draggedIndex = IndexOf(chains, draggedChain);
        bool valid = insertIndex != draggedIndex && insertIndex != draggedIndex + 1;
        return new ChainDropTarget(insertIndex, valid);
    }

    private static int IndexOf(IReadOnlyList<AnimationChainSave> chains, AnimationChainSave chain)
    {
        for (int i = 0; i < chains.Count; i++)
            if (ReferenceEquals(chains[i], chain)) return i;
        return -1;
    }
}
