using AnimationEditor.App.Services;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>Where one frame's thumbnail sits in the atlas: the page and the pixel rectangle on it. Flips are already applied.</summary>
internal readonly record struct AtlasEntry(int Page, int X, int Y, int Width, int Height);

/// <summary>
/// Packs the filmstrips' frame thumbnails into a few large bitmaps, so drawing a strip is a run of copies from one texture
/// instead of a bitmap draw per cell. Thumbnails are cut at the display's pixel height, with the frame's flip and tint
/// baked in, so a cell is copied 1:1 with no resampling. Pages are copied into immutable images for the render thread
/// (<see cref="Snapshot"/>); the packing itself runs on the UI thread only.
/// </summary>
internal sealed class FilmstripAtlas
{
    private const int PageSize = 1024;
    private const int Gutter = 1;
    private const int MaxPages = 6;
    private const int MaxEntries = 20_000;
    private const int MaxSourceSide = 2048;
    private static readonly SKSamplingOptions SamplingNearest = new(SKFilterMode.Nearest);

    private readonly record struct Key(
        string Path, float Left, float Right, float Top, float Bottom, bool FlipH, bool FlipV, bool FlipD, int Height, ResolvedFrameColor Color);

    private sealed class Page(SKBitmap bitmap)
    {
        public readonly SKBitmap Bitmap = bitmap;
        public SKImage? Image;
        public bool Dirty = true;
        public int CursorX, ShelfTop, ShelfHeight;
    }

    private readonly List<Page> _pages = new();
    private readonly Dictionary<Key, (AtlasEntry? Entry, bool Fallback)> _entries = new();
    private SKImage[] _snapshot = Array.Empty<SKImage>();
    private bool _snapshotStale = true;
    private bool _full;

    /// <summary>Changes whenever entries are dropped, so a held <see cref="AtlasEntry"/> from an earlier generation is not trusted.</summary>
    public int Generation { get; private set; }

    public int PageCount => _pages.Count;

    /// <summary>Start of a frame: a full atlas starts over here, never in the middle of a frame that already holds entries.</summary>
    public void BeginFrame()
    {
        if (_full) Clear();
    }

    public void Clear()
    {
        // The images may still be in a render in flight, so they are dropped, not disposed.
        foreach (var page in _pages) page.Bitmap.Dispose();
        _pages.Clear();
        _entries.Clear();
        _snapshot = Array.Empty<SKImage>();
        _snapshotStale = true;
        _full = false;
        Generation++;
    }

    /// <summary>
    /// The thumbnail of <paramref name="plain"/> (a frame with no flips) cut from <paramref name="sheet"/> at
    /// <paramref name="deviceHeight"/> pixels tall, with the flips applied; null when it can't be drawn from the atlas
    /// (empty region, or wider than a page).
    /// </summary>
    public AtlasEntry? Get(SKBitmap sheet, string path, AnimationFrameSave plain, bool flipH, bool flipV, bool flipD, ResolvedFrameColor color, int deviceHeight, out bool fallback)
    {
        var key = new Key(path, plain.LeftCoordinate, plain.RightCoordinate, plain.TopCoordinate, plain.BottomCoordinate, flipH, flipV, flipD, deviceHeight, color);
        if (_entries.TryGetValue(key, out var known))
        {
            fallback = known.Fallback;
            return known.Entry;
        }
        var entry = Build(sheet, plain, flipH, flipV, flipD, color, deviceHeight, out fallback);
        _entries[key] = (entry, fallback);
        if (_entries.Count > MaxEntries) _full = true;   // edits mint new keys; start over at the next frame
        return entry;
    }

    private AtlasEntry? Build(SKBitmap sheet, AnimationFrameSave plain, bool flipH, bool flipV, bool flipD, ResolvedFrameColor color, int deviceHeight, out bool fallback)
    {
        // The unflipped crop. A diagonal flip turns it a quarter, so its width becomes the displayed height.
        using var thumb = ThumbnailService.RenderFrameThumbnail(sheet, plain, color,
            flipD ? deviceHeight : MaxSourceSide, flipD ? MaxSourceSide : deviceHeight);
        fallback = false;
        if (thumb is null) return null;

        bool flipped = flipH || flipV || flipD;
        int width = flipD ? thumb.Height : thumb.Width;
        int height = flipD ? thumb.Width : thumb.Height;
        fallback = true;   // from here on, a null result means the legacy bitmap path draws it
        if (width + 2 * Gutter > PageSize || height + 2 * Gutter > PageSize) return null;

        var (page, x, y) = Allocate(width, height);
        if (page is null) return null;
        fallback = false;

        using var source = SKImage.FromBitmap(thumb);
        using var canvas = new SKCanvas(page.Bitmap);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        if (flipped)
        {
            // The same flip matrix the preview applies, turned about the middle of the sprite.
            var (a, b, c, d) = FlipScaleCalculator.ComputeMatrix(flipH, flipV, flipD);
            float sourceX = thumb.Width / 2f, sourceY = thumb.Height / 2f;
            float destX = x + width / 2f, destY = y + height / 2f;
            canvas.SetMatrix(new SKMatrix(
                a, b, destX - (a * sourceX + b * sourceY),
                c, d, destY - (c * sourceX + d * sourceY),
                0, 0, 1));
            canvas.DrawImage(source, 0, 0, SamplingNearest, paint);
        }
        else
        {
            canvas.DrawImage(source, x, y, SamplingNearest, paint);
        }
        page.Dirty = true;
        _snapshotStale = true;
        return new AtlasEntry(_pages.IndexOf(page), x, y, width, height);
    }

    private (Page? Page, int X, int Y) Allocate(int width, int height)
    {
        int w = width + Gutter, h = height + Gutter;
        for (int i = _pages.Count - 1; i >= 0 && i >= _pages.Count - 2; i--)
        {
            var page = _pages[i];
            if (page.CursorX + w > PageSize)
            {
                page.ShelfTop += page.ShelfHeight;
                page.CursorX = 0;
                page.ShelfHeight = 0;
            }
            if (page.ShelfTop + h > PageSize) continue;
            int x = page.CursorX, y = page.ShelfTop;
            page.CursorX += w;
            page.ShelfHeight = Math.Max(page.ShelfHeight, h);
            return (page, x, y);
        }
        if (_pages.Count >= MaxPages)
        {
            _full = true;
            return (null, 0, 0);
        }
        var fresh = new SKBitmap(PageSize, PageSize);
        fresh.Erase(SKColors.Transparent);
        var created = new Page(fresh) { CursorX = w, ShelfHeight = h };
        _pages.Add(created);
        return (created, 0, 0);
    }

    /// <summary>One immutable image per page, for the render thread. A page that gained thumbnails since the last call gets a new copy.</summary>
    public SKImage[] Snapshot()
    {
        if (!_snapshotStale) return _snapshot;
        var images = new SKImage[_pages.Count];
        for (int i = 0; i < images.Length; i++)
        {
            var page = _pages[i];
            if (page.Dirty || page.Image is null)
            {
                page.Image = SKImage.FromPixelCopy(page.Bitmap.PeekPixels());
                page.Dirty = false;
            }
            images[i] = page.Image;
        }
        _snapshot = images;
        _snapshotStale = false;
        return images;
    }
}
