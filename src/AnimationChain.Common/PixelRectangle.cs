using System;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A renderer-agnostic pixel-coordinate rectangle — the source-region type used by
/// <see cref="AnimationFrameBase"/> so this project has no MonoGame/KNI dependency. Renderers
/// convert this to their own rectangle type (e.g. <c>Microsoft.Xna.Framework.Rectangle</c>) when
/// drawing a frame.
/// </summary>
public struct PixelRectangle
{
    /// <summary>Left edge, in pixels.</summary>
    public int X;

    /// <summary>Top edge, in pixels.</summary>
    public int Y;

    /// <summary>Width, in pixels.</summary>
    public int Width;

    /// <summary>Height, in pixels.</summary>
    public int Height;

    /// <summary>Creates a rectangle from its left/top/width/height, all in pixels.</summary>
    public PixelRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// The region of its texture that <paramref name="frame"/> covers, shared by the game and the AnimationEditor so both
    /// show the same pixels. The edges are rounded, not the size, so adjacent regions meet. The result is empty or
    /// negative in size when the frame's edges are.
    /// </summary>
    /// <param name="frame">The frame whose coordinates to read.</param>
    /// <param name="coordinateType">What the coordinates hold (<see cref="AnimationChainListSave.CoordinateType"/>).</param>
    /// <param name="textureWidth">Texture width. Only used for <see cref="TextureCoordinateType.UV"/>.</param>
    /// <param name="textureHeight">Texture height. Only used for <see cref="TextureCoordinateType.UV"/>.</param>
    public static PixelRectangle FromFrame(AnimationFrameSave frame, TextureCoordinateType coordinateType, int textureWidth, int textureHeight)
    {
        float scaleX = 1f, scaleY = 1f;
        if (coordinateType != TextureCoordinateType.Pixel)
        {
            scaleX = textureWidth;
            scaleY = textureHeight;
        }

        int left = Round(frame.LeftCoordinate * scaleX);
        int top = Round(frame.TopCoordinate * scaleY);
        int right = Round(frame.RightCoordinate * scaleX);
        int bottom = Round(frame.BottomCoordinate * scaleY);
        return new PixelRectangle(left, top, right - left, bottom - top);
    }

    private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
