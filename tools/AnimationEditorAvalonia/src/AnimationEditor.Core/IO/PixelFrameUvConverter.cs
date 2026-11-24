using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Converts a single frame's normalized UV (0-1) coordinates to raw pixels
/// (<see cref="TextureCoordinateType.Pixel"/>), given the decoded texture's pixel size. For readers that show one
/// frame of a legacy UV file and already hold its texture, unlike <c>TextureCoordinateConverter</c>, which converts
/// an entire <c>AnimationChainListSave</c> from PNG headers.
/// </summary>
public static class PixelFrameUvConverter
{
    public static AnimationFrameSave ToPixel(AnimationFrameSave frame, int textureWidth, int textureHeight) => new()
    {
        TextureName = frame.TextureName,
        LeftCoordinate = frame.LeftCoordinate * textureWidth,
        RightCoordinate = frame.RightCoordinate * textureWidth,
        TopCoordinate = frame.TopCoordinate * textureHeight,
        BottomCoordinate = frame.BottomCoordinate * textureHeight,
        FlipHorizontal = frame.FlipHorizontal,
        FlipVertical = frame.FlipVertical,
    };
}
