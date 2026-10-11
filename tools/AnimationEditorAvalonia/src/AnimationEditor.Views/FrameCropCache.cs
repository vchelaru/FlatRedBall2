using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Caches each frame's pixels as a standalone image, so a downscaled (bilinear) draw clamps at
/// the frame's own edge instead of reading the neighbouring frames on the sheet.
/// <para>
/// SkiaSharp's <c>DrawImage(image, source, dest, ...)</c> has no source-rect constraint parameter
/// (native Skia's strict constraint is not exposed), so a source rect on the sheet lets bilinear
/// filtering sample outside it and neighbouring frames bleed in at zoom &lt; 1. A crop is its own
/// image, so its edge pixels are replicated instead.
/// </para>
/// Reads the pixels with <c>SKImage.ReadPixels</c> rather than <c>SKImage.Subset</c>, which crashes
/// when called repeatedly on one image. Entries live and die with the sheet image they were cut from.
/// </summary>
internal static class FrameCropCache
{
    // Dragging a frame region produces a new rect per step; bound the per-sheet cache.
    private const int MaxCropsPerSheet = 256;

    private static readonly ConditionalWeakTable<SKImage, Dictionary<SKRectI, SKImage>> Crops = new();

    /// <summary>The frame's pixels as their own image, or null when <paramref name="rect"/> is empty
    /// or not fully inside <paramref name="sheet"/> (the caller then draws from the sheet directly).</summary>
    public static SKImage? Get(SKImage sheet, SKRectI rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0 ||
            rect.Left < 0 || rect.Top < 0 || rect.Right > sheet.Width || rect.Bottom > sheet.Height)
            return null;

        var perSheet = Crops.GetOrCreateValue(sheet);
        lock (perSheet)
        {
            if (perSheet.TryGetValue(rect, out var cached)) return cached;

            using var bitmap = new SKBitmap(new SKImageInfo(rect.Width, rect.Height));
            if (!sheet.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, rect.Left, rect.Top))
                return null;
            bitmap.SetImmutable();

            if (perSheet.Count >= MaxCropsPerSheet)
            {
                foreach (var old in perSheet.Values) old.Dispose();
                perSheet.Clear();
            }
            var crop = SKImage.FromBitmap(bitmap);
            perSheet[rect] = crop;
            return crop;
        }
    }
}
