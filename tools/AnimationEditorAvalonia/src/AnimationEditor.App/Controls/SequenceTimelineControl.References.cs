using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Reference tracks on the timeline: a track that plays another sequence of the file. Its lane draws one purple clip (a link
/// icon and the sequence's name) that drags and resizes like a chain block and lengthens the sequence like one; the header has a
/// chevron that expands the lane to list the inherited tracks, read-only, as dashed bars. A sequence dragged from the list
/// drops onto the track area as a new reference at the playhead. The rules live in <see cref="SequenceReferences"/> and
/// <see cref="TimelineReferences"/>.
/// </summary>
public sealed partial class SequenceTimelineControl
{
    // The header of a reference track: chevron, link icon, then the name.
    private const double ChevronCenterX = 30;
    private const double LinkCenterX = 46;
    private const double ReferenceNameLeft = 58;
    private const double InheritedNameLeft = 46;

    private static readonly Geometry LinkGlyph = Geometry.Parse(
        "M9 15l6 -6 M11 6l.463 -.536a5 5 0 0 1 7.071 7.072l-.534 .464 M13 18l-.397 .534a5.068 5.068 0 0 1 -7.127 0a4.972 4.972 0 0 1 0 -7.071l.524 -.463");

    private IReadOnlyList<TimelineReference> _references = Array.Empty<TimelineReference>();

    // Expanded lanes, by sequence and track name. View state: not saved, not an undo entry.
    private readonly HashSet<string> _expandedReferences = new();

    private SequenceReferenceSave? _dragReference;
    private int _dragReferenceTrack;
    private double _dragReferenceCycle;

    // A sequence dragged from the list, with where the pointer is: the lanes show where it would land.
    private (string Name, Point Point)? _sequenceGhost;

    /// <summary>The reference tracks of the shown sequence as drawn.</summary>
    public IReadOnlyList<TimelineReference> References => _references;

    /// <summary>Whether reference track <paramref name="track"/> lists its inherited tracks. False for any other track.</summary>
    public bool IsReferenceExpanded(int track) =>
        Sequence is { } sequence && track >= 0 && track < sequence.Tracks.Count && sequence.Tracks[track].References is not null
        && _expandedReferences.Contains(ExpandedKey(sequence, track));

    /// <summary>Expands or collapses reference track <paramref name="track"/>. Nothing happens for a track that is not a reference or inherits nothing.</summary>
    public void ToggleReferenceExpanded(int track)
    {
        if (Sequence is not { } sequence || _references.FirstOrDefault(r => r.Track == track) is not { Expandable: true }) return;
        string key = ExpandedKey(sequence, track);
        if (!_expandedReferences.Remove(key)) _expandedReferences.Add(key);
        Rebuild();
    }

    private string ExpandedKey(SequenceSave sequence, int track) => $"{sequence.Name}\n{sequence.Tracks[track].Name}";

    private bool IsExpanded(TimelineReference reference) => Sequence is { } sequence && IsReferenceExpanded(reference.Track) && reference.Expandable;

    private void RebuildReferences()
    {
        _references = Sequence is { } sequence && _document is { } document
            ? TimelineReferences.For(sequence, document.Save.Sequences, document.CycleLength)
            : Array.Empty<TimelineReference>();
    }

    private TimelineReference? ReferenceOn(int track) => _references.FirstOrDefault(r => r.Track == track);

    private static bool IsOverChevron(double x, double yInLane) =>
        Math.Abs(x - ChevronCenterX) <= 8 && Math.Abs(yInLane - TimelineLayout.HeaderCenterY) <= 9;

    private Color RefColor() => Brush("RefPurple") is ISolidColorBrush solid ? solid.Color : Colors.MediumPurple;

    // ── Adding ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a track that plays sequence <paramref name="name"/> at the playhead with one cycle, in gap <paramref name="slot"/>
    /// (0, the default, is in front), and selects it. One undo entry. Returns why it was refused (it would play itself through a
    /// cycle), or null.
    /// </summary>
    public string? AddSequenceReference(string name, int slot = 0)
    {
        if (_document is null || _undo is null || Sequence is null) return "Open a sequence first.";
        if (_document.AddSequenceReference(_undo, _sequenceIndex, name, _playhead, slot) is { } error) return error;
        int added = Math.Clamp(slot, 0, Sequence.Tracks.Count - 1);
        SetSelection(null, added);
        Rebuild();
        if (slot == 0 && Viewport.ScrollY > 0) ScrollToY(0);
        return null;
    }

    /// <summary>
    /// Appends a clip playing sequence <paramref name="name"/> to reference track <paramref name="track"/> from
    /// <paramref name="start"/> seconds. One undo entry. Returns why it was refused (it would play itself through a cycle), or null.
    /// </summary>
    public string? AddReferenceClip(string name, int track, double start)
    {
        if (_document is null || _undo is null || Sequence is null) return "Open a sequence first.";
        if (_document.AddReferenceClip(_undo, _sequenceIndex, track, name, start) is { } error) return error;
        SetSelection(null, track);
        Rebuild();
        return null;
    }

    /// <summary>The sequences the right-click menu offers to add as a reference: all but this one and any that would close a cycle.</summary>
    public IReadOnlyList<string> ReferenceCandidates =>
        _document is { } document && Sequence is not null ? document.ReferenceCandidates(_sequenceIndex) : Array.Empty<string>();

    /// <summary>
    /// Whether a sequence called <paramref name="name"/> dropped at <paramref name="point"/> (this control's coordinates) would add a
    /// reference: anywhere under the ruler, as long as it would not make the sequences play each other in a loop.
    /// </summary>
    // The reference track whose clip band is under the point, or null: a sequence dropped there is appended to it as a clip.
    private int? ReferenceLaneAt(Point point)
    {
        if (Sequence is not { } sequence) return null;
        var row = Viewport.RowAt(point.Y, sequence.Tracks.Count);
        if (row.Kind != TimelineRowKind.Track || row.Track < 0 || row.Track >= sequence.Tracks.Count || sequence.Tracks[row.Track].References is null) return null;
        double inLane = point.Y - Viewport.LaneTop(row.Track);
        return inLane >= TimelineLayout.BodyTop && inLane <= TimelineLayout.BodyBottom ? row.Track : null;
    }

    /// <summary>The reference track a sequence dragged from the list would be appended to as a clip, or null (a gap or the new-track row adds a track).</summary>
    public int? SequenceDropClipTrack => _sequenceGhost is { } ghost ? ReferenceLaneAt(ghost.Point) : null;

    public bool CanDropSequence(string name, Point point) =>
        _document is { } document && Sequence is not null && point.Y >= TimelineViewport.RulerHeight
        && new Rect(Bounds.Size).Contains(point) && document.CanReference(_sequenceIndex, name);

    /// <summary>
    /// Shows where a sequence dragged from the list would land, or clears that when <paramref name="name"/> or
    /// <paramref name="point"/> is null. Nothing shows for a drop that is refused.
    /// </summary>
    public void UpdateSequenceDrag(string? name, Point? point)
    {
        ClearHover();
        var ghost = name is not null && point is { } p && CanDropSequence(name, p) ? (name, p) : ((string, Point)?)null;
        if (ghost == _sequenceGhost) return;
        _sequenceGhost = ghost;
        InvalidateVisual();
    }

    /// <summary>True while a sequence dragged from the list is over a place it could be dropped.</summary>
    public bool IsSequenceDropTarget => _sequenceGhost is not null;

    /// <summary>The gap between lanes a sequence dragged from the list would be inserted into, or null when it makes the front track on the new-track row or is not over a drop.</summary>
    public int? SequenceDropSlot =>
        _sequenceGhost is { } ghost && ReferenceLaneAt(ghost.Point) is null && TimelineDropPlan.ForSequence(Viewport, TrackCount, ghost.Point.Y) is { Kind: TimelineDropKind.Gap } plan ? plan.Row : null;

    /// <summary>The y of the insertion line for <see cref="SequenceDropSlot"/>, in this control's coordinates.</summary>
    public double? SequenceDropLineY => SequenceDropSlot is { } slot ? TrackReorder.SlotY(Viewport, slot) : null;

    /// <summary>
    /// Drops sequence <paramref name="name"/> at <paramref name="point"/>: a new reference at the playhead, in front on the new-track
    /// row or in the gap between lanes nearest the point. False (nothing added) where <see cref="CanDropSequence"/> says no.
    /// </summary>
    public bool DropSequence(string name, Point point)
    {
        _sequenceGhost = null;
        InvalidateVisual();
        if (!CanDropSequence(name, point)) return false;
        if (ReferenceLaneAt(point) is { } lane)
        {
            if (AddReferenceClip(name, lane, Math.Max(0, Viewport.TimeAt(point.X))) is { } clipError) ErrorRaised?.Invoke(clipError);
            return true;
        }
        var plan = TimelineDropPlan.ForSequence(Viewport, TrackCount, point.Y);
        if (AddSequenceReference(name, plan.Kind == TimelineDropKind.Gap ? plan.Row : 0) is { } error) ErrorRaised?.Invoke(error);
        return true;
    }

    // ── Pointer ──────────────────────────────────────────────────────────────

    // A press on a reference clip: selects its track, then a drag moves it in time or resizes its right edge.
    private void BeginReferencePress(PointerPressedEventArgs e, Point point, TimelineReferenceHit hit)
    {
        int track = hit.Reference.Track;
        if (IsToggleModifier(e.KeyModifiers))
        {
            NotifySelection(_model.ToggleTrack(track));
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            NotifySelection(_model.ExtendTracksTo(track));
            return;
        }
        SetSelection(null, track);
        InvalidateVisual();
        if (e.ClickCount == 2)
        {
            ToggleReferenceExpanded(track);
            return;
        }
        _drag = DragKind.RefPending;
        _pressedPart = hit.Part;
        _pressPoint = point;
        _dragReference = hit.Reference.Reference;
        _dragReferenceTrack = track;
        _dragReferenceCycle = hit.Reference.TargetLength;
        _dragOriginStart = hit.Reference.Start;
        _dragOriginEdge = hit.Reference.End;
        _snapOthers = TimelineSnapTargets.Others(_blocks, Array.Empty<SequenceChainReferenceSave>(), _playhead, _references, _dragReference);
        _gestureBefore = _document!.BeginGesture();
        e.Pointer.Capture(this);
        ShowCursor(point);
    }

    private void MoveReferenceDrag(Point point, KeyModifiers modifiers)
    {
        if (_dragReference is null) return;
        if (_drag == DragKind.RefPending)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThresholdPixels && Math.Abs(point.Y - _pressPoint.Y) < DragThresholdPixels) return;
            _drag = _pressedPart == TimelineHitPart.Edge ? DragKind.RefEdge : DragKind.RefMove;
        }
        double dt = (point.X - _pressPoint.X) / Viewport.PixelsPerSecond;
        bool bypass = modifiers.HasFlag(KeyModifiers.Alt);
        if (_drag == DragKind.RefMove)
        {
            double length = SequenceTimeline.FullLength(_dragReference.Length, _dragReferenceCycle);
            var snap = TimelineSnap.Move(_dragOriginStart + dt, length > 0 ? length : TimelineLayout.MissingLength, _snapOthers, Viewport.PixelsPerSecond, bypass);
            _dragReference.Start = Math.Max(0, snap.Time);
            _snapGuide = snap.Target;
        }
        else
        {
            var targets = new List<SnapTarget>(_snapOthers);
            targets.AddRange(TimelineSnap.CycleMarks(_dragOriginStart, _dragReferenceCycle, _dragOriginEdge + dt, Viewport.PixelsPerSecond));
            var snap = TimelineSnap.Edge(_dragOriginEdge + dt, targets, Viewport.PixelsPerSecond, bypass,
                SequenceTimeline.MinimumEdge(_dragOriginStart));
            _dragReference.Length = SequenceTimeline.LengthForEdge(_dragOriginStart, snap.Time);
            _snapGuide = snap.Target;
        }
        Rebuild();
    }

    private void CommitReferenceDrag()
    {
        var kind = _drag;
        string? before = _gestureBefore;
        var reference = _dragReference;
        int track = _dragReferenceTrack;
        ResetDrag();
        if (kind == DragKind.RefPending || reference is null || before is null || _document is null || _undo is null || Sequence is not { } sequence
            || track >= sequence.Tracks.Count)
            return;
        string name = sequence.Tracks[track].Name;
        string description = kind == DragKind.RefEdge
            ? $"Set Length of '{name}' to {reference.Length?.ToString("0.###", CultureInfo.InvariantCulture)}"
            : SequenceLabel.Named("Move", "Track", name);
        _document.CommitGesture(_undo, description, before);
    }

    private bool IsReferenceEdgeAt(Point point) =>
        point.X >= Viewport.ColumnWidth && Sequence is { } sequence
        && TimelineReferences.HitTest(Viewport, _references, sequence.Tracks.Count, point.X, point.Y) is { Part: TimelineHitPart.Edge };

    // ── Menu ─────────────────────────────────────────────────────────────────

    // "Add Sequence Reference": a submenu of the sequences that may be added, or a disabled item when there are none.
    private MenuItem BuildReferenceMenu(string header)
    {
        var names = ReferenceCandidates;
        var item = TreeMenuRenderer.CreateMenuItem(header, () => { }, TimelineMenuLayout.Icon(TimelineMenuCommand.AddSequenceReference));
        foreach (string name in names)
        {
            var captured = name;
            var child = new MenuItem { Header = name };
            child.Click += (_, _) =>
            {
                if (AddSequenceReference(captured) is { } error) ErrorRaised?.Invoke(error);
            };
            item.Items.Add(child);
        }
        item.IsEnabled = names.Count > 0;
        return item;
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private void DrawReferences(DrawingContext context, Size size)
    {
        _filmstrip.BeginReferencePass();
        foreach (var reference in _references)
        {
            using (PushOpacityUnlessOpaque(context, IsTrackHidden(reference.Track) ? HiddenOpacity : 1))
                DrawReference(context, reference, size);
        }
    }

    private void DrawReference(DrawingContext context, TimelineReference reference, Size size)
    {
        double top = Viewport.LaneTop(reference.Track);
        double pps = Viewport.PixelsPerSecond;
        double left = Viewport.XAt(reference.Start);
        double width = Math.Max(1, reference.Length * pps);
        if (left > size.Width || left + width < Viewport.ColumnWidth) return;
        if (top > size.Height || top + Viewport.LaneHeightOf(reference.Track) < TimelineViewport.RulerHeight) return;

        var color = RefColor();
        var solid = new SolidColorBrush(color);
        var body = new Rect(left, top + TimelineLayout.BodyTop, width, TimelineLayout.BodyBottom - TimelineLayout.BodyTop);
        context.FillRectangle(new SolidColorBrush(color, 0.22), body, 3);
        if (reference.IsBroken) DrawHatched(context, body, solid, 0.4);
        context.DrawRectangle(null, new Pen(solid, 1.5), body, 3, 3);

        if (!IsExpanded(reference)) DrawClipFilmstrip(context, reference, body, color);

        // Whole-cycle marks of the played sequence, so a fractional count reads at a glance.
        if (reference.TargetLength > 0)
        {
            var mark = new Pen(new SolidColorBrush(color, 0.8), 1, new DashStyle(new double[] { 2, 2 }, 0));
            foreach (int k in TimelineCulling.VisibleCycles(Viewport, reference.Start, reference.Length, reference.TargetLength, size.Width))
            {
                if (k == 0) continue;
                double x = Snap(left + k * reference.TargetLength * pps);
                context.DrawLine(mark, new Point(x, body.Top + TimelineLayout.LabelHeight), new Point(x, body.Bottom - 2));
            }
        }

        if (width >= TimelineLayout.MinLabelPixels)
        {
            double labelWidth = Math.Max(1, body.Width - 26);
            string label = reference.IsBroken
                ? $"⚠ {reference.Reference.Sequence} ({(reference.IsMissing ? "missing" : "cycle")})"
                : BlockLabel.Fit(reference.Reference.Sequence, reference.Cycles, reference.Length, labelWidth, s => Text(s, 11, Brush("Ink")).Width);
            using (context.PushClip(body.Deflate(new Thickness(4, 0))))
            {
                DrawLinkGlyph(context, new Point(body.X + 12, body.Y + TimelineLayout.LabelHeight / 2 + 1), 11, solid);
                var text = TrimmedText(label, 11, Brush("Ink"), FontWeight.Normal, labelWidth);
                context.DrawText(text, new Point(body.X + 22, body.Y + (TimelineLayout.LabelHeight - text.Height) / 2 + 1));
            }
        }

        if (IsExpanded(reference)) DrawInheritedRows(context, reference, left, width, top, color);
    }

    // A collapsed clip's content: the target sequence as the preview draws it, cell by cell along the clip, each cycle repeating
    // the same cells. Cells not yet cut stay empty and are filled by the repaint this schedules.
    private void DrawClipFilmstrip(DrawingContext context, TimelineReference reference, Rect body, Color color)
    {
        if (_document is not { } document || reference.TargetLength <= 0) return;
        var film = new Rect(body.X, body.Y + TimelineLayout.LabelHeight, body.Width, TimelineLayout.FilmHeight);
        double scaling = (VisualRoot as TopLevel)?.RenderScaling ?? 1;
        _filmstrip.Stats = Stats;
        int pending = _filmstrip.DrawReference(context, document, reference, Viewport.PixelsPerSecond, film,
            Viewport.ColumnWidth, Bounds.Width, scaling, Color.FromArgb(0x4D, color.R, color.G, color.B));
        if (pending > 0 && !_referenceRepaintQueued)
        {
            _referenceRepaintQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _referenceRepaintQueued = false;
                InvalidateVisual();
            }, DispatcherPriority.Background);
        }
    }

    private bool _referenceRepaintQueued;

    // The inherited tracks as dashed bars: the target's first cycle, repeated for each cycle the clip plays. Read-only, so no handles.
    private void DrawInheritedRows(DrawingContext context, TimelineReference reference, double left, double width, double top, Color color)
    {
        double pps = Viewport.PixelsPerSecond;
        var dashed = new Pen(new SolidColorBrush(color, 0.9), 1, new DashStyle(new double[] { 3, 2 }, 0));
        var clip = new Rect(left, top, width, Viewport.LaneHeightOf(reference.Track));
        using (context.PushClip(clip))
        {
            for (int i = 0; i < reference.Rows.Count; i++)
            {
                var row = reference.Rows[i];
                double y = top + TimelineReferences.InheritedTop + i * TimelineReferences.InheritedRowHeight;
                using (PushOpacityUnlessOpaque(context, row.Visible ? 1 : HiddenOpacity))
                {
                    foreach (int cycle in TimelineCulling.VisibleCycles(Viewport, reference.Start, reference.Length, reference.TargetLength, Bounds.Width))
                    {
                        foreach (var span in row.Spans)
                        {
                            double x = left + (cycle * reference.TargetLength + span.Start) * pps;
                            double w = Math.Max(1, span.Length * pps);
                            if (x > left + width) continue;
                            var bar = new Rect(x, y + 1, Math.Min(w, left + width - x), TimelineReferences.InheritedRowHeight - 3);
                            context.FillRectangle(new SolidColorBrush(color, 0.1), bar, 2);
                            context.DrawRectangle(null, dashed, bar, 2, 2);
                            if (cycle == 0 && bar.Width >= 40)
                            {
                                using (context.PushClip(bar.Deflate(new Thickness(3, 0))))
                                {
                                    var label = TrimmedText(row.Name, 10, Brush("InkMid"), FontWeight.Normal, Math.Max(1, bar.Width - 6));
                                    context.DrawText(label, new Point(bar.X + 4, bar.Y + (bar.Height - label.Height) / 2));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawLinkGlyph(DrawingContext context, Point center, double size, IBrush brush)
    {
        double scale = size / 24;
        var pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X - size / 2, center.Y - size / 2)))
            context.DrawGeometry(null, pen, LinkGlyph);
    }

    // A reference track's header: eye, then a chevron when there is something to expand, the link icon and the name; the inherited names under it when expanded.
    private void DrawReferenceHeader(DrawingContext context, TimelineReference reference, double top, double header, bool hidden, Point nameCenter)
    {
        var solid = new SolidColorBrush(RefColor());
        if (reference.Expandable)
        {
            double cy = nameCenter.Y;
            var chevron = new StreamGeometry();
            using (var g = chevron.Open())
            {
                if (IsExpanded(reference))
                {
                    g.BeginFigure(new Point(ChevronCenterX - 4, cy - 2), true);
                    g.LineTo(new Point(ChevronCenterX + 4, cy - 2));
                    g.LineTo(new Point(ChevronCenterX, cy + 3));
                }
                else
                {
                    g.BeginFigure(new Point(ChevronCenterX - 2, cy - 4), true);
                    g.LineTo(new Point(ChevronCenterX + 3, cy));
                    g.LineTo(new Point(ChevronCenterX - 2, cy + 4));
                }
                g.EndFigure(true);
            }
            context.DrawGeometry(Brush("InkMid"), null, chevron);
        }
        using (PushOpacityUnlessOpaque(context, hidden ? HiddenOpacity + 0.2 : 1))
        {
            DrawLinkGlyph(context, new Point(LinkCenterX, nameCenter.Y), 13, solid);
            var name = TrimmedText(Sequence!.Tracks[reference.Track].Name, 12, Brush("Ink"), FontWeight.SemiBold,
                Math.Max(1, TimelineTooltips.NameRoom(header) - (ReferenceNameLeft - TimelineLayout.HeaderNameLeft)));
            context.DrawText(name, new Point(ReferenceNameLeft, nameCenter.Y - name.Height / 2));
        }
        if (!IsExpanded(reference)) return;
        for (int i = 0; i < reference.Rows.Count; i++)
        {
            double y = top + TimelineReferences.InheritedTop + i * TimelineReferences.InheritedRowHeight;
            using (PushOpacityUnlessOpaque(context, reference.Rows[i].Visible ? 1 : HiddenOpacity))
            {
                var inherited = TrimmedText(reference.Rows[i].Name, 10, Brush("InkMid"), FontWeight.Normal, Math.Max(1, header - InheritedNameLeft - 6));
                context.DrawText(inherited, new Point(InheritedNameLeft, y + (TimelineReferences.InheritedRowHeight - inherited.Height) / 2));
            }
        }
    }

    // While a sequence from the list is over the lanes: the lane area tinted, and a dashed ghost clip at the playhead in the new-track row.
    private void DrawSequenceGhost(DrawingContext context, Rect lanes)
    {
        if (_sequenceGhost is not { } ghost) return;
        var color = RefColor();
        context.FillRectangle(new SolidColorBrush(color, 0.08), lanes);
        if (SequenceDropSlot is not null) return; // a gap shows the reorder's insertion line instead (DrawSequenceGap)
        double length = GhostLength(ghost.Name);
        if (SequenceDropClipTrack is { } clipTrack)
        {
            // Over a reference lane: a dashed ghost clip where the pointer is.
            var onLane = new Rect(Viewport.XAt(Math.Max(0, Viewport.TimeAt(ghost.Point.X))), Viewport.LaneTop(clipTrack) + TimelineLayout.BodyTop,
                Math.Max(1, length * Viewport.PixelsPerSecond), TimelineViewport.LaneHeight - 2 * TimelineLayout.BodyTop);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 2, new DashStyle(new double[] { 4, 3 }, 0)), onLane, 3, 3);
            return;
        }
        double top = Viewport.LaneTop(TimelineViewport.NewTrackLane);
        var rect = new Rect(Viewport.XAt(_playhead), top + TimelineLayout.BodyTop, Math.Max(1, length * Viewport.PixelsPerSecond), TimelineViewport.LaneHeight - 2 * TimelineLayout.BodyTop);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 2, new DashStyle(new double[] { 4, 3 }, 0)), rect, 3, 3);
        using (context.PushClip(rect))
            context.DrawText(Text(ghost.Name, 11, new SolidColorBrush(color)), new Point(rect.X + 6, rect.Y + 4));
    }

    private double GhostLength(string name)
    {
        if (_document is not { } document || SequenceReferences.Find(document.Save.Sequences, name) is not { } target) return TimelineLayout.MissingLength;
        double length = SequenceTimeline.Length(document.Save.Sequences, target, reference => document.CycleLength(reference) ?? 0);
        return length > 0 ? length : TimelineLayout.MissingLength;
    }
}
