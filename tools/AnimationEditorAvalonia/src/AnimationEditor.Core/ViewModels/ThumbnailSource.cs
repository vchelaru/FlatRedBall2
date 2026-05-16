using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Value signature of the visual a chain's first-frame thumbnail is rendered from.
/// Two equal <see cref="ThumbnailSource"/> values produce an identical thumbnail, so the
/// animation tree only regenerates a chain icon when this changes — which happens on a
/// frame reorder, a first-frame texture swap, a first-frame region edit, a first-frame
/// flip toggle, or a first-frame delete.
/// </summary>
public readonly record struct ThumbnailSource(
    string? TextureName,
    float Left,
    float Right,
    float Top,
    float Bottom,
    bool FlipHorizontal,
    bool FlipVertical)
{
    /// <summary>
    /// Returns the signature of <paramref name="chain"/>'s first frame, or <c>null</c>
    /// when the chain has no frames (the tree falls back to the generic chain icon).
    /// </summary>
    public static ThumbnailSource? FromChain(AnimationChainSave chain)
    {
        if (chain.Frames.Count == 0)
            return null;

        var frame = chain.Frames[0];
        return new ThumbnailSource(
            frame.TextureName,
            frame.LeftCoordinate,
            frame.RightCoordinate,
            frame.TopCoordinate,
            frame.BottomCoordinate,
            frame.FlipHorizontal,
            frame.FlipVertical);
    }
}
