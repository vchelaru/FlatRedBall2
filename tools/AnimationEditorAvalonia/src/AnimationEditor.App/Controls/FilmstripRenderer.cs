using AnimationEditor.App.Services;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Paints the filmstrip row of a timeline block from <see cref="FilmstripLayout"/>'s cells. It only draws what the layout
/// returns: one cached-bitmap draw per visible cell, clipped to the cell, with off-screen cells skipped. Frame bitmaps
/// come from the shared <see cref="ThumbnailService"/> through each source's own texture path, so a sprite sheet is
/// decoded once for the library chips and the timeline together.
/// </summary>
internal sealed class FilmstripRenderer
{
    private const int MaxBitmapSide = 2048;
    private const int MaxCachedLayouts = 256;
    private const double NumberMinCellWidth = 26;
    private static readonly IBrush AmberBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB4, 0x54));

    // What never changes for a loaded chain: its texture paths, the frames without flips (the flip is applied when drawing,
    // so a diagonal flip works too), and the effective colors.
    private sealed class ChainInfo(string?[] paths, AnimationFrameSave[] plain, ResolvedFrameColor[] colors)
    {
        public readonly string?[] Paths = paths;
        public readonly AnimationFrameSave[] Plain = plain;
        public readonly ResolvedFrameColor[] Colors = colors;
    }

    private readonly record struct LayoutKey(AnimationChainSave Chain, double Played, double PixelsPerSecond, double Height);

    private readonly ConditionalWeakTable<AnimationChainSave, ChainInfo> _infos = new();
    // The texture paths a layout was sized from, so a texture changing on disk drops only the layouts that used it.
    private readonly record struct Layout(IReadOnlyList<FilmstripCell> Cells, string?[] Paths);

    private readonly Dictionary<LayoutKey, Layout> _layouts = new();
    private readonly Dictionary<(int Number, IBrush Brush), FormattedText> _numbers = new();

    private ThumbnailService? _thumbnails;

    /// <summary>Raised when a texture changed on disk and the cells laid out from it were dropped, so the owner repaints.</summary>
    public event Action? TextureChanged;

    public ThumbnailService? Thumbnails
    {
        get => _thumbnails;
        set
        {
            if (_thumbnails is { } old) old.PathInvalidated -= OnPathInvalidated;
            _thumbnails = value;
            if (value is not null) value.PathInvalidated += OnPathInvalidated;
        }
    }

    // A frame's cell size is its region of the texture in pixels, so a new texture size changes every cell drawn from it.
    private void OnPathInvalidated(string path)
    {
        // The atlas holds thumbnails of every sheet, so it starts over; they are cut again as the strips draw.
        Atlas.Clear();
        TextureChanged?.Invoke();
        List<LayoutKey>? stale = null;
        foreach (var (key, layout) in _layouts)
            if (Array.Exists(layout.Paths, p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                (stale ??= new()).Add(key);
        if (stale is null) return;
        foreach (var key in stale) _layouts.Remove(key);
        TextureChanged?.Invoke();
    }

    /// <summary>The frame's work counters, set by the control before each draw; null counts nothing.</summary>
    public TimelineFrameStats? Stats { get; set; }

    internal FilmstripAtlas Atlas { get; } = new();

    /// <summary>Forgets the laid-out cells: the document or the chain data behind them may have changed.</summary>
    public void Invalidate()
    {
        _layouts.Clear();
        _numbers.Clear();
    }

    public IReadOnlyList<FilmstripCell> CellsFor(SequenceDocument document, string source, AnimationChainSave chain, double played, double pixelsPerSecond, double height)
    {
        var key = new LayoutKey(chain, played, pixelsPerSecond, height);
        if (_layouts.TryGetValue(key, out var cached)) return cached.Cells;
        var paths = InfoFor(document, source, chain).Paths;
        var frames = FilmstripLayout.FramesOf(chain, frame =>
            Thumbnails?.GetBitmap(document.ResolveTexturePath(source, frame)) is { } bitmap ? (bitmap.Width, bitmap.Height) : null);
        var cells = FilmstripLayout.Cells(frames, chain.PingPong, chain.Reverse, played, pixelsPerSecond, height);
        if (_layouts.Count >= MaxCachedLayouts) _layouts.Clear();
        _layouts[key] = new Layout(cells, paths);
        return cells;
    }

    /// <summary>The frame's whole sprite fit in a square of <paramref name="size"/> pixels, for the hover card.</summary>
    public Bitmap? Enlarged(SequenceDocument document, string source, AnimationChainSave chain, int frameIndex, int size)
    {
        if (Thumbnails is null || frameIndex < 0 || frameIndex >= chain.Frames.Count) return null;
        var info = InfoFor(document, source, chain);
        return Thumbnails.GetFrameThumbnail(info.Paths[frameIndex], chain.Frames[frameIndex], info.Colors[frameIndex], size, size);
    }

    private ChainInfo InfoFor(SequenceDocument document, string source, AnimationChainSave chain)
    {
        if (_infos.TryGetValue(chain, out var info)) return info;
        int count = chain.Frames.Count;
        var paths = new string?[count];
        var plain = new AnimationFrameSave[count];
        for (int i = 0; i < count; i++)
        {
            var frame = chain.Frames[i];
            paths[i] = document.ResolveTexturePath(source, frame);
            plain[i] = new AnimationFrameSave
            {
                LeftCoordinate = frame.LeftCoordinate, RightCoordinate = frame.RightCoordinate,
                TopCoordinate = frame.TopCoordinate, BottomCoordinate = frame.BottomCoordinate,
            };
        }
        info = new ChainInfo(paths, plain, EffectiveFrameColor.ResolveAll(chain.Frames));
        _infos.Add(chain, info);
        return info;
    }

    /// <summary>
    /// Draws <paramref name="cells"/> into <paramref name="film"/> (the block's filmstrip rectangle). Only cells that
    /// overlap [<paramref name="minX"/>, <paramref name="maxX"/>] are drawn.
    /// </summary>
    public void Draw(DrawingContext context, SequenceDocument document, string source, AnimationChainSave chain,
        IReadOnlyList<FilmstripCell> cells, Rect film, double minX, double maxX, double scaling, Pen boundary, IBrush numberBrush)
    {
        if (cells.Count == 0) return;
        if (Stats is { } counted) counted.Cells += cells.Count;
        var info = InfoFor(document, source, chain);
        Atlas.BeginFrame();
        // The sprites, merged-group bars and boundary lines of the whole strip go out as one operation; only a sprite the
        // atlas can't hold (wider than a page) falls back to a clipped bitmap draw.
        Color? boundaryColor = boundary.Brush is ISolidColorBrush solid
            ? Color.FromArgb((byte)Math.Round(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B)
            : null;
        _draws.Clear();
        _numbered.Clear();
        int first = FirstVisible(cells, minX - film.X);
        for (int i = first; i < cells.Count; i++)
        {
            var cell = cells[i];
            double left = Math.Round(film.X + cell.X);
            if (left > maxX) break;
            double right = Math.Max(left + 1, Math.Round(film.X + cell.X + cell.Width));
            var rect = new Rect(left, film.Y, right - left, film.Height);
            if (Stats is { } stats)
            {
                stats.CellsIssued++;
                if (right > minX && left < maxX) stats.CellsVisible++;
            }

            AtlasEntry? sprite = null;
            double spriteLeft = 0, spriteTop = 0;
            if (SpriteEntry(chain, info, cell, rect.Height, scaling, out bool fallback) is { } entry)
            {
                sprite = entry;
                spriteLeft = Math.Round(rect.X + cell.SpriteX);
                spriteTop = rect.Y + Math.Round((rect.Height - entry.Height / scaling) / 2);
                if (Stats is { } drawn) drawn.Images++;
            }
            else if (fallback)
            {
                if (Stats is { } clipped) clipped.Clips++;
                using (context.PushClip(rect))
                    DrawSprite(context, chain, info, cell, rect, scaling);
            }

            bool boundaryInOp = i > 0 && boundaryColor is not null;
            _draws.Add(new FilmstripCellDraw(left, right, boundaryInOp, cell.IsMerged, sprite ?? default, sprite is not null, spriteLeft, spriteTop));
            if (i > 0 && boundaryColor is null)
                context.DrawLine(boundary, new Point(left + 0.5, film.Y), new Point(left + 0.5, film.Bottom));
            if (rect.Width >= NumberMinCellWidth) _numbered.Add((cell.Frame + 1, rect));
        }

        if (_draws.Count > 0)
        {
            if (Stats is { } ops) ops.FilmstripOps++;
            context.Custom(new FilmstripOp(_draws.ToArray(), Atlas.Snapshot(), film, boundaryColor ?? default));
        }
        foreach (var (number, rect) in _numbered)
            DrawNumber(context, number, rect, numberBrush);
    }

    private readonly List<FilmstripCellDraw> _draws = new();
    private readonly List<(int Number, Rect Rect)> _numbered = new();

    // The atlas thumbnail for a cell's frame, cut at the display's pixel height; null when the frame has nothing to draw
    // or the atlas can't hold it (fallback says which).
    private AtlasEntry? SpriteEntry(AnimationChainSave chain, ChainInfo info, FilmstripCell cell, double rowHeight, double scaling, out bool fallback)
    {
        fallback = false;
        if (Thumbnails is null || info.Paths[cell.Frame] is not { } path || Thumbnails.GetBitmap(path) is not { } sheet) return null;
        var frame = chain.Frames[cell.Frame];
        int target = Math.Max(1, (int)Math.Round(rowHeight * scaling));
        return Atlas.Get(sheet, path, info.Plain[cell.Frame], frame.FlipHorizontal, frame.FlipVertical, frame.FlipDiagonal, info.Colors[cell.Frame], target, out fallback);
    }

    private void DrawSprite(DrawingContext context, AnimationChainSave chain, ChainInfo info, FilmstripCell cell, Rect rect, double scaling)
    {
        if (Thumbnails is null) return;
        var frame = chain.Frames[cell.Frame];
        bool diagonal = frame.FlipDiagonal;
        int target = Math.Max(1, (int)Math.Round(rect.Height * scaling));
        // The bitmap is the unflipped crop. A diagonal flip turns it a quarter, so its width becomes the displayed height.
        var bitmap = Thumbnails.GetFrameThumbnail(info.Paths[cell.Frame], info.Plain[cell.Frame], info.Colors[cell.Frame],
            diagonal ? target : MaxBitmapSide, diagonal ? MaxBitmapSide : target);
        if (bitmap is null) return;
        if (Stats is { } drawn) drawn.Images++;

        double bitmapWidth = bitmap.PixelSize.Width / scaling;
        double bitmapHeight = bitmap.PixelSize.Height / scaling;
        double shownWidth = diagonal ? bitmapHeight : bitmapWidth;
        double shownHeight = diagonal ? bitmapWidth : bitmapHeight;
        double left = Math.Round(rect.X + cell.SpriteX);
        double top = rect.Y + Math.Round((rect.Height - shownHeight) / 2);

        if (!(frame.FlipHorizontal || frame.FlipVertical || diagonal))
        {
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                new Rect(left, top, bitmapWidth, bitmapHeight));
            return;
        }

        // The same flip matrix the preview applies, turned about the middle of the sprite.
        var (a, b, c, d) = FlipScaleCalculator.ComputeMatrix(frame.FlipHorizontal, frame.FlipVertical, diagonal);
        var center = new Point(left + shownWidth / 2, top + shownHeight / 2);
        var flip = Matrix.CreateTranslation(-center.X, -center.Y) * new Matrix(a, c, b, d, 0, 0) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(flip))
        {
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                new Rect(center.X - bitmapWidth / 2, center.Y - bitmapHeight / 2, bitmapWidth, bitmapHeight));
        }
    }

    private void DrawNumber(DrawingContext context, int number, Rect rect, IBrush brush)
    {
        if (!_numbers.TryGetValue((number, brush), out var text))
        {
            text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 9, brush);
            _numbers[(number, brush)] = text;
        }
        context.DrawText(text, new Point(rect.Right - text.Width - 2, rect.Bottom - text.Height - 1));
    }

    // The first cell that ends at or after x (cells are in order of X).
    private static int FirstVisible(IReadOnlyList<FilmstripCell> cells, double x)
    {
        int low = 0, high = cells.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (cells[mid].X + cells[mid].Width < x) low = mid + 1;
            else high = mid;
        }
        return low;
    }
}
