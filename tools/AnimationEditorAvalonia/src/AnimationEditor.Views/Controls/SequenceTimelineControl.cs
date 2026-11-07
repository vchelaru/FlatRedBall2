using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>What the timeline has selected: a track, and optionally one chain reference on it (index into its list).</summary>
public readonly record struct SequenceSelection(int TrackIndex, int ChainIndex);

/// <summary>
/// The sequence timeline: a ruler, one lane per track with its chains, and a dashed "new track" row. Click selects, drag
/// moves a chain, dragging its right edge changes the loop count (free, magnet at whole cycles, stops at the next chain),
/// clicking the ruler scrubs. The cut part of a chain is drawn hatched. Spike: drawn by hand, no scrolling.
/// </summary>
public sealed class SequenceTimelineControl : Control
{
    public const double HeaderWidth = 96;
    public const double RulerHeight = 22;
    public const double LaneHeight = 34;
    private const double EdgeGrabPixels = 6;
    private const double MagnetPixels = 8;

    private enum DragKind { None, Scrub, Move, Edge }

    private DragKind _drag;
    private double _dragStartX;
    private double _dragOriginalStart;
    private double _dragOriginalEdge;
    private string? _dragBefore;

    public SequenceDocument? Document { get; set; }
    public IUndoManager? UndoManager { get; set; }
    public int SequenceIndex { get; set; }
    public double PixelsPerSecond { get; set; } = 120;
    public SequenceSelection? Selection { get; private set; }

    private double _playhead;
    public double Playhead
    {
        get => _playhead;
        set { _playhead = value; InvalidateVisual(); }
    }

    public event Action<SequenceSelection?>? SelectionChanged;
    public event Action<double>? ScrubRequested;

    private SequenceSave? Sequence =>
        Document is { } d && SequenceIndex >= 0 && SequenceIndex < d.Save.Sequences.Count ? d.Save.Sequences[SequenceIndex] : null;

    public double TimeAt(double x) => Math.Max(0, (x - HeaderWidth) / PixelsPerSecond);
    public double XAt(double time) => HeaderWidth + time * PixelsPerSecond;

    /// <summary>Track index under <paramref name="y"/>; the track count means the "new track" row; -1 is the ruler or below.</summary>
    public int LaneAt(double y)
    {
        if (y < RulerHeight || Sequence is not { } sequence) return -1;
        int lane = (int)((y - RulerHeight) / LaneHeight);
        return lane <= sequence.Tracks.Count ? lane : -1;
    }

    public double LaneTop(int lane) => RulerHeight + lane * LaneHeight;

    /// <summary>Drops a library chip at <paramref name="point"/>: onto a lane, or onto the new-track row. Returns false off the lanes.</summary>
    public bool DropChain(Point point, string source, string chain)
    {
        if (Document is null || UndoManager is null || Sequence is null) return false;
        int lane = LaneAt(point.Y);
        if (lane < 0 || point.X < HeaderWidth) return false;
        Document.PlaceChain(UndoManager, SequenceIndex, lane, source, chain, TimeAt(point.X));
        var track = Sequence.Tracks[Math.Min(lane, Sequence.Tracks.Count - 1)];
        Select(new SequenceSelection(Sequence.Tracks.IndexOf(track), track.Chains.Count - 1));
        return true;
    }

    public void Select(SequenceSelection? selection)
    {
        Selection = selection;
        InvalidateVisual();
        SelectionChanged?.Invoke(selection);
    }

    /// <summary>The chain under <paramref name="point"/>, and whether the point is on its right edge.</summary>
    public (int Track, int Chain, bool Edge)? HitChain(Point point)
    {
        if (Document is null || Sequence is not { } sequence) return null;
        int lane = LaneAt(point.Y);
        if (lane < 0 || lane >= sequence.Tracks.Count) return null;
        var track = sequence.Tracks[lane];
        // Later starts draw on top, so test them first.
        foreach (var reference in SequenceTimeline.Ordered(track).Reverse())
        {
            double left = XAt(reference.Start);
            double right = XAt(reference.Start + SequenceTimeline.PlayedLength(track, reference, Document.DisplayCycleLength(reference)));
            if (point.X >= left && point.X <= right + EdgeGrabPixels)
                return (lane, track.Chains.IndexOf(reference), point.X >= right - EdgeGrabPixels);
        }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Document is null || Sequence is not { } sequence) return;
        e.Handled = true;
        e.Pointer.Capture(this);

        if (point.Y < RulerHeight)
        {
            _drag = DragKind.Scrub;
            ScrubRequested?.Invoke(TimeAt(point.X));
            return;
        }
        if (HitChain(point) is { } hit)
        {
            Select(new SequenceSelection(hit.Track, hit.Chain));
            _drag = hit.Edge ? DragKind.Edge : DragKind.Move;
            _dragStartX = point.X;
            var grabbed = sequence.Tracks[hit.Track].Chains[hit.Chain];
            _dragOriginalStart = grabbed.Start;
            // Relative to the grab, so grabbing a few pixels off the edge doesn't jump it.
            _dragOriginalEdge = grabbed.Start + SequenceTimeline.PlayedLength(sequence.Tracks[hit.Track], grabbed, Document.DisplayCycleLength(grabbed));
            _dragBefore = Document.Capture();
            return;
        }
        int lane = LaneAt(point.Y);
        Select(lane >= 0 && lane < sequence.Tracks.Count ? new SequenceSelection(lane, -1) : null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == DragKind.None || Document is null || Sequence is not { } sequence) return;
        var point = e.GetPosition(this);
        if (_drag == DragKind.Scrub)
        {
            ScrubRequested?.Invoke(TimeAt(point.X));
            return;
        }
        if (Selection is not { ChainIndex: >= 0 } selection) return;
        var track = sequence.Tracks[selection.TrackIndex];
        var reference = track.Chains[selection.ChainIndex];
        if (_drag == DragKind.Move)
            reference.Start = Math.Max(0, _dragOriginalStart + (point.X - _dragStartX) / PixelsPerSecond);
        else
            reference.Loops = SequenceTimeline.LoopsForEdgeDrag(reference.Start, _dragOriginalEdge + (point.X - _dragStartX) / PixelsPerSecond,
                Document.DisplayCycleLength(reference), SequenceTimeline.NextStart(track, reference), MagnetPixels / PixelsPerSecond);
        InvalidateVisual();
        SelectionChanged?.Invoke(Selection);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is DragKind.Move or DragKind.Edge && _dragBefore is not null && Document is not null && UndoManager is not null
            && Selection is { ChainIndex: >= 0 } selection && Sequence is { } sequence)
        {
            string name = sequence.Tracks[selection.TrackIndex].Chains[selection.ChainIndex].Chain;
            Document.Commit(UndoManager, _drag == DragKind.Move ? $"Move '{name}'" : $"Set Loops '{name}'", _dragBefore);
        }
        _drag = DragKind.None;
        _dragBefore = null;
        e.Pointer.Capture(null);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int lanes = (Sequence?.Tracks.Count ?? 0) + 1;
        return new Size(0, RulerHeight + lanes * LaneHeight + 4);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x16, 0x17, 0x1a)), new Rect(bounds.Size));
        if (Document is null || Sequence is not { } sequence) return;

        var dim = new SolidColorBrush(Color.FromRgb(0x8b, 0x90, 0x9a));
        var line = new Pen(new SolidColorBrush(Color.FromRgb(0x36, 0x39, 0x40)), 1);
        var typeface = new Typeface("Segoe UI");

        // Ruler
        for (int s = 0; XAt(s) < bounds.Width; s++)
        {
            double x = XAt(s);
            context.DrawLine(line, new Point(x, RulerHeight - 6), new Point(x, RulerHeight));
            context.DrawText(Text($"{s}s", 10, dim, typeface), new Point(x + 2, 3));
        }
        context.DrawLine(line, new Point(0, RulerHeight), new Point(bounds.Width, RulerHeight));

        for (int lane = 0; lane < sequence.Tracks.Count; lane++)
        {
            var track = sequence.Tracks[lane];
            double top = LaneTop(lane);
            bool trackSelected = Selection?.TrackIndex == lane;
            context.FillRectangle(new SolidColorBrush(lane % 2 == 0 ? Color.FromRgb(0x1b, 0x1d, 0x20) : Color.FromRgb(0x22, 0x24, 0x28)),
                new Rect(0, top, bounds.Width, LaneHeight));
            context.DrawText(Text(track.Name, 12, trackSelected ? Brushes.White : dim, typeface), new Point(6, top + 9));
            foreach (var reference in SequenceTimeline.Ordered(track))
                DrawChain(context, track, reference, top, typeface, Selection == new SequenceSelection(lane, track.Chains.IndexOf(reference)));
        }

        double newTop = LaneTop(sequence.Tracks.Count);
        var dashed = new Pen(dim, 1, new DashStyle(new double[] { 4, 3 }, 0));
        context.DrawRectangle(null, dashed, new Rect(HeaderWidth + 2, newTop + 4, Math.Max(0, bounds.Width - HeaderWidth - 6), LaneHeight - 8));
        context.DrawText(Text("+ drop a chain here for a new track", 11, dim, typeface), new Point(HeaderWidth + 10, newTop + 10));

        double playX = XAt(Playhead);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xd9, 0x41, 0x41)), 1.5), new Point(playX, 0), new Point(playX, bounds.Height));
    }

    private void DrawChain(DrawingContext context, SequenceTrackSave track, SequenceChainReferenceSave reference, double top,
        Typeface typeface, bool selected)
    {
        double cycle = Document!.DisplayCycleLength(reference);
        double full = SequenceTimeline.FullLength(reference.Loops, cycle);
        double played = SequenceTimeline.PlayedLength(track, reference, cycle);
        bool missing = Document.FindChain(reference) is null;
        double left = XAt(reference.Start);
        var body = new Rect(left, top + 3, played * PixelsPerSecond, LaneHeight - 13);

        // Cut tail: a hatched strip below the bodies, so it stays visible under the chain that interrupts it.
        if (full > played + 1e-9)
        {
            var tail = new Rect(left + played * PixelsPerSecond, top + LaneHeight - 9, (full - played) * PixelsPerSecond, 7);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(150, 0x8b, 0x90, 0x9a)), 1), tail);
            using (context.PushClip(tail))
            {
                var hatch = new Pen(new SolidColorBrush(Color.FromArgb(170, 0x8b, 0x90, 0x9a)), 1);
                for (double x = tail.Left - tail.Height; x < tail.Right; x += 5)
                    context.DrawLine(hatch, new Point(x, tail.Bottom), new Point(x + tail.Height, tail.Top));
            }
        }

        var fill = missing ? Color.FromRgb(0x5a, 0x22, 0x22) : Color.FromRgb(0x2f, 0x4f, 0x80);
        context.DrawRectangle(new SolidColorBrush(fill),
            new Pen(selected ? Brushes.White : new SolidColorBrush(missing ? Color.FromRgb(0xd9, 0x41, 0x41) : Color.FromRgb(0x4a, 0x8c, 0xff)), selected ? 2 : 1),
            body, 3, 3);

        // Whole-cycle ticks, so a float loop count reads at a glance.
        var tick = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1);
        for (int k = 1; k * cycle < played - 1e-9; k++)
            context.DrawLine(tick, new Point(left + k * cycle * PixelsPerSecond, body.Top + 2), new Point(left + k * cycle * PixelsPerSecond, body.Bottom - 2));

        using (context.PushClip(body))
            context.DrawText(Text(missing ? $"! {reference.Chain} (missing)" : reference.Chain, 11, Brushes.White, typeface), new Point(left + 5, top + 6));
    }

    private static FormattedText Text(string text, double size, IBrush brush, Typeface typeface) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush);
}
