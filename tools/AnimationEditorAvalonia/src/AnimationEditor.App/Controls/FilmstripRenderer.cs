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
    private readonly Dictionary<LayoutKey, IReadOnlyList<FilmstripCell>> _layouts = new();
    private readonly Dictionary<(int Number, IBrush Brush), FormattedText> _numbers = new();

    public ThumbnailService? Thumbnails { get; set; }

    /// <summary>Forgets the laid-out cells: the document or the chain data behind them may have changed.</summary>
    public void Invalidate()
    {
        _layouts.Clear();
        _numbers.Clear();
    }

    public IReadOnlyList<FilmstripCell> CellsFor(SequenceDocument document, string source, AnimationChainSave chain, double played, double pixelsPerSecond, double height)
    {
        var key = new LayoutKey(chain, played, pixelsPerSecond, height);
        if (_layouts.TryGetValue(key, out var cached)) return cached;
        var frames = FilmstripLayout.FramesOf(chain, frame =>
            Thumbnails?.GetBitmap(document.ResolveTexturePath(source, frame)) is { } bitmap ? (bitmap.Width, bitmap.Height) : null);
        var cells = FilmstripLayout.Cells(frames, chain.PingPong, chain.Reverse, played, pixelsPerSecond, height);
        if (_layouts.Count >= MaxCachedLayouts) _layouts.Clear();
        _layouts[key] = cells;
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
        var info = InfoFor(document, source, chain);
        int first = FirstVisible(cells, minX - film.X);
        for (int i = first; i < cells.Count; i++)
        {
            var cell = cells[i];
            double left = Math.Round(film.X + cell.X);
            if (left > maxX) break;
            double right = Math.Max(left + 1, Math.Round(film.X + cell.X + cell.Width));
            var rect = new Rect(left, film.Y, right - left, film.Height);
            using (context.PushClip(rect))
            {
                DrawSprite(context, chain, info, cell, rect, scaling);
                if (cell.IsMerged)
                    context.FillRectangle(AmberBrush, new Rect(rect.X, rect.Bottom - 2, rect.Width, 2));
                if (rect.Width >= NumberMinCellWidth)
                    DrawNumber(context, cell.Frame + 1, rect, numberBrush);
            }
            if (i > 0) context.DrawLine(boundary, new Point(left + 0.5, film.Y), new Point(left + 0.5, film.Bottom));
        }
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
