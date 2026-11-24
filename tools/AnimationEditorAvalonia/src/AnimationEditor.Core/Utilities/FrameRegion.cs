using System;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Utilities;

/// <summary>A pixel rectangle on a texture. Frames of the open document store their region in pixels (#1952).</summary>
public readonly record struct FrameRegion(float Left, float Top, float Right, float Bottom)
{
    /// <summary>
    /// The region of a frame nobody has given one: <see cref="AnimationFrameSave"/>'s own 0,0,1,1. It is also a
    /// real one-pixel region, so it only means "not set" together with an unknown texture size; see <see cref="IsUnset"/>.
    /// </summary>
    public static readonly FrameRegion Unset = new(0f, 0f, 1f, 1f);

    /// <summary>Edges that differ by no more than this many pixels differ by float noise, not by an edit.</summary>
    public const float EditTolerance = 0.01f;

    public static FrameRegion Whole((int Width, int Height) texture) => new(0f, 0f, texture.Width, texture.Height);

    public static FrameRegion Of(AnimationFrameSave frame) =>
        new(frame.LeftCoordinate, frame.TopCoordinate, frame.RightCoordinate, frame.BottomCoordinate);

    public void ApplyTo(AnimationFrameSave frame)
    {
        frame.LeftCoordinate = Left;
        frame.TopCoordinate = Top;
        frame.RightCoordinate = Right;
        frame.BottomCoordinate = Bottom;
    }

    /// <summary>
    /// True for a frame waiting for a texture to show whole: it holds <see cref="Unset"/> and has no texture, or
    /// one whose size <paramref name="textureSize"/> can't read.
    /// </summary>
    public static bool IsUnset(AnimationFrameSave frame, Func<string, (int Width, int Height)?> textureSize) =>
        Of(frame) == Unset && (string.IsNullOrEmpty(frame.TextureName) || textureSize(frame.TextureName) is null);
}
