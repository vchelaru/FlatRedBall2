using AnimationEditor.App.Services;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.App.Controls;

public sealed partial class SequenceTimelineControl
{
    private readonly FilmstripRenderer _filmstrip = new();

    /// <summary>Decodes the source textures the filmstrips draw. Shared with the library chips, so a sheet is decoded once.</summary>
    public ThumbnailService? Thumbnails
    {
        get => _filmstrip.Thumbnails;
        set => _filmstrip.Thumbnails = value;
    }

    /// <summary>The cells <paramref name="block"/>'s filmstrip draws at the current zoom; empty for a missing or empty chain.</summary>
    public IReadOnlyList<FilmstripCell> FilmstripCells(TimelineBlock block) =>
        _document is { } document && document.FindChain(block.Reference) is { Frames.Count: > 0 } chain
            ? _filmstrip.CellsFor(document, block.Reference.Source, chain, block.Played, Viewport.PixelsPerSecond, TimelineLayout.FilmHeight)
            : Array.Empty<FilmstripCell>();

    /// <summary>The card shown over the filmstrip cell under the pointer, or null when there is none.</summary>
    public FilmstripCard? HoverCard { get; private set; }

    private Point _hoverPoint;
    private (string Source, AnimationChainSave Chain)? _hoverSource;
    private string[]? _hoverBlockLines;

    /// <summary>The block's <c>Cycles 2</c> and <c>Length 1 s</c> lines, shown as the last lines of a filmstrip <see cref="HoverCard"/>, or null when the card is for an event flag.</summary>
    public string[]? HoverCardBlockLines => HoverCard is null ? null : _hoverBlockLines;

    // The card follows the pointer. It is off during any drag (a chain, the ruler, a library chip) and after a scroll or zoom.
    private void UpdateHover(Point point)
    {
        FilmstripCard? card = null;
        (string, AnimationChainSave)? source = null;
        _hoverBlockLines = null;
        if (_drag == DragKind.None && !_scrubbing && !_panning && _chipGhost is null && point.X >= Viewport.ColumnWidth
            && _document is { } document && Sequence is { } sequence
            && Viewport.RowAt(point.Y, sequence.Tracks.Count) is { Kind: TimelineRowKind.Track } row)
        {
            double inLane = point.Y - Viewport.LaneTop(row.Track);
            if (inLane >= TimelineLayout.FilmTop && inLane < TimelineLayout.BodyBottom
                && TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y) is { } hit
                && document.FindChain(hit.Block.Reference) is { } chain
                && FilmstripLayout.CellAt(FilmstripCells(hit.Block), point.X - Viewport.XAt(hit.Block.Start)) is { } cell)
            {
                var visit = FilmstripLayout.VisitAt(cell, Viewport.TimeAt(point.X) - hit.Block.Start);
                source = (hit.Block.Reference.Source, chain);
                card = FilmstripReadout.At(hit.Block.Reference.Chain, chain.Frames.Count, hit.Block.Start, cell, visit);
                _hoverBlockLines = BlockLabel.CardLines(hit.Block.Reference.Loops, hit.Block.Played);
            }
            else if (EventCardAt(point, inLane) is { } eventCard)
            {
                card = eventCard.Card;
                source = eventCard.Source;
            }
        }
        if (card is null && HoverCard is null) return;
        _hoverPoint = point;
        _hoverSource = source;
        HoverCard = card;
        InvalidatePopups();
    }

    private void ClearHover()
    {
        ClearBlockTooltip();
        if (HoverCard is null) return;
        HoverCard = null;
        InvalidatePopups();
    }

    private const double CardThumb = 64;

    // The frame enlarged beside its facts, kept inside the control. Drawn, not a control, so it can't take a click.
    private void DrawHover(DrawingContext context, Size size)
    {
        if (HoverCard is not { } card || _document is not { } document) return;
        var texts = new List<FormattedText> { Text(card.Title, 11, Brush("Ink"), FontWeight.SemiBold) };
        texts.AddRange(card.Lines.Select(line => Text(line, 11, Brush("InkMid"))));
        if (_hoverBlockLines is { } blockLines) texts.AddRange(blockLines.Select(line => Text(line, 11, Brush("InkMid"))));
        double textWidth = texts.Max(t => t.Width);
        double textHeight = texts.Sum(t => t.Height);
        double width = 8 + CardThumb + 8 + textWidth + 8;
        double height = Math.Max(CardThumb, textHeight) + 16;
        double x = _hoverPoint.X + 14;
        double y = _hoverPoint.Y + 16;
        if (x + width > size.Width) x = _hoverPoint.X - 10 - width;
        if (y + height > size.Height) y = _hoverPoint.Y - 10 - height;
        var rect = new Rect(Math.Clamp(x, 0, Math.Max(0, size.Width - width)), Math.Clamp(y, 0, Math.Max(0, size.Height - height)), width, height);

        context.FillRectangle(Brush("BgPanel"), rect, 5);
        context.DrawRectangle(null, new Pen(Brush("LineStrong"), 1), rect, 5, 5);
        var thumb = new Rect(rect.X + 8, rect.Y + 8, CardThumb, CardThumb);
        context.FillRectangle(Brush("BgCanvas"), thumb, 3);
        double scaling = (VisualRoot as TopLevel)?.RenderScaling ?? 1;
        if (_hoverSource is { } hoverSource
            && _filmstrip.Enlarged(document, hoverSource.Source, hoverSource.Chain, card.Frame, (int)Math.Round(CardThumb * scaling)) is { } bitmap)
        {
            double w = bitmap.PixelSize.Width / scaling, h = bitmap.PixelSize.Height / scaling;
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                new Rect(thumb.X + (thumb.Width - w) / 2, thumb.Y + (thumb.Height - h) / 2, w, h));
        }
        double textY = rect.Y + 8;
        foreach (var text in texts)
        {
            context.DrawText(text, new Point(thumb.Right + 8, textY));
            textY += text.Height;
        }
    }

    // With a block paint the one operation also draws the block's fill, backdrop, outline and cycle marks, and runs without
    // the film clip (the outline reaches past the film row). Without one it is the filmstrip alone, under that clip.
    private void DrawFilmstrip(DrawingContext context, TimelineBlock block, Rect film, BlockPaint? paint = null)
    {
        bool probedAway = ProbeMode is TimelineProbeMode.NoFilmstrips or TimelineProbeMode.NoFilmstripsOrText or TimelineProbeMode.BackgroundOnly;
        bool drawn = false;
        if (!probedAway && _document is { } document && document.FindChain(block.Reference) is { Frames.Count: > 0 } chain)
        {
            var cells = FilmstripCells(block);
            var boundary = new Pen(Brush("LineStrong"), 1);
            double scaling = (VisualRoot as TopLevel)?.RenderScaling ?? 1;
            _filmstrip.Stats = Stats;
            if (paint is not null)
            {
                drawn = _filmstrip.Draw(context, document, block.Reference.Source, chain, cells, film,
                    Viewport.ColumnWidth, Bounds.Width, scaling, boundary, Brush("Ink"), paint);
            }
            else
            {
                using (context.PushClip(film))
                    _filmstrip.Draw(context, document, block.Reference.Source, chain, cells, film,
                        Viewport.ColumnWidth, Bounds.Width, scaling, boundary, Brush("Ink"));
            }
        }
        if (!drawn && paint is not null) _filmstrip.DrawBlockOnly(context, film, paint);
    }
}
