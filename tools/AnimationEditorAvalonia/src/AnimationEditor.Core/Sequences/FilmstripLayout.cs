using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>One frame of a chain as the filmstrip needs it: how long it shows and how big its sprite is on screen.</summary>
/// <param name="Duration">Seconds the frame shows.</param>
/// <param name="SpriteWidth">The sprite's width in pixels as displayed (a diagonal flip has already swapped width and height); 0 when unknown.</param>
/// <param name="SpriteHeight">The sprite's height in pixels as displayed; 0 when unknown.</param>
public readonly record struct FilmstripFrame(double Duration, double SpriteWidth, double SpriteHeight);

/// <summary>One showing of a frame within the block: <see cref="Cycle"/> is the loop cycle it starts in. Times are seconds from the block's start.</summary>
public readonly record struct FilmstripVisit(int Frame, double Start, double End, int Cycle);

/// <summary>
/// One cell of a block's filmstrip. <see cref="Frame"/> is the frame it draws (the longest of its visits). X and Width are
/// pixels from the block's left edge; <see cref="SpriteX"/> is where the sprite's left edge sits relative to the cell's
/// left edge (0 or negative), and <see cref="SpriteWidth"/> its width at the cell height. <see cref="IsMerged"/> marks a
/// group of frames too narrow to draw separately.
/// </summary>
public sealed record FilmstripCell(
    int Frame, IReadOnlyList<FilmstripVisit> Visits, double Start, double End, double X, double Width,
    double SpriteWidth, double SpriteX, bool IsMerged);

/// <summary>
/// Where a block's filmstrip draws each frame. Pure geometry, so the control only paints what this returns. Cells are to
/// scale in time (width = duration x pixels per second) and tile once per loop cycle, in the order playback shows the
/// frames (<see cref="PlaybackTimeline"/>: Reverse plays last to first, PingPong plays forward then back). A ping-pong's
/// end frames show twice as long, back to back across the turnaround and across the loop point, so each is one
/// double-length cell. The last cycle is clipped at the played end, which is where a cut chain stops.
/// </summary>
public static class FilmstripLayout
{
    /// <summary>Frames narrower than this many pixels are grouped into one cell.</summary>
    public const double SliverPixels = 3;

    /// <summary>Stops a pathological chain (microsecond frames, thousands of loops) from building millions of cells.</summary>
    private const int MaxUnits = 50_000;

    private sealed class Unit(FilmstripVisit first, double naturalEnd)
    {
        public int Frame = first.Frame;
        public double Start = first.Start;
        public double End = first.End;
        public double NaturalEnd = naturalEnd;
        public readonly List<FilmstripVisit> Visits = new() { first };
        public double NaturalDuration => NaturalEnd - Start;
    }

    /// <summary>
    /// <paramref name="chain"/>'s frames as the filmstrip needs them. <paramref name="textureSize"/> returns a frame's
    /// texture size in pixels, or null when it can't be read (the frame then has no sprite size).
    /// </summary>
    public static IReadOnlyList<FilmstripFrame> FramesOf(AnimationChainSave chain, Func<AnimationFrameSave, (int Width, int Height)?> textureSize)
    {
        var frames = new FilmstripFrame[chain.Frames.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            var frame = chain.Frames[i];
            double duration = PlaybackController.FrameLength(frame);
            if (textureSize(frame) is not { } size || size.Width <= 0 || size.Height <= 0)
            {
                frames[i] = new FilmstripFrame(duration, 0, 0);
                continue;
            }
            double width = Math.Abs(frame.RightCoordinate - frame.LeftCoordinate) * size.Width;
            double height = Math.Abs(frame.BottomCoordinate - frame.TopCoordinate) * size.Height;
            frames[i] = frame.FlipDiagonal ? new FilmstripFrame(duration, height, width) : new FilmstripFrame(duration, width, height);
        }
        return frames;
    }

    /// <summary>
    /// The cells of a chain played for <paramref name="played"/> seconds at <paramref name="pixelsPerSecond"/>, each
    /// <paramref name="cellHeight"/> tall. A cell narrower than its sprite is a window onto it: the sprite keeps its aspect
    /// at the cell height and is centered on the image (a clipped last cell keeps the window of its full width). A wider
    /// cell left-aligns the sprite. Frames under <see cref="SliverPixels"/> group until the group reaches that width (a
    /// leftover joins the group before it), and the group draws its longest frame. A frame with no sprite size draws square.
    /// </summary>
    public static IReadOnlyList<FilmstripCell> Cells(
        IReadOnlyList<FilmstripFrame> frames, bool pingPong, bool reverse, double played, double pixelsPerSecond, double cellHeight)
    {
        int count = frames.Count;
        if (count == 0 || played <= 0 || pixelsPerSecond <= 0 || cellHeight <= 0) return Array.Empty<FilmstripCell>();
        double forward = 0;
        foreach (var frame in frames) forward += Math.Max(0, frame.Duration);
        if (forward <= 0) return Array.Empty<FilmstripCell>();

        double cycle = PlaybackTimeline.CycleLength(forward, pingPong);
        int visitsPerCycle = PlaybackTimeline.VisitCount(count, pingPong);
        int cycles = (int)Math.Ceiling(played / cycle - 1e-9);

        var units = new List<Unit>();
        for (int k = 0; k < cycles && units.Count < MaxUnits; k++)
        {
            double start = k * cycle;
            for (int v = 0; v < visitsPerCycle && units.Count < MaxUnits; v++)
            {
                int frame = PlaybackTimeline.FrameOfVisit(v, count, pingPong, reverse);
                double end = start + Math.Max(0, frames[frame].Duration);
                if (start >= played - 1e-9) break;
                var visit = new FilmstripVisit(frame, start, Math.Min(end, played), k);
                if (pingPong && units.Count > 0 && units[^1].Frame == frame)
                {
                    // The turnaround and the loop point play the same frame twice in a row: one cell, no boundary.
                    var last = units[^1];
                    last.Visits.Add(visit);
                    last.End = visit.End;
                    last.NaturalEnd = end;
                }
                else
                {
                    units.Add(new Unit(visit, end));
                }
                start = end;
            }
        }

        // Frames under the sliver width accumulate until they reach it; a leftover at the end joins the last group.
        var groups = new List<List<Unit>>();
        List<Unit>? open = null;
        double openWidth = 0;
        foreach (var unit in units)
        {
            open ??= new List<Unit>();
            open.Add(unit);
            openWidth += (unit.End - unit.Start) * pixelsPerSecond;
            if (openWidth >= SliverPixels - 1e-6)
            {
                groups.Add(open);
                open = null;
                openWidth = 0;
            }
        }
        if (open is not null)
        {
            if (groups.Count > 0) groups[^1].AddRange(open);
            else groups.Add(open);
        }

        var cells = new List<FilmstripCell>(groups.Count);
        foreach (var group in groups)
        {
            var longest = group[0];
            foreach (var unit in group)
                if (unit.NaturalDuration > longest.NaturalDuration + 1e-12) longest = unit;

            double start = group[0].Start;
            double end = group[^1].End;
            double naturalWidth = (group[^1].NaturalEnd - start) * pixelsPerSecond;
            var sprite = frames[longest.Frame];
            double spriteWidth = sprite.SpriteWidth > 0 && sprite.SpriteHeight > 0
                ? sprite.SpriteWidth * cellHeight / sprite.SpriteHeight
                : cellHeight;
            // A wide cell keeps the sprite at the frame's start; a narrow one shows the middle of it.
            double spriteX = spriteWidth <= naturalWidth
                ? 0
                : Math.Clamp((naturalWidth - spriteWidth) / 2, naturalWidth - spriteWidth, 0);

            var visits = new List<FilmstripVisit>();
            foreach (var unit in group) visits.AddRange(unit.Visits);
            cells.Add(new FilmstripCell(longest.Frame, visits, start, end, start * pixelsPerSecond, (end - start) * pixelsPerSecond,
                spriteWidth, spriteX, group.Count > 1));
        }
        return cells;
    }

    /// <summary>The cell whose span holds <paramref name="x"/> (pixels from the block's left edge), or null.</summary>
    public static FilmstripCell? CellAt(IReadOnlyList<FilmstripCell> cells, double x)
    {
        int low = 0, high = cells.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            var cell = cells[mid];
            if (x < cell.X) high = mid - 1;
            else if (x >= cell.X + cell.Width) low = mid + 1;
            else return cell;
        }
        return null;
    }

    /// <summary>The showing in <paramref name="cell"/> at <paramref name="time"/> (seconds from the block's start), clamped to the cell.</summary>
    public static FilmstripVisit VisitAt(FilmstripCell cell, double time)
    {
        foreach (var visit in cell.Visits)
            if (time < visit.End) return visit;
        return cell.Visits[^1];
    }
}
