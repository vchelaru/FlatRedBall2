namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes the pixel region for placing an <em>existing</em> frame onto a grid cell.
/// Grid placement snaps the frame's origin (top-left) to the grid but preserves its
/// current size — clicking a cell must never resize a frame (issue #538). Contrast
/// with new-frame creation, which is free to use a full grid cell as the frame size.
/// </summary>
public static class GridPlacementCalculator
{
    /// <summary>
    /// Snaps (<paramref name="worldX"/>, <paramref name="worldY"/>) down to the grid
    /// via <see cref="GridSnapper.Snap"/> and returns the region
    /// (minX, minY, maxX, maxY) whose size equals the passed
    /// <paramref name="frameWidth"/> × <paramref name="frameHeight"/> in pixels.
    /// </summary>
    public static (int minX, int minY, int maxX, int maxY) SnapOriginPreserveSize(
        float worldX, float worldY, int gridSize, int frameWidth, int frameHeight)
    {
        int gx = GridSnapper.Snap(worldX, gridSize);
        int gy = GridSnapper.Snap(worldY, gridSize);
        return (gx, gy, gx + frameWidth, gy + frameHeight);
    }
}
