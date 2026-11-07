using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>A chain on the timeline: its track, and its index in that track's <see cref="SequenceTrackSave.Chains"/>.</summary>
public readonly record struct TimelineChainId(int Track, int Chain);

/// <summary>
/// A <c>.seqj</c> tab's timeline for one sequence: a ruler, a lane per track with its chains, and a dashed row that
/// makes a new track. Drag a chain to move it in time or to another lane; drag its right edge to change its loop
/// count; drop a library chip to add a chain. Each gesture is one undo entry, recorded on release. Ctrl+wheel zooms
/// around the pointer; the wheel scrolls (Shift+wheel always sideways). Pressing or dragging on the ruler scrubs: the
/// host moves its clock on <see cref="Scrubbed"/> and sets <see cref="Playhead"/> back. Geometry and hit-testing live in
/// <see cref="TimelineViewport"/> and <see cref="TimelineLayout"/>.
/// </summary>
public sealed class SequenceTimelineControl : Control, IZoomTarget
{
    private const double DragThresholdPixels = 3;
    private const double WheelZoomStep = 1.15;
    private const double WheelScrollPixels = 60;

    private enum DragKind { None, Pending, Move, Edge }

    private readonly ContextMenu _menu = new();
    private SequenceDocument? _document;
    private IUndoManager? _undo;
    private int _sequenceIndex;
    private IReadOnlyList<TimelineBlock> _blocks = Array.Empty<TimelineBlock>();

    private DragKind _drag;
    private TimelineHitPart _pressedPart;
    private Point _pressPoint;
    private SequenceChainReferenceSave? _dragged;
    private int _dragOriginTrack;
    private double _dragOriginStart;
    private double _dragOriginEdge;
    private TimelineBlock? _dragBlock;
    private string? _gestureBefore;

    private (LibraryChip Chip, Point Point)? _chipGhost;
    private double _playhead;
    private bool _scrubbing;

    public SequenceTimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;
        ContextMenu = _menu;
        _menu.Opening += (_, e) => e.Cancel = _menu.Items.Count == 0;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public TimelineViewport Viewport { get; } = new();

    public TimelineChainId? Selection { get; private set; }

    /// <summary>The context menu of the last right-click: a track header's, or a chain's.</summary>
    public ContextMenu Menu => _menu;

    /// <summary>The chains as drawn, for tests and the host.</summary>
    public IReadOnlyList<TimelineBlock> Blocks => _blocks;

    /// <summary>Raised when the user picks Rename on a track header, with the track index. The host prompts for the name.</summary>
    public event Action<int>? RenameTrackRequested;

    /// <summary>Raised while the ruler is pressed or dragged, with the time under the pointer clamped to the sequence.</summary>
    public event Action<double>? Scrubbed;

    /// <summary>Where the playhead line is drawn, in seconds.</summary>
    public double Playhead
    {
        get => _playhead;
        set
        {
            if (_playhead == value) return;
            _playhead = value;
            InvalidateVisual();
        }
    }

    /// <summary>Seconds until the shown sequence ends (<see cref="SequenceTimeline.Length"/>); 0 when nothing is shown.</summary>
    public double SequenceLength =>
        Sequence is { } sequence && _document is { } document
            ? SequenceTimeline.Length(sequence, reference => document.CycleLength(reference) ?? 0)
            : 0;

    // IZoomTarget: 100% is the default pixels per second.
    public float Zoom => (float)(Viewport.PixelsPerSecond / TimelineViewport.DefaultPixelsPerSecond);

    // The wheel zooms by a fixed factor, not through presets.
    public int[]? WheelZoomPresets { get; set; }

    public event Action<float>? ZoomChanged;

    public void SetZoomPercent(int percent)
    {
        Viewport.SetPixelsPerSecond(TimelineViewport.DefaultPixelsPerSecond * percent / 100.0);
        ZoomChanged?.Invoke(Zoom * 100f);
        InvalidateVisual();
    }

    private SequenceSave? Sequence =>
        _document is { } document && _sequenceIndex >= 0 && _sequenceIndex < document.Save.Sequences.Count
            ? document.Save.Sequences[_sequenceIndex]
            : null;

    private int TrackCount => Sequence?.Tracks.Count ?? 0;

    /// <summary>Shows sequence <paramref name="sequenceIndex"/> of <paramref name="document"/>, or nothing when null.</summary>
    public void Show(SequenceDocument? document, int sequenceIndex, IUndoManager? undo)
    {
        if (!ReferenceEquals(document, _document) || sequenceIndex != _sequenceIndex)
            Selection = null;
        _document = document;
        _sequenceIndex = sequenceIndex;
        _undo = undo;
        Rebuild();
    }

    /// <summary>Removes the selected chain. Returns false when no chain is selected.</summary>
    public bool DeleteSelected()
    {
        if (_document is null || _undo is null || Selection is not { } selection) return false;
        Selection = null;
        _document.DeleteChain(_undo, _sequenceIndex, selection.Track, selection.Chain);
        Rebuild();
        return true;
    }

    /// <summary>
    /// Draws where <paramref name="chip"/> would land if dropped at <paramref name="point"/> (this control's
    /// coordinates), or clears that when <paramref name="chip"/> or <paramref name="point"/> is null.
    /// </summary>
    public void UpdateChipDrag(LibraryChip? chip, Point? point)
    {
        _chipGhost = chip is not null && point is { } p && DropTarget(p) is not null ? (chip, p) : null;
        InvalidateVisual();
    }

    /// <summary>
    /// Adds <paramref name="chip"/>'s chain at <paramref name="point"/>: on a lane, at the time under the point; on the
    /// new-track row, as a new track. Returns false (and adds nothing) anywhere else.
    /// </summary>
    public bool DropChip(LibraryChip chip, Point point)
    {
        _chipGhost = null;
        InvalidateVisual();
        if (_document is null || _undo is null || DropTarget(point) is not { } target) return false;
        double start = TimelineLayout.SnapStart(Viewport.TimeAt(point.X));
        _document.PlaceChain(_undo, _sequenceIndex, target.Kind == TimelineRowKind.Track ? target.Track : null,
            chip.Source, chip.Chain.Name, start);
        var sequence = Sequence!;
        int track = target.Kind == TimelineRowKind.Track ? target.Track : sequence.Tracks.Count - 1;
        Selection = new TimelineChainId(track, sequence.Tracks[track].Chains.Count - 1);
        Rebuild();
        return true;
    }

    private TimelineRow? DropTarget(Point point) =>
        Sequence is null ? null : ChipDrop.TargetAt(Viewport, TrackCount, point.X, point.Y, Bounds.Width);

    /// <summary>
    /// What a chip drag should show at the mouse cursor at <paramref name="point"/> (this control's
    /// coordinates, null when the pointer is outside it): the same target <see cref="DropChip"/> would use.
    /// </summary>
    public ChipDragFeedback ChipDragFeedbackAt(Point? point)
    {
        var target = point is { } p ? DropTarget(p) : null;
        string? trackName = target is { Kind: TimelineRowKind.Track } row ? Sequence!.Tracks[row.Track].Name : null;
        return ChipDragFeedback.For(target, trackName);
    }

    private void Rebuild()
    {
        _blocks = Sequence is { } sequence && _document is { } document
            ? TimelineLayout.Blocks(sequence, document.CycleLength)
            : Array.Empty<TimelineBlock>();
        if (Selection is { } selection && !_blocks.Any(b => b.Track == selection.Track && b.Chain == selection.Chain))
            Selection = null;
        Viewport.ClampVertical(TrackCount, Bounds.Height);
        InvalidateVisual();
    }

    // ── Pointer ──────────────────────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_document is null || Sequence is not { } sequence) return;
        Focus();
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var row = Viewport.RowAt(point.Y, sequence.Tracks.Count);

        if (properties.IsRightButtonPressed)
        {
            BuildMenu(point, row);
            return;
        }
        if (!properties.IsLeftButtonPressed) return;
        e.Handled = true;

        if (row.Kind == TimelineRowKind.Ruler && point.X >= TimelineViewport.HeaderWidth)
        {
            _scrubbing = true;
            e.Pointer.Capture(this);
            Scrubbed?.Invoke(Viewport.ScrubTime(point.X, SequenceLength));
            return;
        }

        var hit = point.X >= TimelineViewport.HeaderWidth
            ? TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y)
            : null;
        if (hit is not { } found)
        {
            Selection = null;
            InvalidateVisual();
            return;
        }

        var block = found.Block;
        Selection = new TimelineChainId(block.Track, block.Chain);
        _drag = DragKind.Pending;
        _pressedPart = found.Part;
        _pressPoint = point;
        _dragged = block.Reference;
        _dragBlock = block;
        _dragOriginTrack = block.Track;
        _dragOriginStart = block.Start;
        _dragOriginEdge = block.FullEnd;
        _gestureBefore = _document.BeginGesture();
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_scrubbing)
        {
            Scrubbed?.Invoke(Viewport.ScrubTime(e.GetPosition(this).X, SequenceLength));
            return;
        }
        if (_drag == DragKind.None || Sequence is not { } sequence || _dragged is null || _dragBlock is null) return;
        var point = e.GetPosition(this);
        if (_drag == DragKind.Pending)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThresholdPixels && Math.Abs(point.Y - _pressPoint.Y) < DragThresholdPixels) return;
            _drag = _pressedPart == TimelineHitPart.Edge ? DragKind.Edge : DragKind.Move;
        }

        double dt = (point.X - _pressPoint.X) / Viewport.PixelsPerSecond;
        if (_drag == DragKind.Move)
        {
            _dragged.Start = TimelineLayout.SnapStart(_dragOriginStart + dt);
            var row = Viewport.RowAt(point.Y, sequence.Tracks.Count);
            int current = sequence.Tracks.FindIndex(t => t.Chains.Contains(_dragged));
            if (row.Kind == TimelineRowKind.Track && row.Track != current)
            {
                sequence.Tracks[current].Chains.Remove(_dragged);
                sequence.Tracks[row.Track].Chains.Add(_dragged);
                current = row.Track;
            }
            Selection = new TimelineChainId(current, sequence.Tracks[current].Chains.IndexOf(_dragged));
        }
        else
        {
            var track = sequence.Tracks[_dragOriginTrack];
            _dragged.Loops = TimelineLayout.EdgeDragLoops(_dragBlock, _dragOriginEdge + dt, Viewport.PixelsPerSecond,
                SequenceTimeline.NextStart(track, _dragged));
        }
        Rebuild();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_scrubbing)
        {
            _scrubbing = false;
            e.Pointer.Capture(null);
            return;
        }
        if (_drag == DragKind.None) return;
        e.Pointer.Capture(null);
        EndDrag();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _scrubbing = false;
        EndDrag();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || _drag == DragKind.None || _document is null || _gestureBefore is null) return;
        // Escape puts the dragged chain back with no undo entry.
        string before = _gestureBefore;
        ResetDrag();
        _document.CancelGesture(before);
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (_drag == DragKind.None) return;
        var kind = _drag;
        var dragged = _dragged;
        int originTrack = _dragOriginTrack;
        string? before = _gestureBefore;
        ResetDrag();
        if (kind == DragKind.Pending || dragged is null || before is null || _document is null || _undo is null
            || Sequence is not { } sequence)
            return;

        string description;
        if (kind == DragKind.Edge)
        {
            description = $"Set Loops of '{dragged.Chain}' to {dragged.Loops.ToString("0.##", CultureInfo.InvariantCulture)}";
        }
        else
        {
            var track = sequence.Tracks.First(t => t.Chains.Contains(dragged));
            description = sequence.Tracks.IndexOf(track) == originTrack
                ? $"Move '{dragged.Chain}'"
                : $"Move '{dragged.Chain}' to '{track.Name}'";
        }
        _document.CommitGesture(_undo, description, before);
    }

    private void ResetDrag()
    {
        _drag = DragKind.None;
        _dragged = null;
        _dragBlock = null;
        _gestureBefore = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Sequence is null) return;
        var point = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            Viewport.ZoomAround(Math.Max(point.X, TimelineViewport.HeaderWidth), Math.Pow(WheelZoomStep, e.Delta.Y));
            ZoomChanged?.Invoke(Zoom * 100f);
        }
        else if (e.Delta.X != 0 || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            double delta = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
            Viewport.ScrollHorizontally(-delta * WheelScrollPixels);
        }
        else if (TimelineViewport.ContentHeight(TrackCount) > Bounds.Height)
        {
            Viewport.ScrollVertically(-e.Delta.Y * TimelineViewport.LaneHeight, TrackCount, Bounds.Height);
        }
        else
        {
            Viewport.ScrollHorizontally(-e.Delta.Y * WheelScrollPixels);
        }
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Viewport.ClampVertical(TrackCount, e.NewSize.Height);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private void BuildMenu(Point point, TimelineRow row)
    {
        _menu.Items.Clear();
        if (_document is null || _undo is null || row.Kind != TimelineRowKind.Track) return;
        int track = row.Track;
        if (point.X < TimelineViewport.HeaderWidth)
        {
            var rename = new MenuItem { Header = "Rename Track…" };
            rename.Click += (_, _) => RenameTrackRequested?.Invoke(track);
            var delete = new MenuItem { Header = "Delete Track" };
            delete.Click += (_, _) =>
            {
                Selection = null;
                _document.DeleteTrack(_undo, _sequenceIndex, track);
            };
            _menu.Items.Add(rename);
            _menu.Items.Add(delete);
            return;
        }
        if (TimelineLayout.HitTest(Viewport, _blocks, TrackCount, point.X, point.Y) is { } hit)
        {
            Selection = new TimelineChainId(hit.Block.Track, hit.Block.Chain);
            InvalidateVisual();
            var remove = new MenuItem { Header = "Remove from Track" };
            remove.Click += (_, _) => DeleteSelected();
            _menu.Items.Add(remove);
        }
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brush("BgCanvas"), new Rect(size));
        if (Sequence is not { } sequence) return;

        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        double laneHeight = TimelineViewport.LaneHeight;
        var line = new Pen(Brush("LineBrush"), 1);
        var lanes = new Rect(header, ruler, Math.Max(0, size.Width - header), Math.Max(0, size.Height - ruler));
        double step = TickStep(Viewport.PixelsPerSecond);
        double firstTick = Math.Floor(Viewport.ScrollSeconds / step) * step;

        using (context.PushClip(lanes))
        {
            for (int lane = 0; lane < sequence.Tracks.Count; lane++)
            {
                double top = Viewport.LaneTop(lane);
                if (lane % 2 == 1)
                    context.FillRectangle(Brush("TreeRowStripe"), new Rect(header, top, lanes.Width, laneHeight));
                context.DrawLine(line, new Point(header, top + laneHeight - 0.5), new Point(size.Width, top + laneHeight - 0.5));
            }
            for (double t = firstTick; Viewport.XAt(t) < size.Width; t += step)
                context.DrawLine(line, new Point(Snap(Viewport.XAt(t)), ruler), new Point(Snap(Viewport.XAt(t)), size.Height));

            foreach (var block in _blocks)
                DrawBlock(context, block, Selection == new TimelineChainId(block.Track, block.Chain));
            DrawNewTrackRow(context, sequence, lanes);
            DrawChipGhost(context);
        }

        DrawHeaders(context, sequence, size);
        DrawRuler(context, size, step, firstTick);
        DrawPlayhead(context, size);
    }

    private void DrawPlayhead(DrawingContext context, Size size)
    {
        double x = Snap(Viewport.XAt(_playhead));
        if (x < TimelineViewport.HeaderWidth || x > size.Width) return;
        var brush = Brush("Accent");
        context.DrawLine(new Pen(brush, 2), new Point(x, 0), new Point(x, size.Height));
        var head = new StreamGeometry();
        using (var g = head.Open())
        {
            g.BeginFigure(new Point(x - 6, 0), true);
            g.LineTo(new Point(x + 6, 0));
            g.LineTo(new Point(x, 8));
            g.EndFigure(true);
        }
        context.DrawGeometry(brush, null, head);
    }

    private void DrawHeaders(DrawingContext context, SequenceSave sequence, Size size)
    {
        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        var area = new Rect(0, ruler, header, Math.Max(0, size.Height - ruler));
        using (context.PushClip(area))
        {
            context.FillRectangle(Brush("BgPanel"), area);
            for (int lane = 0; lane < sequence.Tracks.Count; lane++)
            {
                double top = Viewport.LaneTop(lane);
                var text = Text(sequence.Tracks[lane].Name, 12, Brush("Ink"), FontWeight.SemiBold);
                text.MaxTextWidth = header - 16;
                text.Trimming = TextTrimming.CharacterEllipsis;
                text.MaxLineCount = 1;
                context.DrawText(text, new Point(8, top + (TimelineViewport.LaneHeight - text.Height) / 2));
                context.DrawLine(new Pen(Brush("LineBrush"), 1), new Point(0, top + TimelineViewport.LaneHeight - 0.5),
                    new Point(header, top + TimelineViewport.LaneHeight - 0.5));
            }
            var newTrack = Text("+ new track", 11, Brush("InkMid"));
            context.DrawText(newTrack, new Point(8, Viewport.LaneTop(sequence.Tracks.Count) + (TimelineViewport.LaneHeight - newTrack.Height) / 2));
        }
        context.DrawLine(new Pen(Brush("LineStrong"), 1), new Point(header - 0.5, 0), new Point(header - 0.5, size.Height));
    }

    private void DrawRuler(DrawingContext context, Size size, double step, double firstTick)
    {
        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        context.FillRectangle(Brush("BgRail"), new Rect(0, 0, size.Width, ruler));
        using (context.PushClip(new Rect(header, 0, Math.Max(0, size.Width - header), ruler)))
        {
            var tick = new Pen(Brush("LineStrong"), 1);
            for (double t = firstTick; Viewport.XAt(t) < size.Width; t += step)
            {
                double x = Snap(Viewport.XAt(t));
                context.DrawLine(tick, new Point(x, ruler - 7), new Point(x, ruler));
                context.DrawText(Text(FormatTick(t, step), 10, Brush("InkMid")), new Point(x + 3, 3));
            }
        }
        context.DrawLine(new Pen(Brush("LineStrong"), 1), new Point(0, ruler - 0.5), new Point(size.Width, ruler - 0.5));
    }

    private void DrawBlock(DrawingContext context, TimelineBlock block, bool selected)
    {
        double top = Viewport.LaneTop(block.Track);
        double left = Viewport.XAt(block.Start);
        double pps = Viewport.PixelsPerSecond;
        var body = new Rect(left, top + TimelineLayout.BodyTop, Math.Max(1, block.Played * pps),
            TimelineLayout.BodyBottom - TimelineLayout.BodyTop);

        if (block.IsCut)
        {
            var tail = new Rect(left + block.Played * pps, top + TimelineLayout.TailTop, (block.Full - block.Played) * pps,
                TimelineLayout.TailBottom - TimelineLayout.TailTop);
            DrawHatched(context, tail, Brush("InkMid"), 0.75);
        }

        string colorKey = block.IsMissing ? "Accent" : "AccentCool";
        var color = Brush(colorKey) is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;
        if (block.IsMissing)
        {
            context.FillRectangle(new SolidColorBrush(color, 0.15), body, 3);
            DrawHatched(context, body, Brush(colorKey), 0.4);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(color, 0.35), body, 3);
        }
        context.DrawRectangle(null, new Pen(selected ? Brush("Ink") : new SolidColorBrush(color), selected ? 2 : 1), body, 3, 3);

        // Whole-cycle marks, so a float loop count reads at a glance.
        if (block.CycleLength > 0)
        {
            var mark = new Pen(new SolidColorBrush(color, 0.8), 1, new DashStyle(new double[] { 2, 2 }, 0));
            for (int k = 1; k * block.CycleLength < block.Played - 1e-9; k++)
            {
                double x = Snap(left + k * block.CycleLength * pps);
                context.DrawLine(mark, new Point(x, body.Top + 2), new Point(x, body.Bottom - 2));
            }
        }

        string label = block.IsMissing ? $"⚠ {block.Reference.Chain} (missing)" : block.Reference.Chain;
        if (!block.IsMissing && Math.Abs(block.Reference.Loops - 1) > 1e-6)
            label += $"  ×{block.Reference.Loops.ToString("0.##", CultureInfo.InvariantCulture)}";
        using (context.PushClip(body.Deflate(new Thickness(5, 0))))
        {
            var text = Text(label, 11, Brush("Ink"));
            context.DrawText(text, new Point(body.X + 6, body.Y + (body.Height - text.Height) / 2));
        }
    }

    private void DrawNewTrackRow(DrawingContext context, SequenceSave sequence, Rect lanes)
    {
        double top = Viewport.LaneTop(sequence.Tracks.Count);
        var row = new Rect(lanes.X + 4, top + 4, Math.Max(0, lanes.Width - 8), TimelineViewport.LaneHeight - 8);
        bool target = _chipGhost is { } ghost && Viewport.RowAt(ghost.Point.Y, sequence.Tracks.Count).Kind == TimelineRowKind.NewTrack;
        var brush = Brush(target ? "AccentCool" : "LineStrong");
        context.DrawRectangle(null, new Pen(brush, 1, new DashStyle(new double[] { 4, 3 }, 0)), row, 4, 4);
        string hint = sequence.Tracks.Count == 0
            ? "Empty sequence. Drag a chain from the library here to start."
            : "Drop a chain here to make a new track";
        var text = Text(hint, 11, Brush(target ? "AccentCool" : "InkMid"));
        context.DrawText(text, new Point(row.X + 10, row.Y + (row.Height - text.Height) / 2));
    }

    private void DrawChipGhost(DrawingContext context)
    {
        if (_chipGhost is not { } ghost || Sequence is not { } sequence) return;
        var row = Viewport.RowAt(ghost.Point.Y, sequence.Tracks.Count);
        if (row.Kind != TimelineRowKind.Track) return;
        double start = TimelineLayout.SnapStart(Viewport.TimeAt(ghost.Point.X));
        double cycle = ChainClock.TotalLength(ghost.Chip.Chain);
        double length = cycle > 0 ? cycle : TimelineLayout.MissingLength;
        double top = Viewport.LaneTop(row.Track);
        var rect = new Rect(Viewport.XAt(start), top + TimelineLayout.BodyTop, length * Viewport.PixelsPerSecond,
            TimelineLayout.BodyBottom - TimelineLayout.BodyTop);
        context.DrawRectangle(null, new Pen(Brush("AccentCool"), 2, new DashStyle(new double[] { 4, 3 }, 0)), rect, 3, 3);
        using (context.PushClip(rect))
            context.DrawText(Text(ghost.Chip.Chain.Name, 11, Brush("AccentCool")), new Point(rect.X + 6, rect.Y + 4));
    }

    private static void DrawHatched(DrawingContext context, Rect rect, IBrush brush, double opacity)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var pen = new Pen(brush, 1) { };
        using (context.PushOpacity(opacity))
        using (context.PushClip(rect))
        {
            for (double x = rect.Left - rect.Height; x < rect.Right; x += 5)
                context.DrawLine(pen, new Point(x, rect.Bottom), new Point(x + rect.Height, rect.Top));
        }
    }

    /// <summary>Seconds between ruler ticks: the smallest step that leaves at least 60 pixels between labels.</summary>
    internal static double TickStep(double pixelsPerSecond)
    {
        double[] steps = { 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60 };
        return steps.FirstOrDefault(step => step * pixelsPerSecond >= 60, 120);
    }

    private static string FormatTick(double time, double step) =>
        (step < 1 ? time.ToString("0.##", CultureInfo.InvariantCulture) : Math.Round(time).ToString(CultureInfo.InvariantCulture)) + "s";

    private static double Snap(double x) => Math.Round(x) + 0.5;

    // The theme tokens live in ThemeDictionaries, which a lookup without the theme variant never searches.
    private IBrush Brush(string key) =>
        (this.TryFindResource(key, ActualThemeVariant, out var value) ? value : null) switch
        {
            IBrush brush => brush,
            Color color => new SolidColorBrush(color),
            _ => Brushes.Gray,
        };

    private static FormattedText Text(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);
}
