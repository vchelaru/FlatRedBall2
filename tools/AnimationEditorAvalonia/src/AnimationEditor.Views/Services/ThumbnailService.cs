using System;
using System.Collections.Generic;
using System.IO;
using AnimationEditor.Core;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Platform;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Services;

/// <summary>
/// Decodes texture PNGs once, caches them, and crops frame-region thumbnails.
/// Shared by the preview render path, the timeline strip, and the animation-tree
/// first-frame chain icons so a sprite sheet is only decoded a single time.
/// <para>
/// Finished per-frame thumbnails are cached too (see <see cref="GetFrameThumbnail"/>),
/// so switching back to a previously-viewed tab re-uses the existing icons instead of
/// re-cropping every chain. The service owns those bitmaps; callers must not dispose
/// the returned <see cref="Avalonia.Media.Imaging.Bitmap"/>.
/// </para>
/// </summary>
public sealed class ThumbnailService : IDisposable
{
    private readonly IProjectManager _projectManager;

    /// <summary>
    /// Decoded-bitmap cache keyed by absolute file path (case-insensitive). One decode per
    /// file. Exposed so the preview render path can hand it to its off-thread draw op.
    /// </summary>
    public Dictionary<string, SKBitmap?> BitmapCache { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Immutable-image cache keyed by absolute file path (case-insensitive), parallel to
    /// <see cref="BitmapCache"/>. The preview render path draws these directly on the Avalonia
    /// render thread (via <see cref="GetImage"/>) instead of rebuilding an <see cref="SKImage"/>
    /// from the source bitmap every frame. For a 4096×4096 sheet that per-frame
    /// <see cref="SKImage.FromBitmap"/> was a 67 MB copy + GPU re-upload 60×/sec — the preview
    /// framerate bottleneck (issue #514). <see cref="SKImage"/> is immutable, so a single cached
    /// instance is safe to draw off-thread across frames.
    /// </summary>
    public Dictionary<string, SKImage?> ImageCache { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cache signature for a finished thumbnail. Two equal keys produce a pixel-identical
    /// bitmap, so a tab switch (or strip rebuild) re-uses the cached icon. The resolved
    /// <em>absolute</em> texture path is part of the key — two tabs in different folders can
    /// share the same relative <c>TextureName</c> yet must not collide on one cached crop.
    /// <para>
    /// The effective color/alpha/operation are part of the key too: two frames with the same crop
    /// but a different tint must not collide on one cached bitmap, and a tinted thumbnail is rendered
    /// once and reused until its (crop or color) inputs change.
    /// </para>
    /// </summary>
    private readonly record struct ThumbnailKey(
        string Path, float Left, float Right, float Top, float Bottom,
        bool FlipHorizontal, bool FlipVertical, int MaxWidth, int MaxHeight,
        int? Red, int? Green, int? Blue, int? Alpha, ColorOperation? Operation);

    /// <summary>
    /// Finished-thumbnail cache. The service owns these bitmaps and is their sole disposer
    /// (on eviction the reference is simply dropped — GC reclaims it once no UI control holds
    /// it, which avoids disposing a bitmap a live <c>Image</c> is still bound to).
    /// </summary>
    private readonly Dictionary<ThumbnailKey, Avalonia.Media.Imaging.Bitmap> _thumbnailCache = new();

    /// <summary>Insertion order for FIFO eviction once <see cref="MaxCachedThumbnails"/> is exceeded.
    /// A key may linger here after <see cref="InvalidatePath"/> removed it from the cache; the
    /// eviction loop tolerates that (the dictionary <c>Remove</c> simply reports it absent).</summary>
    private readonly Queue<ThumbnailKey> _thumbnailOrder = new();

    /// <summary>Cache signature for a whole-PNG (Files-panel) thumbnail: the source path plus the
    /// target box. Bounded by the number of distinct PNGs at a given icon size, so — unlike the
    /// frame-thumbnail cache — it needs no churn cap; <see cref="InvalidatePath"/> drops entries
    /// when a PNG reloads.</summary>
    private readonly record struct FullImageKey(string Path, int MaxWidth, int MaxHeight);

    /// <summary>Finished full-image thumbnails, owned by the service (do not dispose the returned
    /// bitmap). Lets the Files-panel scope toggle rebuild the tree without re-downscaling every PNG.</summary>
    private readonly Dictionary<FullImageKey, Avalonia.Media.Imaging.Bitmap> _fullImageCache = new();

    /// <summary>Caps finished-thumbnail memory so editing churn (e.g. dragging a UV slider, which
    /// mints a new key per tick) can't grow the cache without bound. Each entry is tiny
    /// (≤ 56×56×4 bytes), so this is a few MB worst case.</summary>
    private const int MaxCachedThumbnails = 512;

    public ThumbnailService(IProjectManager projectManager) =>
        _projectManager = projectManager;

    /// <summary>Evict a specific path from the bitmap cache so it is re-decoded on next access.
    /// Normalises backslashes to forward slashes before lookup so FSW-reported paths (backslash
    /// on Windows) evict entries that were stored via <c>FilePath.FullPath</c> (forward slash).
    /// Also drops every finished thumbnail cropped from this texture, so a hot-reloaded sheet
    /// re-renders its chain/timeline icons instead of showing the stale crop.
    /// </summary>
    public void InvalidatePath(string absolutePath)
    {
        var key = absolutePath.Replace('\\', '/');
        BitmapCache.Remove(key);
        // Drop (don't Dispose) the cached image: a render on the compositor thread may still be
        // drawing it. Removing the reference re-decodes on next access; GC reclaims the old image
        // once no in-flight draw holds it. Mirrors the non-disposing BitmapCache.Remove above.
        ImageCache.Remove(key);

        List<ThumbnailKey>? stale = null;
        foreach (var k in _thumbnailCache.Keys)
            if (string.Equals(k.Path, key, StringComparison.OrdinalIgnoreCase))
                (stale ??= new()).Add(k);
        if (stale is not null)
            foreach (var k in stale)
                _thumbnailCache.Remove(k);

        // Same drop-don't-dispose treatment for the whole-PNG Files-panel thumbnails, so a
        // hot-reloaded sheet re-renders its Files-panel icon instead of showing the stale crop.
        List<FullImageKey>? staleFull = null;
        foreach (var k in _fullImageCache.Keys)
            if (string.Equals(k.Path, key, StringComparison.OrdinalIgnoreCase))
                (staleFull ??= new()).Add(k);
        if (staleFull is not null)
            foreach (var k in staleFull)
                _fullImageCache.Remove(k);
    }

    /// <summary>
    /// Returns the cached decode of <paramref name="path"/>, decoding on first access.
    /// Returns <c>null</c> for a null/empty path or a file that fails to decode (the
    /// failure is cached too, so a missing texture is not retried every frame).
    /// <para>
    /// The cache key is always stored with forward slashes so it matches the normalized
    /// paths returned by <see cref="ResolveTexturePath"/> and removed by
    /// <see cref="InvalidatePath"/>. This prevents stale bitmaps when the caller supplies
    /// a Windows-style backslash path (e.g. from a drag-drop on an unsaved project).
    /// </para>
    /// </summary>
    public SKBitmap? GetBitmap(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var key = path.Replace('\\', '/');
        if (BitmapCache.TryGetValue(key, out var cached)) return cached;
        try
        {
            var bm = SKBitmap.Decode(path);
            BitmapCache[key] = bm;
            return bm;
        }
        catch
        {
            BitmapCache[key] = null;
            return null;
        }
    }

    /// <summary>
    /// Returns a cached immutable <see cref="SKImage"/> for <paramref name="path"/>, built once
    /// from the decoded bitmap. Returns <c>null</c> for a null/empty path or a texture that fails
    /// to decode (the failure is cached, so a missing texture is not retried every frame). Reusing
    /// one image across frames avoids the per-frame <see cref="SKImage.FromBitmap"/> full-atlas
    /// copy that made large-sheet previews crawl (issue #514).
    /// </summary>
    public SKImage? GetImage(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var key = path.Replace('\\', '/');
        if (ImageCache.TryGetValue(key, out var cached)) return cached;
        var bm  = GetBitmap(path);
        var img = bm is null ? null : SKImage.FromBitmap(bm);
        ImageCache[key] = img;
        return img;
    }

    /// <summary>
    /// Resolves a frame's <see cref="AnimationFrameSave.TextureName"/> to an absolute file
    /// path. Relative names require a saved .achx to derive the base folder. Returns
    /// <c>null</c> when the texture cannot be located on disk -- unless it was already
    /// registered via <see cref="SeedTexture"/>, in which case no disk check happens at all.
    /// </summary>
    public string? ResolveTexturePath(AnimationFrameSave? frame)
    {
        if (frame is null || string.IsNullOrEmpty(frame.TextureName)) return null;

        // Bundled/in-memory content (e.g. the browser build, which has no real filesystem):
        // if the bytes were already registered under this name, trust it -- no disk check.
        var normalizedName = frame.TextureName.Replace('\\', '/');
        if (BitmapCache.ContainsKey(normalizedName)) return normalizedName;

        // Absolute path (e.g. drag-dropped textures before an ACHX file is saved).
        // Normalize to forward slashes so the returned path is a consistent cache key
        // regardless of whether the caller used backslashes (native Windows drag-drop path).
        if (Path.IsPathRooted(frame.TextureName))
        {
            var normalized = frame.TextureName.Replace('\\', '/');
            return File.Exists(frame.TextureName) ? normalized : null;
        }

        // Relative path: requires a saved ACHX to derive the base folder.
        if (string.IsNullOrEmpty(_projectManager.FileName))
            return null;
        string achxFolder = Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty;
        string full = new FilePath(Path.Combine(achxFolder, frame.TextureName)).FullPath;
        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// Registers an already-decoded bitmap under <paramref name="textureName"/> without
    /// touching disk, so <see cref="ResolveTexturePath"/> and <see cref="GetBitmap"/> can serve
    /// it purely from memory. For content that only ever exists as in-memory bytes (e.g. the
    /// browser build, which fetches textures over HTTP instead of reading a real file).
    /// </summary>
    public void SeedTexture(string textureName, SKBitmap bitmap) =>
        BitmapCache[textureName.Replace('\\', '/')] = bitmap;

    /// <summary>
    /// Returns a downscaled Avalonia bitmap of the full PNG at <paramref name="path"/>, for the
    /// Files panel. The result is cached (keyed by path + target size) and <em>owned by this
    /// service</em> — do not dispose it. Caching matters because the Files-panel scope toggle
    /// rebuilds the whole tree; without it, every rebuild re-ran <see cref="SKImage.FromBitmap"/>
    /// on each source atlas (a full-sheet copy — the #514 hot path) plus a fresh bitmap alloc per
    /// PNG. <see cref="InvalidatePath"/> drops the entry so a hot-reloaded PNG re-renders.
    /// </summary>
    public Avalonia.Media.Imaging.Bitmap? GetFullImageThumbnail(string? path, int maxWidth, int maxHeight)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var key = new FullImageKey(path.Replace('\\', '/'), maxWidth, maxHeight);
        if (_fullImageCache.TryGetValue(key, out var cached)) return cached;

        var bm = GetBitmap(path);
        if (bm is null) return null;

        float scale = Math.Min((float)maxWidth / bm.Width, (float)maxHeight / bm.Height);
        scale = Math.Min(scale, 1f);
        int finalW = Math.Max(1, (int)(bm.Width * scale));
        int finalH = Math.Max(1, (int)(bm.Height * scale));

        using var thumb = new SKBitmap(finalW, finalH);
        using var canvas = new SKCanvas(thumb);
        canvas.Clear(SKColors.Transparent);
        using var img = SKImage.FromBitmap(bm);
        canvas.DrawImage(img, SKRect.Create(0, 0, finalW, finalH),
            new SKSamplingOptions(SKFilterMode.Linear));

        var bitmap = ToAvaloniaBitmap(thumb);
        _fullImageCache[key] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Returns an Avalonia Bitmap of the frame's texture region, scaled to fit within
    /// <paramref name="maxWidth"/> × <paramref name="maxHeight"/> (preserving aspect ratio).
    /// Returns <c>null</c> if the texture cannot be resolved, is not loaded, or the frame
    /// has no valid UV region.
    /// <para>
    /// The result is cached (keyed by resolved texture path + UV region + flips + effective
    /// color/alpha + target size) and <em>owned by this service</em> — do not dispose it. Re-calling
    /// with the same frame state returns the same cached instance, so a tab switch re-uses every
    /// unchanged icon and playback never re-renders a cell. The Skia crop is wrapped directly as a
    /// <see cref="Avalonia.Media.Imaging.WriteableBitmap"/>; there is no PNG encode/decode round-trip.
    /// </para>
    /// <para>
    /// <paramref name="color"/> is the frame's <em>effective</em> (sticky) color — resolve it once per
    /// data change via <see cref="EffectiveFrameColor.ResolveAll"/> and pass it in; do not resolve
    /// inside the playback render loop. <c>default</c> means no tint / full opacity.
    /// </para>
    /// </summary>
    public Avalonia.Media.Imaging.Bitmap? GetFrameThumbnail(
        AnimationFrameSave frame, ResolvedFrameColor color, int maxWidth, int maxHeight)
    {
        var path = ResolveTexturePath(frame);
        var bm   = GetBitmap(path);
        if (bm is null) return null;

        var key = new ThumbnailKey(
            path!.Replace('\\', '/'),
            frame.LeftCoordinate, frame.RightCoordinate, frame.TopCoordinate, frame.BottomCoordinate,
            frame.FlipHorizontal, frame.FlipVertical, maxWidth, maxHeight,
            color.Red, color.Green, color.Blue, color.Alpha, color.Operation);
        if (_thumbnailCache.TryGetValue(key, out var cachedBitmap))
            return cachedBitmap;

        using var thumb = RenderFrameThumbnail(bm, frame, color, maxWidth, maxHeight);
        if (thumb is null) return null;

        var bitmap = ToAvaloniaBitmap(thumb);
        _thumbnailCache[key] = bitmap;
        _thumbnailOrder.Enqueue(key);
        EvictExcessThumbnails();
        return bitmap;
    }

    /// <summary>
    /// Wraps a Skia bitmap as an Avalonia <see cref="Avalonia.Media.Imaging.WriteableBitmap"/> by
    /// copying its pixels straight into the framebuffer — no PNG encode/decode. Skia converts to
    /// the destination BGRA/premultiplied layout during <see cref="SKBitmap.ReadPixels(SKImageInfo,
    /// IntPtr, int, int, int)"/>, so the source colour/alpha type does not matter.
    /// </summary>
    private static Avalonia.Media.Imaging.Bitmap ToAvaloniaBitmap(SKBitmap thumb)
    {
        var size   = new PixelSize(thumb.Width, thumb.Height);
        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
            size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bitmap.Lock())
        using (var pixmap = thumb.PeekPixels())
        {
            var dstInfo = new SKImageInfo(thumb.Width, thumb.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            pixmap.ReadPixels(dstInfo, fb.Address, fb.RowBytes, 0, 0);
        }
        return bitmap;
    }

    /// <summary>Drops the oldest finished thumbnails once the cache exceeds its cap. The reference
    /// is released (not disposed) so a bitmap still bound to a live <c>Image</c> stays valid until
    /// GC reclaims it after the control lets go.</summary>
    private void EvictExcessThumbnails()
    {
        while (_thumbnailCache.Count > MaxCachedThumbnails && _thumbnailOrder.Count > 0)
            _thumbnailCache.Remove(_thumbnailOrder.Dequeue());
    }

    /// <summary>Disposes every cached source sheet and finished thumbnail. Call on window close;
    /// any bitmaps still bound to UI are released as the window tears down.</summary>
    public void Dispose()
    {
        foreach (var bm in BitmapCache.Values)
            bm?.Dispose();
        BitmapCache.Clear();
        foreach (var img in ImageCache.Values)
            img?.Dispose();
        ImageCache.Clear();
        foreach (var bitmap in _thumbnailCache.Values)
            bitmap.Dispose();
        _thumbnailCache.Clear();
        _thumbnailOrder.Clear();
        foreach (var bitmap in _fullImageCache.Values)
            bitmap.Dispose();
        _fullImageCache.Clear();
    }

    /// <summary>
    /// Pure SkiaSharp core of <see cref="GetFrameThumbnail"/>: crops <paramref name="frame"/>'s
    /// UV region out of <paramref name="source"/>, applies the effective (sticky) <paramref name="color"/>
    /// and alpha, and scales it to fit within <paramref name="maxWidth"/> × <paramref name="maxHeight"/>
    /// (aspect-preserving). Returns <c>null</c> for a degenerate UV region. Exposed (internal) so tests
    /// can verify crop/scale/sampling/tint behaviour without going through file decode or Avalonia
    /// bitmap wrapping.
    /// <para>
    /// The tint reuses <see cref="FrameColorFilter"/> + <see cref="FramePreviewOpacity"/> — the same
    /// reference interpretation the preview panel applies — so a timeline/tree thumbnail and the
    /// preview render a given frame identically. A <c>default</c> color leaves the crop untinted at
    /// full opacity.
    /// </para>
    /// </summary>
    internal static SKBitmap? RenderFrameThumbnail(
        SKBitmap source, AnimationFrameSave frame, ResolvedFrameColor color, int maxWidth, int maxHeight)
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

        // Same reference interpretation the preview panel uses (PreviewControl.DrawFrameCore): the
        // sticky effective alpha previews as opacity, and the effective color operation as a filter.
        // A default color yields alpha 255 + a null filter, i.e. the untinted crop.
        using var paint  = new SKPaint
        {
            Color = new SKColor(255, 255, 255, FramePreviewOpacity.Resolve(color.Alpha, 1f)),
        };
        using var colorFilter = FrameColorFilter.Create(color.Operation, color.Red, color.Green, color.Blue);
        if (colorFilter is not null)
            paint.ColorFilter = colorFilter;

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
