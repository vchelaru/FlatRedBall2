using System;

namespace AnimationEditor.Core.Diff;

/// <summary>
/// Computes which pixels changed between two revisions of an image. A pixel counts as changed when the
/// largest per-channel difference — max(|ΔR|, |ΔG|, |ΔB|, |ΔA|) — exceeds a tolerance, so small
/// anti-aliasing / lossy re-export noise is absorbed. Alpha is compared like any other channel, so a
/// new sprite frame drawn on a previously-transparent area (alpha 0 → 255) registers as changed.
/// </summary>
public static class PixelDiff
{
    /// <summary>
    /// Builds a <see cref="ChangeMask"/> for <paramref name="after"/> relative to
    /// <paramref name="before"/>. Either image may be <c>null</c>: a null <paramref name="before"/>
    /// (the initial add — no prior revision) treats every prior pixel as fully transparent, so all
    /// visible content reads as new; a null <paramref name="after"/> does the reverse. When the two
    /// images differ in size, the mask spans the union bounds and any pixel outside an image is
    /// treated as fully transparent (0,0,0,0) — so a grown sheet's new rows/columns show as changed.
    /// </summary>
    /// <param name="tolerance">
    /// Max per-channel delta (0–255) still considered "unchanged". 0 means exact equality; larger
    /// values ignore progressively bigger color/alpha differences.
    /// </param>
    public static ChangeMask Compute(ImageData? before, ImageData? after, int tolerance)
    {
        // Mask spans the union of both sizes so a grown/shrunk sheet's added/removed area is covered.
        int width = Math.Max(before?.Width ?? 0, after?.Width ?? 0);
        int height = Math.Max(before?.Height ?? 0, after?.Height ?? 0);
        var changed = new bool[width * height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var (br, bg, bb, ba) = Sample(before, x, y);
            var (ar, ag, ab, aa) = Sample(after, x, y);

            int delta = Math.Max(Math.Max(Math.Abs(ar - br), Math.Abs(ag - bg)),
                                 Math.Max(Math.Abs(ab - bb), Math.Abs(aa - ba)));
            if (delta > tolerance)
                changed[y * width + x] = true;
        }

        return new ChangeMask(width, height, changed);
    }

    // Pixels outside the image (null image, or a coordinate beyond its bounds) read as fully
    // transparent, so content that only exists in one image counts as a change.
    private static (int r, int g, int b, int a) Sample(ImageData? img, int x, int y)
    {
        if (img is null || x >= img.Width || y >= img.Height)
            return (0, 0, 0, 0);
        int i = (y * img.Width + x) * 4;
        var p = img.Rgba;
        return (p[i], p[i + 1], p[i + 2], p[i + 3]);
    }
}
