namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes the pixel region for the grid double-click placement gesture: snaps the
/// clicked point down to the grid and returns the full cell as the frame's new bounds.
///
/// <para>
/// <b>History (read before "simplifying" this away — #895):</b> this double-click gesture
/// has flip-flopped once already. #363 originally wanted double-click to resize the
/// selected frame to fill the clicked cell. #538 (a real bug about grid-enabled DISPLAY
/// snapping ballooning small frames on every refresh, and property-panel edits jumping to
/// the grid — nothing to do with double-click specifically) swept this double-click call
/// site into the same fix as a collateral side effect, making it preserve size instead.
/// That silently reverted #363 with no discussion in the #538 issue/PR of the behavior it
/// was overwriting. #895 restores #363's resize-to-cell behavior deliberately, because it
/// matches the actual workflow: dropping a PNG onto an animation creates one frame sized to
/// the entire sheet, and double-click-to-carve-out-a-cell is how users size it down — a
/// preserve-size double-click can't do that; only a full edge-handle drag can, which is
/// exactly the friction #895 was filed to remove.
/// </para>
/// <para>
/// If a genuine future need for a size-preserving reposition-only gesture shows up, add it
/// back as its own method (and its own explicit gesture, e.g. a modifier key) — don't
/// collapse it into this one and repeat the #538/#363 flip-flop a third time.
/// </para>
/// </summary>
public static class GridPlacementCalculator
{
    /// <summary>
    /// Snaps (<paramref name="worldX"/>, <paramref name="worldY"/>) down to the grid via
    /// <see cref="GridSnapper.Snap"/> and returns the full cell region (minX, minY,
    /// maxX, maxY) — the frame is resized to exactly one <paramref name="gridSize"/> cell.
    /// </summary>
    public static (int minX, int minY, int maxX, int maxY) SnapToCell(
        float worldX, float worldY, int gridSize)
    {
        int gx = GridSnapper.Snap(worldX, gridSize);
        int gy = GridSnapper.Snap(worldY, gridSize);
        return (gx, gy, gx + gridSize, gy + gridSize);
    }

    /// <summary>The <see cref="TileGrid"/> form: the cell containing the point (a click in the
    /// spacing gap after a cell counts as that cell), sized to the cell alone -- never the gap.</summary>
    public static (int minX, int minY, int maxX, int maxY) SnapToCell(
        float worldX, float worldY, TileGrid grid)
    {
        int gx = grid.CellLeft(grid.ColumnContaining(worldX));
        int gy = grid.CellTop(grid.RowContaining(worldY));
        return (gx, gy, gx + grid.CellWidth, gy + grid.CellHeight);
    }
}
