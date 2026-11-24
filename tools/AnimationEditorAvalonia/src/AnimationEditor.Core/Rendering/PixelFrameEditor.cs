using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Edits a frame's pixel region one component at a time, the way the property inspector's X/Y/W/H fields do.
/// X and Y move the region and keep its size; width and height keep the left and top edges. The region may
/// extend beyond the texture.
/// </summary>
public static class PixelFrameEditor
{
    /// <summary>Moves the frame to <paramref name="newXPixels"/>, keeping its width (at least 1 pixel).</summary>
    public static void SetX(AnimationFrameSave frame, int newXPixels)
    {
        ArgumentNullException.ThrowIfNull(frame);

        int frameWidth = Math.Max(1, RoundToPixel(frame.RightCoordinate) - RoundToPixel(frame.LeftCoordinate));
        frame.LeftCoordinate  = newXPixels;
        frame.RightCoordinate = newXPixels + frameWidth;
    }

    /// <summary>Moves the frame to <paramref name="newYPixels"/>, keeping its height (at least 1 pixel).</summary>
    public static void SetY(AnimationFrameSave frame, int newYPixels)
    {
        ArgumentNullException.ThrowIfNull(frame);

        int frameHeight = Math.Max(1, RoundToPixel(frame.BottomCoordinate) - RoundToPixel(frame.TopCoordinate));
        frame.TopCoordinate    = newYPixels;
        frame.BottomCoordinate = newYPixels + frameHeight;
    }

    /// <summary>Sets the frame's width, clamped to at least 1 pixel, keeping its left edge.</summary>
    public static void SetWidth(AnimationFrameSave frame, int newWidthPixels)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.RightCoordinate = frame.LeftCoordinate + Math.Max(1, newWidthPixels);
    }

    /// <summary>Sets the frame's height, clamped to at least 1 pixel, keeping its top edge.</summary>
    public static void SetHeight(AnimationFrameSave frame, int newHeightPixels)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.BottomCoordinate = frame.TopCoordinate + Math.Max(1, newHeightPixels);
    }

    private static int RoundToPixel(float coord) => (int)Math.Round(coord);
}
