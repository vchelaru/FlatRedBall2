using System;
using System.Collections.Generic;
using System.IO;
using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Services;

/// <summary>
/// Decodes texture PNGs once, caches them, and crops frame-region thumbnails.
/// Shared by the preview render path, the timeline strip, and the animation-tree
/// first-frame chain icons so a sprite sheet is only decoded a single time.
/// </summary>
public sealed class ThumbnailService
{
    private readonly IProjectManager _projectManager;

    /// <summary>
    /// Decoded-bitmap cache keyed by absolute file path (case-insensitive). One decode per
    /// file. Exposed so the preview render path can hand it to its off-thread draw op.
    /// </summary>
    public Dictionary<string, SKBitmap?> BitmapCache { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ThumbnailService(IProjectManager projectManager) =>
        _projectManager = projectManager;

    /// <summary>
    /// Returns the cached decode of <paramref name="path"/>, decoding on first access.
    /// Returns <c>null</c> for a null/empty path or a file that fails to decode (the
    /// failure is cached too, so a missing texture is not retried every frame).
    /// </summary>
    public SKBitmap? GetBitmap(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (BitmapCache.TryGetValue(path, out var cached)) return cached;
        try
        {
            var bm = SKBitmap.Decode(path);
            BitmapCache[path] = bm;
            return bm;
        }
        catch
        {
            BitmapCache[path] = null;
            return null;
        }
    }

    /// <summary>
    /// Resolves a frame's <see cref="AnimationFrameSave.TextureName"/> to an absolute file
    /// path. Relative names require a saved .achx to derive the base folder. Returns
    /// <c>null</c> when the texture cannot be located on disk.
    /// </summary>
    public string? ResolveTexturePath(AnimationFrameSave? frame)
    {
        if (frame is null || string.IsNullOrEmpty(frame.TextureName)) return null;

        // Absolute path (e.g. drag-dropped textures before an ACHX file is saved).
        if (Path.IsPathRooted(frame.TextureName))
            return File.Exists(frame.TextureName) ? frame.TextureName : null;

        // Relative path: requires a saved ACHX to derive the base folder.
        if (string.IsNullOrEmpty(_projectManager.FileName))
            return null;
        string achxFolder = Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty;
        string full = new FilePath(Path.Combine(achxFolder, frame.TextureName)).FullPath;
        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// Returns an Avalonia Bitmap of the frame's texture region, scaled to fit within
    /// <paramref name="maxWidth"/> × <paramref name="maxHeight"/> (preserving aspect ratio).
    /// Returns <c>null</c> if the texture cannot be resolved, is not loaded, or the frame
    /// has no valid UV region. Caller owns the returned bitmap.
    /// </summary>
    public Avalonia.Media.Imaging.Bitmap? GetFrameThumbnail(AnimationFrameSave frame, int maxWidth, int maxHeight)
    {
        var bm = GetBitmap(ResolveTexturePath(frame));
        if (bm is null) return null;

        using var thumb = RenderFrameThumbnail(bm, frame, maxWidth, maxHeight);
        if (thumb is null) return null;

        using var ms = new MemoryStream();
        thumb.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;
        return new Avalonia.Media.Imaging.Bitmap(ms);
    }

    /// <summary>
    /// Pure SkiaSharp core of <see cref="GetFrameThumbnail"/>: crops <paramref name="frame"/>'s
    /// UV region out of <paramref name="source"/> and scales it to fit within
    /// <paramref name="maxWidth"/> × <paramref name="maxHeight"/> (aspect-preserving).
    /// Returns <c>null</c> for a degenerate UV region. Exposed (internal) so tests can verify
    /// crop/scale/sampling behaviour without going through file decode or Avalonia bitmap wrapping.
    /// </summary>
    internal static SKBitmap? RenderFrameThumbnail(
        SKBitmap source, AnimationFrameSave frame, int maxWidth, int maxHeight)
    {
        float uvW = frame.RightCoordinate  - frame.LeftCoordinate;
        float uvH = frame.BottomCoordinate - frame.TopCoordinate;
        if (uvW <= 0f || uvH <= 0f) return null;

        int tw = source.Width, th = source.Height;
        int sx = Math.Clamp((int)(frame.LeftCoordinate * tw), 0, tw - 1);
        int sy = Math.Clamp((int)(frame.TopCoordinate  * th), 0, th - 1);
        int sw = Math.Clamp((int)(uvW * tw), 1, tw - sx);
        int sh = Math.Clamp((int)(uvH * th), 1, th - sy);

        float scale = Math.Min((float)maxWidth / sw, (float)maxHeight / sh);
        int finalW = Math.Max(1, (int)(sw * scale));
        int finalH = Math.Max(1, (int)(sh * scale));

        // Crop the frame's region into its own image first, then scale. Scaling a sub-rect of
        // the full sheet directly lets the sampler reach past the rect edges and pull in
        // neighbouring frames (visible bleed / thin seam lines). A standalone subset has no
        // neighbours to bleed from.
        using var img    = SKImage.FromBitmap(source);
        using var region = img.Subset(SKRectI.Create(sx, sy, sw, sh));
        if (region is null) return null;   // sx/sy/sw/sh are clamped in-bounds, so defensive only

        var thumb = new SKBitmap(finalW, finalH);
        using var canvas = new SKCanvas(thumb);
        canvas.Clear(SKColors.Transparent);
        using var paint  = new SKPaint { Color = SKColors.White };

        bool anyFlip = frame.FlipHorizontal || frame.FlipVertical;
        if (anyFlip)
        {
            canvas.Save();
            float flipScaleX = frame.FlipHorizontal ? -1f : 1f;
            float flipScaleY = frame.FlipVertical   ? -1f : 1f;
            canvas.Scale(flipScaleX, flipScaleY, finalW / 2f, finalH / 2f);
        }

        // Nearest-neighbour ("point") sampling: keeps sprite-sheet art crisp/pixellated
        // instead of the blurry smear linear filtering produces on game art.
        canvas.DrawImage(region,
            SKRect.Create(0, 0, finalW, finalH),
            new SKSamplingOptions(SKFilterMode.Nearest),
            paint);

        if (anyFlip) canvas.Restore();
        return thumb;
    }
}
