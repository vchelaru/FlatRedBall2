using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>One frame of a chain as the event row needs it: how long it shows and the names of the events it carries.</summary>
public readonly record struct EventFrame(double Duration, IReadOnlyList<string> Names);

/// <summary>One firing of a frame event within a block. <see cref="Time"/> is seconds from the block's start; <see cref="IsReturn"/> marks the way back of a ping-pong.</summary>
public readonly record struct EventOccurrence(string Name, int Frame, double Time, int Cycle, bool IsReturn);

/// <summary>One flag on the event row: <see cref="X"/> pixels from the block's left edge, holding every occurrence that merged into it.</summary>
public sealed record EventMarker(double X, double Time, IReadOnlyList<EventOccurrence> Occurrences)
{
    public int Count => Occurrences.Count;
}

/// <summary>
/// Where a block's event flags go. Pure geometry like <see cref="FilmstripLayout"/>, so the control only paints what this
/// returns. A frame event fires when playback enters its frame, so a flag sits at the start of each showing that enters
/// one (<see cref="PlaybackTimeline.EntersNewFrame"/>): every loop cycle, mirrored on a ping-pong return half or a reverse
/// chain, and nothing for a ping-pong's turnaround or loop point. A showing that would start at or after the played end
/// (the cut tail, the rest of a partial last cycle) never plays, so it fires nothing.
/// </summary>
public static class EventMarkers
{
    /// <summary>Flags closer together than this many pixels merge into one with a count.</summary>
    public const double MarkerPixels = 9;

    /// <summary>Stops a pathological chain (microsecond frames, thousands of loops) from building millions of occurrences.</summary>
    private const int MaxOccurrences = 50_000;

    public static IReadOnlyList<EventFrame> FramesOf(AnimationChainSave chain) =>
        chain.Frames.Select(frame => new EventFrame(
            PlaybackController.FrameLength(frame), frame.Events.Select(e => e.Name ?? string.Empty).ToArray())).ToArray();

    /// <summary>
    /// Every event that fires while <paramref name="frames"/> play for <paramref name="played"/> seconds, in time order.
    /// A looping chain fires in every cycle of <paramref name="played"/>. With <paramref name="loop"/> false the chain has
    /// one cycle, so the rest of <paramref name="played"/> is the hold, which enters no frame and fires nothing.
    /// </summary>
    public static IReadOnlyList<EventOccurrence> Occurrences(IReadOnlyList<EventFrame> frames, bool pingPong, bool reverse, double played, bool loop = true)
    {
        int count = frames.Count;
        if (count == 0 || played <= 0) return Array.Empty<EventOccurrence>();
        double forward = 0;
        foreach (var frame in frames) forward += Math.Max(0, frame.Duration);
        if (forward <= 0) return Array.Empty<EventOccurrence>();

        double cycle = PlaybackTimeline.CycleLength(forward, pingPong);
        int visitsPerCycle = PlaybackTimeline.VisitCount(count, pingPong);
        // A chain that does not loop plays one cycle, then holds its last frame: the hold enters no frame, so it fires nothing.
        int cycles = loop ? (int)Math.Ceiling(played / cycle - 1e-9) : 1;

        var result = new List<EventOccurrence>();
        for (int k = 0; k < cycles && result.Count < MaxOccurrences; k++)
        {
            double start = k * cycle;
            for (int v = 0; v < visitsPerCycle && result.Count < MaxOccurrences; v++)
            {
                if (start >= played - 1e-9) break;
                int frame = PlaybackTimeline.FrameOfVisit(v, count, pingPong, reverse);
                double end = start + Math.Max(0, frames[frame].Duration);
                // Playback enters the first showing at once; after that only a changed frame fires.
                bool enters = (k == 0 && v == 0) || PlaybackTimeline.EntersNewFrame(v, count, pingPong, reverse);
                if (enters)
                {
                    bool isReturn = pingPong && (v >= count ^ reverse);
                    foreach (string name in frames[frame].Names)
                        result.Add(new EventOccurrence(name, frame, start, k, isReturn));
                }
                start = end;
            }
        }
        return result;
    }

    /// <summary>
    /// Flags for <paramref name="occurrences"/> at <paramref name="pixelsPerSecond"/>: an occurrence closer than
    /// <paramref name="markerWidth"/> pixels to the first of the flag before it joins that flag, so it never draws over it.
    /// </summary>
    public static IReadOnlyList<EventMarker> Group(IReadOnlyList<EventOccurrence> occurrences, double pixelsPerSecond, double markerWidth = MarkerPixels)
    {
        var markers = new List<EventMarker>();
        if (pixelsPerSecond <= 0) return markers;
        List<EventOccurrence>? open = null;
        double openX = 0;
        void Close()
        {
            if (open is not null) markers.Add(new EventMarker(openX, open[0].Time, open));
            open = null;
        }
        foreach (var occurrence in occurrences)
        {
            double x = occurrence.Time * pixelsPerSecond;
            if (open is not null && x - openX >= markerWidth - 1e-9) Close();
            if (open is null)
            {
                open = new List<EventOccurrence>();
                openX = x;
            }
            open.Add(occurrence);
        }
        Close();
        return markers;
    }

    public static IReadOnlyList<EventMarker> For(IReadOnlyList<EventFrame> frames, bool pingPong, bool reverse, double played, double pixelsPerSecond, double markerWidth = MarkerPixels, bool loop = true) =>
        Group(Occurrences(frames, pingPong, reverse, played, loop), pixelsPerSecond, markerWidth);

    /// <summary>The flag whose span [X, X + <paramref name="markerWidth"/>) holds <paramref name="x"/> (pixels from the block's left edge), or null.</summary>
    public static EventMarker? MarkerAt(IReadOnlyList<EventMarker> markers, double x, double markerWidth = MarkerPixels)
    {
        int low = 0, high = markers.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            var marker = markers[mid];
            if (x < marker.X) high = mid - 1;
            else if (x >= marker.X + markerWidth) low = mid + 1;
            else return marker;
        }
        return null;
    }
}
