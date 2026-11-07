using AnimationEditor.App.Services;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

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

    private void DrawFilmstrip(DrawingContext context, TimelineBlock block, Rect film)
    {
        if (_document is not { } document || document.FindChain(block.Reference) is not { Frames.Count: > 0 } chain) return;
        var cells = FilmstripCells(block);
        var boundary = new Pen(Brush("BgCanvas"), 1);
        double scaling = (VisualRoot as TopLevel)?.RenderScaling ?? 1;
        using (context.PushClip(film))
            _filmstrip.Draw(context, document, block.Reference.Source, chain, cells, film,
                TimelineViewport.HeaderWidth, Bounds.Width, scaling, boundary, Brush("Ink"));
    }
}
