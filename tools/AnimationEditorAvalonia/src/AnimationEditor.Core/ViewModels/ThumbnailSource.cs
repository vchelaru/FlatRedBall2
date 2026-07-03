using AnimationEditor.Core.Rendering;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Value signature of the visual a chain's first-frame thumbnail is rendered from.
/// Two equal <see cref="ThumbnailSource"/> values produce an identical thumbnail, so the
/// animation tree only regenerates a chain icon when this changes — which happens on a
/// frame reorder, a first-frame texture swap, a first-frame region edit, a first-frame
/// flip toggle, a first-frame color/alpha edit, or a first-frame delete.
/// <para>
/// The color fields carry the frame's <em>effective</em> (sticky) color — the value a runtime
/// would apply — not just what the frame explicitly sets. That's why editing an earlier frame's
/// color changes the signature of every later frame it tints, so the timeline strip rebuilds
/// those downstream cells too.
/// </para>
/// </summary>
public readonly record struct ThumbnailSource(
    string? TextureName,
    float Left,
    float Right,
    float Top,
    float Bottom,
    bool FlipHorizontal,
    bool FlipVertical,
    int? Red,
    int? Green,
    int? Blue,
    int? Alpha,
    ColorOperation? Operation)
{
    /// <summary>
    /// Returns the signature of a single frame's thumbnail-relevant visual state, given the
    /// frame's pre-resolved effective <paramref name="color"/> (from
    /// <see cref="EffectiveFrameColor.Resolve"/> / <see cref="EffectiveFrameColor.ResolveAll"/>).
    /// Requiring the resolved color as a parameter keeps callers that build a whole-strip signature
    /// on the O(n) <see cref="EffectiveFrameColor.ResolveAll"/> path instead of an O(n²) per-frame walk.
    /// </summary>
    public static ThumbnailSource FromFrame(AnimationFrameSave frame, ResolvedFrameColor color) =>
        new(frame.TextureName,
            frame.LeftCoordinate,
            frame.RightCoordinate,
            frame.TopCoordinate,
            frame.BottomCoordinate,
            frame.FlipHorizontal,
            frame.FlipVertical,
            color.Red,
            color.Green,
            color.Blue,
            color.Alpha,
            color.Operation);

    /// <summary>
    /// Returns the signature of <paramref name="chain"/>'s first frame, or <c>null</c>
    /// when the chain has no frames (the tree falls back to the generic chain icon). Frame 0's
    /// effective color is just its own set channels (nothing precedes it), so this is a cheap resolve.
    /// </summary>
    public static ThumbnailSource? FromChain(AnimationChainSave chain) =>
        chain.Frames.Count == 0
            ? null
            : FromFrame(chain.Frames[0], EffectiveFrameColor.Resolve(chain.Frames, 0));
}
