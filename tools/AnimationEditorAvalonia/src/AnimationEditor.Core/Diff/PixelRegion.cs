namespace AnimationEditor.Core.Diff;

/// <summary>
/// A rectangular region of changed pixels, in texture-space pixel coordinates with <b>inclusive</b>
/// bounds — the box tightly encloses every changed pixel in one merged cluster (see
/// <see cref="RegionMerger"/>). The overlay draws one square per region over the image.
/// </summary>
public readonly record struct PixelRegion(int MinX, int MinY, int MaxX, int MaxY, int ChangedPixelCount)
{
    /// <summary>Pixel width of the region (inclusive bounds, so ≥ 1 when the region is non-empty).</summary>
    public int Width => MaxX - MinX + 1;

    /// <summary>Pixel height of the region (inclusive bounds, so ≥ 1 when the region is non-empty).</summary>
    public int Height => MaxY - MinY + 1;
}
