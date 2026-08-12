using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Converts a single frame's raw-pixel coordinates (<see cref="TextureCoordinateType.Pixel"/>) to
/// normalized UV (0-1), given the decoded texture's pixel size. Used by the project-tree thumbnail
/// generator (issue #839), which only ever needs one frame's coordinates converted and already has
/// the texture decoded -- unlike <c>ProjectManager.ConvertCoordinates</c>, which round-trips an
/// entire <c>AnimationChainListSave</c> for save/load.
/// </summary>
public static class PixelFrameUvConverter
{
    public static AnimationFrameSave ToUv(AnimationFrameSave frame, int textureWidth, int textureHeight) => new()
    {
        TextureName = frame.TextureName,
        LeftCoordinate = frame.LeftCoordinate / textureWidth,
        RightCoordinate = frame.RightCoordinate / textureWidth,
        TopCoordinate = frame.TopCoordinate / textureHeight,
        BottomCoordinate = frame.BottomCoordinate / textureHeight,
        FlipHorizontal = frame.FlipHorizontal,
        FlipVertical = frame.FlipVertical,
    };
}
