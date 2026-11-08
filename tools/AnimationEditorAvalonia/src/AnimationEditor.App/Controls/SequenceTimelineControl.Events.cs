using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AnimationEditor.App.Controls;

// The event row: a thin strip under a track's filmstrip that only a track with frame events has. What it shows comes from
// EventMarkers; this file caches it per chain and paints it.
public sealed partial class SequenceTimelineControl
{
    private const int MaxCachedEventLayouts = 256;

    private readonly record struct EventLayoutKey(AnimationChainSave Chain, bool Loop, double Played, double PixelsPerSecond);

    private readonly Dictionary<(AnimationChainSave Chain, bool Loop, double Played), IReadOnlyList<EventOccurrence>> _eventOccurrences = new();
    private readonly Dictionary<EventLayoutKey, IReadOnlyList<EventMarker>> _eventLayouts = new();

    private void InvalidateEvents()
    {
        _eventOccurrences.Clear();
        _eventLayouts.Clear();
    }

    private IReadOnlyList<EventOccurrence> EventOccurrencesOf(TimelineBlock block)
    {
        if (block.IsMissing || _document is not { } document || document.FindChain(block.Reference) is not { Frames.Count: > 0 } chain)
            return Array.Empty<EventOccurrence>();
        var key = (chain, chain.Loop, block.Played);
        if (_eventOccurrences.TryGetValue(key, out var cached)) return cached;
        var occurrences = EventMarkers.Occurrences(EventMarkers.FramesOf(chain), chain.PingPong, chain.Reverse, block.Played, chain.Loop);
        if (_eventOccurrences.Count >= MaxCachedEventLayouts) _eventOccurrences.Clear();
        _eventOccurrences[key] = occurrences;
        return occurrences;
    }

    /// <summary>Whether <paramref name="block"/> has any event that fires while it plays. A track with such a block gets the event row.</summary>
    private bool HasEvents(TimelineBlock block) => EventOccurrencesOf(block).Count > 0;

    /// <summary>The flags on <paramref name="block"/>'s event row at the current zoom; X is pixels from the block's left edge. Empty for a block with no events.</summary>
    public IReadOnlyList<EventMarker> EventMarkersOf(TimelineBlock block)
    {
        var occurrences = EventOccurrencesOf(block);
        if (occurrences.Count == 0 || _document?.FindChain(block.Reference) is not { } chain) return Array.Empty<EventMarker>();
        var key = new EventLayoutKey(chain, chain.Loop, block.Played, Viewport.PixelsPerSecond);
        if (_eventLayouts.TryGetValue(key, out var cached)) return cached;
        var markers = EventMarkers.Group(occurrences, Viewport.PixelsPerSecond);
        if (_eventLayouts.Count >= MaxCachedEventLayouts) _eventLayouts.Clear();
        _eventLayouts[key] = markers;
        return markers;
    }

    private const double FlagHeight = 7;
    private const double NameGap = 3;

    private void DrawEvents(DrawingContext context, TimelineBlock block, double size)
    {
        var markers = EventMarkersOf(block);
        if (markers.Count == 0) return;
        Stats.Markers += markers.Count;
        double pps = Viewport.PixelsPerSecond;
        double left = Viewport.XAt(block.Start);
        double right = left + block.Played * pps;
        if (right < TimelineViewport.HeaderWidth || left > size) return;

        // A name may run past its block's end, up to where the next chain on the track starts.
        double trackRoomRight = size;
        foreach (var other in _blocks)
            if (other.Track == block.Track && other.Start > block.Start)
                trackRoomRight = Math.Min(trackRoomRight, Viewport.XAt(other.Start));

        double top = Viewport.LaneTop(block.Track);
        double rowTop = top + TimelineLayout.EventRowTop;
        double rowBottom = rowTop + TimelineLayout.EventRowHeight;
        var color = AccentCoolColor();
        var ink = Brush("Ink");
        var pole = new Pen(new SolidColorBrush(color, 0.9), 1);

        // A faint band the length of the block, so the row reads as belonging to its chain.
        context.FillRectangle(new SolidColorBrush(color, 0.12), new Rect(left, rowTop, Math.Max(1, right - left), TimelineLayout.EventRowHeight));

        for (int i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            double x = left + marker.X;
            if (x + EventMarkers.MarkerPixels < TimelineViewport.HeaderWidth) continue;
            if (x > size) break;
            Stats.MarkersIssued++;
            double poleX = Snap(x);
            context.DrawLine(pole, new Point(poleX, top + TimelineLayout.BodyBottom), new Point(poleX, rowBottom - 1));

            double flagTop = rowTop + 1;
            if (marker.Count == 1)
            {
                var flag = new StreamGeometry();
                using (var g = flag.Open())
                {
                    g.BeginFigure(new Point(poleX, flagTop), true);
                    g.LineTo(new Point(poleX + EventMarkers.MarkerPixels - 1, flagTop + FlagHeight / 2));
                    g.LineTo(new Point(poleX, flagTop + FlagHeight));
                    g.EndFigure(true);
                }
                context.DrawGeometry(ink, new Pen(Brush("BgCanvas"), 1), flag);
                DrawEventName(context, marker, markers, i, poleX, rowTop, trackRoomRight);
            }
            else
            {
                var count = Text(marker.Count.ToString(CultureInfo.InvariantCulture), 8, Brush("BgCanvas"), FontWeight.Bold);
                double width = Math.Max(EventMarkers.MarkerPixels, count.Width + 5);
                var pill = new Rect(poleX, rowTop + 1, width, TimelineLayout.EventRowHeight - 2);
                context.FillRectangle(ink, pill, 4);
                context.DrawText(count, new Point(pill.X + (pill.Width - count.Width) / 2, pill.Y + (pill.Height - count.Height) / 2));
            }
        }
    }

    // A lone flag with room before the next one (or the end of its block) says its name.
    private void DrawEventName(DrawingContext context, EventMarker marker, IReadOnlyList<EventMarker> markers, int index, double poleX, double rowTop, double roomRight)
    {
        string name = marker.Occurrences[0].Name;
        if (name.Length == 0) return;
        double textLeft = poleX + EventMarkers.MarkerPixels + NameGap;
        double limit = index + 1 < markers.Count ? Math.Min(roomRight, poleX - marker.X + markers[index + 1].X) : roomRight;
        double room = limit - textLeft - 2;
        if (room < 14) return;
        var text = Text(name, 9, Brush("InkMid"));
        if (text.Width > room) return;
        context.DrawText(text, new Point(textLeft, rowTop + (TimelineLayout.EventRowHeight - text.Height) / 2));
    }

    // The hover card for the flag under a point in a lane's event row, or null.
    private (FilmstripCard Card, (string Source, AnimationChainSave Chain) Source)? EventCardAt(Point point, double inLane)
    {
        if (inLane < TimelineLayout.EventRowTop || _document is not { } document || Sequence is not { } sequence) return null;
        if (TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y) is not { } hit
            || document.FindChain(hit.Block.Reference) is not { } chain) return null;
        var markers = EventMarkersOf(hit.Block);
        if (EventMarkers.MarkerAt(markers, point.X - Viewport.XAt(hit.Block.Start)) is not { } marker) return null;
        return (EventReadout.At(hit.Block.Reference.Chain, chain.Frames.Count, hit.Block.Start, marker), (hit.Block.Reference.Source, chain));
    }
}
