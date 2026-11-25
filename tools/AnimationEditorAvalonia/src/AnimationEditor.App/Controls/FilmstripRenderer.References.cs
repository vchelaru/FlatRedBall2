using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AnimationEditor.App.Controls;

/// <summary>What the reference clips' composite cells cost, for the F3 overlay and for measuring.</summary>
internal sealed class ReferenceFilmstripStats
{
    /// <summary>Composite cells cut since the last <see cref="Reset"/>.</summary>
    public int CellsRendered;

    /// <summary>Visible cells whose picture was already in the atlas.</summary>
    public int CacheHits;

    /// <summary>Visible cells left empty this frame because the per-frame budget was spent; they fill in on the repaint that follows.</summary>
    public int Pending;

    /// <summary>Paints that drew reference clips since the last <see cref="Reset"/>.</summary>
    public int Passes;

    /// <summary>Milliseconds spent cutting composite cells.</summary>
    public double RenderMs;

    public void Reset() => CellsRendered = CacheHits = Pending = Passes = 0;
}

internal sealed partial class FilmstripRenderer
{
    // At most this long per paint is spent cutting new composite cells (one is always cut), the rest wait for the next paint.
    private const double ReferenceBudgetMs = 6;

    // The poses of a clip's cells at one zoom and clip length by the time they are sampled at, each worked out the first time
    // its cell is visible.
    private sealed class ClipPoses(double pixelsPerSecond, double length)
    {
        public readonly double PixelsPerSecond = pixelsPerSecond;
        public readonly double Length = length;
        public readonly Dictionary<double, ScenePose?> ByTime = new();
    }

    // Rebuilt with the timeline's references on every edit, so a new instance starts with no poses.
    private readonly ConditionalWeakTable<TimelineReference, ClipPoses> _clips = new();
    private long _referenceTicks;
    private int _cutThisPass;

    internal ReferenceFilmstripStats ReferenceStats { get; } = new();

    /// <summary>Starts a paint's composite-cell budget.</summary>
    public void BeginReferencePass()
    {
        _referenceTicks = Stopwatch.GetTimestamp();
        ReferenceStats.Passes++;
        _cutThisPass = 0;
    }

    /// <summary>
    /// Draws a collapsed reference clip's composite filmstrip into <paramref name="film"/> (the clip's film row, as wide as the
    /// clip): each cell shows the target sequence as the preview draws it at that instant, cut from the shared sprite sheets
    /// and kept in the atlas by its pose, so equal poses and repeated cycles are cut once. Only cells overlapping
    /// [<paramref name="minX"/>, <paramref name="maxX"/>] are drawn. Returns the visible cells still waiting for a turn to be
    /// cut; the owner repaints for them.
    /// </summary>
    public int DrawReference(DrawingContext context, SequenceDocument document, TimelineReference clip, double pixelsPerSecond,
        Rect film, double minX, double maxX, double scaling, Color boundary)
    {
        if (Thumbnails is not { } thumbnails || clip.TargetLength <= 0
            || SequenceReferences.Find(document.Save.Sequences, clip.Reference.Sequence) is not { } target) return 0;
        if (!_clips.TryGetValue(clip, out var info) || info.PixelsPerSecond != pixelsPerSecond || info.Length != clip.Length)
        {
            info = new ClipPoses(pixelsPerSecond, clip.Length);
            _clips.AddOrUpdate(clip, info);
        }
        // A long clip of a short target has millions of cells, so only the visible ones are ever made.
        var cells = ReferenceFilmstrip.Cells(clip.TargetLength, clip.Reference.Length, pixelsPerSecond, minX - film.X, maxX - film.X + 1);
        if (cells.Count == 0) return 0;

        Atlas.BeginFrame();
        int height = Math.Max(1, (int)Math.Round(film.Height * scaling));
        var stats = ReferenceStats;
        int pending = 0;
        _draws.Clear();
        foreach (var cell in cells)
        {
            double left = Math.Round(film.X + cell.X);
            if (left > maxX) break;
            double right = Math.Max(left + 1, Math.Round(film.X + cell.X + cell.Width));

            if (!info.ByTime.TryGetValue(cell.Time, out var pose))
                info.ByTime[cell.Time] = pose = ReferenceFilmstrip.PoseAt(clip.Reference, target, cell.Time, document, document.Save.Sequences);
            AtlasEntry? entry = null;
            if (pose is not null)
            {
                if (Atlas.TryGetComposite(pose, height, out entry)) stats.CacheHits++;
                else if (_cutThisPass == 0 || Stopwatch.GetElapsedTime(_referenceTicks).TotalMilliseconds < ReferenceBudgetMs)
                {
                    long start = Stopwatch.GetTimestamp();
                    using var bitmap = ReferenceCellRenderer.Render(pose, height, path => thumbnails.GetBitmap(path));
                    entry = Atlas.AddComposite(pose, height, bitmap);
                    stats.RenderMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    stats.CellsRendered++;
                    _cutThisPass++;
                }
                else
                {
                    pending++;
                    stats.Pending++;
                }
            }

            double spriteLeft = 0, spriteTop = 0;
            if (entry is { } sprite)
            {
                spriteLeft = Math.Round(film.X + cell.X + (cell.FullWidth - sprite.Width / scaling) / 2);
                spriteTop = film.Y + Math.Round((film.Height - sprite.Height / scaling) / 2);
            }
            _draws.Add(new FilmstripCellDraw(left, right, cell.X > 0, false, entry ?? default, entry is not null, spriteLeft, spriteTop));
        }

        if (_draws.Count > 0)
            context.Custom(new FilmstripOp(_draws.ToArray(), Atlas.Snapshot(), film, boundary, Probe, PerCellDraws, WaitForGpu));
        return pending;
    }
}
