using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Shared UV/pixel → integer rect resolution for export formats. Pixel coords are used
/// directly; UV coords are scaled by the resolved texture size. Edges are rounded with
/// <see cref="MidpointRounding.AwayFromZero"/> so adjacent frames tile without gaps.
/// </summary>
public static class FrameRectResolver
{
    /// <summary>
    /// Builds a pixel rect for <paramref name="frame"/>. Returns <c>false</c> when the
    /// coordinate type is UV and <paramref name="textureSizeResolver"/> cannot supply a
    /// positive size for the frame's texture.
    /// </summary>
    public static bool TryBuildRect(
        AnimationFrameSave frame,
        TextureCoordinateType coordinateType,
        Func<string, (int Width, int Height)?> textureSizeResolver,
        out FramePixelRect rect)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(textureSizeResolver);

        if (coordinateType == TextureCoordinateType.Pixel)
        {
            int left = Round(frame.LeftCoordinate);
            int top = Round(frame.TopCoordinate);
            rect = new FramePixelRect(
                left,
                top,
                Round(frame.RightCoordinate) - left,
                Round(frame.BottomCoordinate) - top);
            return true;
        }

        var size = textureSizeResolver(frame.TextureName);
        if (size is not { Width: > 0, Height: > 0 })
        {
            rect = default;
            return false;
        }

        int leftPx = Round(frame.LeftCoordinate * size.Value.Width);
        int rightPx = Round(frame.RightCoordinate * size.Value.Width);
        int topPx = Round(frame.TopCoordinate * size.Value.Height);
        int bottomPx = Round(frame.BottomCoordinate * size.Value.Height);
        rect = new FramePixelRect(leftPx, topPx, rightPx - leftPx, bottomPx - topPx);
        return true;
    }

    private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
