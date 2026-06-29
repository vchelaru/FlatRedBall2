using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

public enum CopySelectionKind
{
    Chain,
    Frame,
    Shape,
}

/// <summary>
/// Homogeneous multi-select payload for copy, paste, and duplicate.
/// </summary>
public sealed class CopySelectionPayload
{
    public CopySelectionKind Kind { get; init; }
    public IReadOnlyList<AnimationChainSave> Chains { get; init; } = [];
    public IReadOnlyList<AnimationFrameSave> Frames { get; init; } = [];
    public IReadOnlyList<object> Shapes { get; init; } = [];
}

/// <summary>
/// Validates tree selection and collects same-kind items for clipboard operations.
/// Mirrors delete dispatch: focused node kind wins; incidental parent selection is ignored.
/// When copy fails with a user-visible <c>failureMessage</c>, the app clears the animation clipboard
/// so paste cannot silently reuse a prior copy.
/// </summary>
public static class SelectionCopyContext
{
    public const string MixedSelectionMessage =
        "Can't copy a mixed selection — select only animations, only frames, or only shapes.";

    public static bool TryGet(
        ISelectedState state,
        IObjectFinder finder,
        AnimationChainListSave? acls,
        out CopySelectionPayload payload,
        out string? failureMessage)
    {
        payload = null!;
        failureMessage = null;

        if (!TryGetFocusedKind(state, out var kind))
        {
            failureMessage = "Nothing selected to copy.";
            return false;
        }

        if (HasForeignKindsInSelectedNodes(state, kind))
        {
            failureMessage = MixedSelectionMessage;
            return false;
        }

        switch (kind)
        {
            case CopySelectionKind.Shape:
                return TryGetShapes(state, finder, out payload, out failureMessage);
            case CopySelectionKind.Frame:
                payload = new CopySelectionPayload
                {
                    Kind = CopySelectionKind.Frame,
                    Frames = SortFrames(CollectFrames(state), acls),
                };
                return payload.Frames.Count > 0;
            default:
                payload = new CopySelectionPayload
                {
                    Kind = CopySelectionKind.Chain,
                    Chains = SortChains(CollectChains(state), acls),
                };
                if (payload.Chains.Count == 0)
                {
                    failureMessage = "Nothing selected to copy.";
                    return false;
                }
                return true;
        }
    }

    /// <summary>
    /// Same priority as <c>SelectedData</c> in the app layer: shape → frame → chain.
    /// </summary>
    private static bool TryGetFocusedKind(ISelectedState state, out CopySelectionKind kind)
    {
        if (state.SelectedRectangle is not null || state.SelectedCircle is not null)
        {
            kind = CopySelectionKind.Shape;
            return true;
        }
        if (state.SelectedFrame is not null)
        {
            kind = CopySelectionKind.Frame;
            return true;
        }
        if (state.SelectedChain is not null)
        {
            kind = CopySelectionKind.Chain;
            return true;
        }

        if (state.SelectedNodes.Count > 0)
        {
            bool hasChain  = state.SelectedNodes.Any(n => n is AnimationChainSave);
            bool hasFrame  = state.SelectedNodes.Any(n => n is AnimationFrameSave);
            bool hasShape  = state.SelectedNodes.Any(n => n is AARectSave or CircleSave);
            int kinds = (hasChain ? 1 : 0) + (hasFrame ? 1 : 0) + (hasShape ? 1 : 0);
            if (kinds == 1)
            {
                kind = hasChain ? CopySelectionKind.Chain
                     : hasFrame ? CopySelectionKind.Frame
                     : CopySelectionKind.Shape;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static bool HasForeignKindsInSelectedNodes(ISelectedState state, CopySelectionKind kind)
    {
        foreach (var node in state.SelectedNodes)
        {
            bool matches = kind switch
            {
                CopySelectionKind.Chain => node is AnimationChainSave,
                CopySelectionKind.Frame => node is AnimationFrameSave,
                CopySelectionKind.Shape => node is AARectSave or CircleSave,
                _ => false,
            };
            if (!matches) return true;
        }
        return false;
    }

    private static bool TryGetShapes(
        ISelectedState state,
        IObjectFinder finder,
        out CopySelectionPayload payload,
        out string? failureMessage)
    {
        payload = null!;
        failureMessage = null;
        var shapes = CollectShapes(state);
        if (shapes.Count == 0)
        {
            failureMessage = "Nothing selected to copy.";
            return false;
        }

        AnimationFrameSave? parent = null;
        foreach (var shape in shapes)
        {
            var frame = shape switch
            {
                AARectSave r => finder.GetAnimationFrameContaining(r),
                CircleSave c => finder.GetAnimationFrameContaining(c),
                _ => null,
            };
            if (frame is null)
            {
                failureMessage = MixedSelectionMessage;
                return false;
            }
            parent ??= frame;
            if (!ReferenceEquals(parent, frame))
            {
                failureMessage = MixedSelectionMessage;
                return false;
            }
        }

        payload = new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = shapes };
        return true;
    }

    private static List<AnimationChainSave> CollectChains(ISelectedState state)
    {
        var chains = state.SelectedChains;
        if (chains.Count == 0 && state.SelectedChain is { } single)
            return new List<AnimationChainSave> { single };
        return chains;
    }

    private static List<AnimationFrameSave> CollectFrames(ISelectedState state)
        => state.SelectedFrames;

    private static List<object> CollectShapes(ISelectedState state)
    {
        var shapes = new List<object>();
        shapes.AddRange(state.SelectedRectangles.Cast<object>());
        shapes.AddRange(state.SelectedCircles.Cast<object>());
        if (shapes.Count == 0)
        {
            if (state.SelectedRectangle is { } rect) shapes.Add(rect);
            else if (state.SelectedCircle is { } circle) shapes.Add(circle);
        }
        return shapes;
    }

    private static IReadOnlyList<AnimationChainSave> SortChains(
        IReadOnlyList<AnimationChainSave> chains,
        AnimationChainListSave? acls)
    {
        if (acls is null) return chains;
        return chains
            .OrderBy(c => acls.AnimationChains.IndexOf(c))
            .ToList();
    }

    private static IReadOnlyList<AnimationFrameSave> SortFrames(
        IReadOnlyList<AnimationFrameSave> frames,
        AnimationChainListSave? acls)
    {
        if (acls is null) return frames;
        return frames
            .OrderBy(f => ChainIndex(acls, f))
            .ThenBy(f => FrameIndex(acls, f))
            .ToList();
    }

    private static int ChainIndex(AnimationChainListSave acls, AnimationFrameSave frame)
    {
        for (int i = 0; i < acls.AnimationChains.Count; i++)
        {
            if (acls.AnimationChains[i].Frames.Contains(frame))
                return i;
        }
        return int.MaxValue;
    }

    private static int FrameIndex(AnimationChainListSave acls, AnimationFrameSave frame)
    {
        foreach (var chain in acls.AnimationChains)
        {
            int idx = chain.Frames.IndexOf(frame);
            if (idx >= 0) return idx;
        }
        return int.MaxValue;
    }
}
