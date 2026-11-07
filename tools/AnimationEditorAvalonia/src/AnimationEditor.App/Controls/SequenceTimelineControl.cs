using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
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
public sealed partial class SequenceTimelineControl : Control, IZoomTarget
{
    private const double DragThresholdPixels = 3;
    private const double WheelZoomStep = 1.15;
    private const double WheelScrollPixels = 60;
    private const double RenameBoxHeight = 24;

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

    private InlineRenameBox? _renameBox;
    private int _renameTrack;

    private TimelineChainId? _selection;
    private (LibraryChip Chip, Point Point)? _chipGhost;
    private double _playhead;
    private bool _scrubbing;
    private Point _lastPoint;
    private TimelineCursor _shownCursor;

    public SequenceTimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;
        ContextMenu = _menu;
        _menu.Opening += (_, e) => e.Cancel = _menu.Items.Count == 0;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public TimelineViewport Viewport { get; } = new();

    /// <summary>The cursor the timeline last asked for. The OS cursor itself is not observable headlessly.</summary>
    public TimelineCursor ShownCursor => _shownCursor;

    /// <summary>The selected chain, or null. At most one thing is selected: choosing a chain clears <see cref="SelectedTrack"/> and the reverse.</summary>
    public TimelineChainId? Selection
    {
        get => _selection;
        private set => SetSelection(value, null);
    }

    /// <summary>The track whose header is selected, or null.</summary>
    public int? SelectedTrack { get; private set; }

    /// <summary>Selects chain <paramref name="chain"/>, or else track <paramref name="track"/>'s header, as a click would. A place that no longer exists clears the selection.</summary>
    public void Select(TimelineChainId? chain, int? track)
    {
        if (chain is { } id && _blocks.Any(b => b.Track == id.Track && b.Chain == id.Chain))
            SetSelection(id, null);
        else if (chain is null && track is { } header && header >= 0 && header < TrackCount)
            SetSelection(null, header);
        else
            SetSelection(null, null);
    }

    /// <summary>The context menu of the last right-click: a track header's, or a chain's.</summary>
    public ContextMenu Menu => _menu;

    /// <summary>The chains as drawn, for tests and the host.</summary>
    public IReadOnlyList<TimelineBlock> Blocks => _blocks;

    /// <summary>Raised when what is selected changes, for the inspector.</summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Raised after the shown sequence is rebuilt: an edit, undo, redo, or each step of a drag. The inspector reads the
    /// document again so its values follow a drag live.
    /// </summary>
    public event Action? ContentChanged;

    /// <summary>Raised when the user double-clicks a chain or picks Go to Definition on it.</summary>
    public event Action<SequenceChainReferenceSave>? GoToDefinitionRequested;

    /// <summary>Raised when the user picks Rename on a track header, with the track index. The host prompts for the name.</summary>
    public event Action<int>? RenameTrackRequested;

    /// <summary>Raised with the reason when an in-place rename is refused (an empty or taken name).</summary>
    public event Action<string>? ErrorRaised;

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
        {
            EndTrackRename();
            SetSelection(null, null);
        }
        _document = document;
        _sequenceIndex = sequenceIndex;
        _undo = undo;
        _filmstrip.Invalidate();
        Rebuild();
    }

    /// <summary>Removes the selected chain, or the selected track with its chains. Returns false when nothing is selected.</summary>
    public bool DeleteSelected()
    {
        if (_document is null || _undo is null) return false;
        if (Selection is { } selection)
        {
            Selection = null;
            _document.DeleteChain(_undo, _sequenceIndex, selection.Track, selection.Chain);
        }
        else if (SelectedTrack is { } track)
        {
            Selection = null;
            _document.DeleteTrack(_undo, _sequenceIndex, track);
        }
        else
        {
            return false;
        }
        Rebuild();
        return true;
    }

    /// <summary>Deselects the chain or track, so the inspector shows the sequence.</summary>
    public void ClearSelection() => SetSelection(null, null);

    /// <summary>True while a track's name is being edited in place.</summary>
    public bool IsRenamingTrack => _renameBox is { IsEditing: true };

    /// <summary>
    /// Edits track <paramref name="track"/>'s name in place over its header. Enter or clicking away commits through
    /// <see cref="SequenceDocument.RenameTrack"/> (one undo entry; a refused name goes out on <see cref="ErrorRaised"/>
    /// and the old name stays), Escape cancels.
    /// </summary>
    public void BeginRenameTrack(int track)
    {
        if (Sequence is not { } sequence || track < 0 || track >= sequence.Tracks.Count) return;
        EndTrackRename();
        if (_renameBox is null)
        {
            _renameBox = new InlineRenameBox();
            _renameBox.Committed += (_, _) => CommitTrackRename();
            _renameBox.Cancelled += (_, _) =>
            {
                EndTrackRename();
                Focus();
            };
        }
        _renameTrack = track;
        VisualChildren.Add(_renameBox);
        LogicalChildren.Add(_renameBox);
        InvalidateMeasure();
        InvalidateArrange();
        _renameBox.Begin(sequence.Tracks[track].Name);
    }

    private void CommitTrackRename()
    {
        if (_renameBox is null) return;
        string name = _renameBox.Text ?? string.Empty;
        int track = _renameTrack;
        EndTrackRename();
        Focus();
        if (_document is { } document && _undo is { } undo && track < TrackCount
            && document.RenameTrack(undo, _sequenceIndex, track, name) is { } error)
            ErrorRaised?.Invoke(error);
    }

    private void EndTrackRename()
    {
        if (_renameBox is not { } box || !VisualChildren.Contains(box)) return;
        box.Abandon();
        VisualChildren.Remove(box);
        LogicalChildren.Remove(box);
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _renameBox?.Measure(new Size(TimelineViewport.HeaderWidth - 4, RenameBoxHeight));
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_renameBox is { } box && VisualChildren.Contains(box))
        {
            box.Arrange(new Rect(2, Viewport.LaneTop(_renameTrack) + (TimelineViewport.LaneHeight - RenameBoxHeight) / 2,
                TimelineViewport.HeaderWidth - 4, RenameBoxHeight));
        }
        return finalSize;
    }

    private void SetSelection(TimelineChainId? chain, int? track)
    {
        if (_selection == chain && SelectedTrack == track) return;
        _selection = chain;
        SelectedTrack = track;
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Draws where <paramref name="chip"/> would land if dropped at <paramref name="point"/> (this control's
    /// coordinates), or clears that when <paramref name="chip"/> or <paramref name="point"/> is null.
    /// </summary>
    public void UpdateChipDrag(LibraryChip? chip, Point? point)
    {
        _chipGhost = chip is not null && point is { } p && PlanDrop(chip, p).Kind != TimelineDropKind.None ? (chip, p) : null;
        InvalidateVisual();
    }

    /// <summary>The dragged chip's current drop plan, or null when no chip is over a droppable row. The ghost is drawn from it and <see cref="DropChip"/> uses the same computation.</summary>
    public TimelineDropPlan? DropPreview => _chipGhost is { } ghost ? PlanDrop(ghost.Chip, ghost.Point) : null;

    /// <summary>The track lane a dragged chip is over (its whole row is highlighted), or null over the new-track row or nowhere.</summary>
    public int? DropTargetTrack => DropPreview is { Kind: TimelineDropKind.Track } plan ? plan.Row : null;

    /// <summary>True while a dragged chip is over the new-track row.</summary>
    public bool IsDropTargetNewTrack => DropPreview is { Kind: TimelineDropKind.NewTrack };

    private TimelineDropPlan PlanDrop(LibraryChip chip, Point point) =>
        Sequence is null
            ? TimelineDropPlan.None
            : TimelineDropPlan.For(Viewport, Bounds.Width, TrackCount, point.X, point.Y, ChainClock.TotalLength(chip.Chain));

    /// <summary>
    /// Adds <paramref name="chip"/>'s chain at <paramref name="point"/>: on a lane, at the time under the point; on the
    /// new-track row, as a new track. Returns false (and adds nothing) anywhere else.
    /// </summary>
    public bool DropChip(LibraryChip chip, Point point)
    {
        _chipGhost = null;
        InvalidateVisual();
        if (_document is null || _undo is null) return false;
        var plan = PlanDrop(chip, point);
        if (plan.Kind == TimelineDropKind.None) return false;
        _document.PlaceChain(_undo, _sequenceIndex, plan.Kind == TimelineDropKind.Track ? plan.Row : null,
            chip.Source, chip.Chain.Name, plan.Start);
        var sequence = Sequence!;
        int track = plan.Kind == TimelineDropKind.Track ? plan.Row : sequence.Tracks.Count - 1;
        Selection = new TimelineChainId(track, sequence.Tracks[track].Chains.Count - 1);
        Rebuild();
        return true;
    }

    /// <summary>
    /// What a chip drag should show at the mouse cursor at <paramref name="point"/> (this control's
    /// coordinates, null when the pointer is outside it): the same plan <see cref="DropChip"/> would use.
    /// </summary>
    public ChipDragFeedback ChipDragFeedbackAt(LibraryChip chip, Point? point)
    {
        var plan = point is { } p ? PlanDrop(chip, p) : TimelineDropPlan.None;
        string? trackName = plan.Kind == TimelineDropKind.Track ? Sequence!.Tracks[plan.Row].Name : null;
        return ChipDragFeedback.For(plan.Kind, trackName);
    }

    private void Rebuild()
    {
        _blocks = Sequence is { } sequence && _document is { } document
            ? TimelineLayout.Blocks(sequence, document.CycleLength)
            : Array.Empty<TimelineBlock>();
        if (Selection is { } selection && !_blocks.Any(b => b.Track == selection.Track && b.Chain == selection.Chain))
            Selection = null;
        if (SelectedTrack is { } track && track >= TrackCount)
            SetSelection(null, null);
        if (IsRenamingTrack && _renameTrack >= TrackCount)
            EndTrackRename();
        Viewport.ClampVertical(TrackCount, Bounds.Height);
        InvalidateVisual();
        ContentChanged?.Invoke();
    }

    // ── Pointer ──────────────────────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_document is null || Sequence is not { } sequence) return;
        // A press inside the rename box is the box's own (caret, word select).
        if (_renameBox is { } box && e.Source is Visual source && (source == box || box.IsVisualAncestorOf(source))) return;
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

        if (row.Kind == TimelineRowKind.Track && point.X < TimelineViewport.HeaderWidth)
        {
            SetSelection(null, row.Track);
            InvalidateVisual();
            if (e.ClickCount == 2) BeginRenameTrack(row.Track);
            return;
        }

        var hit = point.X >= TimelineViewport.HeaderWidth
            ? TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y)
            : null;
        if (hit is not { } found)
        {
            SetSelection(null, null);
            InvalidateVisual();
            return;
        }

        var block = found.Block;
        Selection = new TimelineChainId(block.Track, block.Chain);
        if (e.ClickCount == 2)
        {
            GoToDefinitionRequested?.Invoke(block.Reference);
            return;
        }
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
        ShowCursor(point);
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
        var point = e.GetPosition(this);
        ShowCursor(point);
        if (_drag == DragKind.None || Sequence is not { } sequence || _dragged is null || _dragBlock is null) return;
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
        _lastPoint = e.GetPosition(this);
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

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_drag != DragKind.None || _scrubbing) return;
        _lastPoint = new Point(-1, -1);
        ShowCursor(_lastPoint);
    }

    /// <summary>
    /// Shows the cursor for a pointer at <paramref name="point"/>. While a chain is gripped the pointer is captured, and
    /// only the raw OS call changes the cursor then (see <see cref="DragCursors"/>), so it goes through there on every move.
    /// Plain hover uses <see cref="InputElement.Cursor"/>.
    /// </summary>
    private void ShowCursor(Point point)
    {
        _lastPoint = point;
        TimelineHit? hover = point.X >= TimelineViewport.HeaderWidth
            ? TimelineLayout.HitTest(Viewport, _blocks, TrackCount, point.X, point.Y)
            : null;
        _shownCursor = TimelineCursors.For(hover, _drag == DragKind.None ? null : _pressedPart);
        Cursor = DragCursors.ForHover(_shownCursor);
        if (_drag != DragKind.None) DragCursors.Apply(this, _shownCursor);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || _drag == DragKind.None || _document is null || _gestureBefore is null) return;
        // Escape puts the dragged chain back with no undo entry.
        string before = _gestureBefore;
        ResetDrag();
        _document.CancelGesture(before);
        ReleaseCursor();
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (_drag == DragKind.None) return;
        CommitDrag();
        ReleaseCursor();
    }

    /// <summary>The drag held the OS cursor; hand it back to whatever is under the pointer now (after the blocks settle).</summary>
    private void ReleaseCursor()
    {
        ShowCursor(_lastPoint);
        DragCursors.Apply(this, _shownCursor);
    }

    private void CommitDrag()
    {
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
            InvalidateArrange();
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
            SetSelection(null, track);
            InvalidateVisual();
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
            var reference = hit.Block.Reference;
            var definition = new MenuItem { Header = "Go to Definition" };
            definition.Click += (_, _) => GoToDefinitionRequested?.Invoke(reference);
            var remove = new MenuItem { Header = "Remove from Track" };
            remove.Click += (_, _) => DeleteSelected();
            _menu.Items.Add(definition);
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
        var drop = DropPreview ?? TimelineDropPlan.None;

        using (context.PushClip(lanes))
        {
            for (int lane = 0; lane < sequence.Tracks.Count; lane++)
            {
                double top = Viewport.LaneTop(lane);
                if (lane % 2 == 1)
                    context.FillRectangle(Brush("TreeRowStripe"), new Rect(header, top, lanes.Width, laneHeight));
                context.DrawLine(line, new Point(header, top + laneHeight - 0.5), new Point(size.Width, top + laneHeight - 0.5));
                if (drop.Kind == TimelineDropKind.Track && drop.Row == lane)
                    DrawDropRow(context, new Rect(header, top, lanes.Width, laneHeight));
            }
            for (double t = firstTick; Viewport.XAt(t) < size.Width; t += step)
                context.DrawLine(line, new Point(Snap(Viewport.XAt(t)), ruler), new Point(Snap(Viewport.XAt(t)), size.Height));

            foreach (var block in _blocks)
                DrawBlock(context, block, Selection == new TimelineChainId(block.Track, block.Chain));
            DrawNewTrackRow(context, sequence, lanes, drop);
            DrawChipGhost(context, drop);
        }

        DrawHeaders(context, sequence, size, drop);
        DrawRuler(context, size, step, firstTick);
        DrawDropTime(context, size, drop);
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

    private void DrawHeaders(DrawingContext context, SequenceSave sequence, Size size, TimelineDropPlan drop)
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
                if (SelectedTrack == lane)
                {
                    var accent = Brush("Accent") is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;
                    context.FillRectangle(new SolidColorBrush(accent, 0.25), new Rect(0, top, header, TimelineViewport.LaneHeight));
                }
                if (drop.Kind == TimelineDropKind.Track && drop.Row == lane)
                    DrawDropRow(context, new Rect(0, top, header, TimelineViewport.LaneHeight));
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
            var film = new Rect(left, top + TimelineLayout.FilmTop, body.Width, TimelineLayout.FilmHeight);
            context.FillRectangle(Brush("BgCanvas"), film);
            DrawFilmstrip(context, block, film);
        }
        context.DrawRectangle(null, new Pen(selected ? Brush("Ink") : new SolidColorBrush(color), selected ? 2 : 1), body, 3, 3);

        // Whole-cycle marks, so a float loop count reads at a glance.
        if (block.CycleLength > 0)
        {
            var mark = new Pen(new SolidColorBrush(color, 0.8), 1, new DashStyle(new double[] { 2, 2 }, 0));
            double markTop = block.IsMissing ? body.Top + 2 : top + TimelineLayout.FilmTop;
            for (int k = 1; k * block.CycleLength < block.Played - 1e-9; k++)
            {
                double x = Snap(left + k * block.CycleLength * pps);
                context.DrawLine(mark, new Point(x, markTop), new Point(x, body.Bottom - 2));
            }
        }

        if (!TimelineLayout.ShowsLabel(body.Width)) return;
        string label = block.IsMissing ? $"⚠ {block.Reference.Chain} (missing)" : block.Reference.Chain;
        if (!block.IsMissing && Math.Abs(block.Reference.Loops - 1) > 1e-6)
            label += $"  ×{block.Reference.Loops.ToString("0.##", CultureInfo.InvariantCulture)}";
        using (context.PushClip(body.Deflate(new Thickness(5, 0))))
        {
            var text = Text(label, 11, Brush("Ink"));
            text.MaxTextWidth = Math.Max(1, body.Width - 12);
            text.MaxLineCount = 1;
            text.Trimming = TextTrimming.CharacterEllipsis;
            // A missing chain has no filmstrip, so its label centers in the whole block.
            double strip = block.IsMissing ? body.Height : TimelineLayout.LabelHeight;
            context.DrawText(text, new Point(body.X + 6, body.Y + (strip - text.Height) / 2));
        }
    }

    private const double DropRowOpacity = 0.14;

    private Color AccentCoolColor() => Brush("AccentCool") is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;

    private void DrawNewTrackRow(DrawingContext context, SequenceSave sequence, Rect lanes, TimelineDropPlan drop)
    {
        double top = Viewport.LaneTop(sequence.Tracks.Count);
        var row = new Rect(lanes.X + 4, top + 4, Math.Max(0, lanes.Width - 8), TimelineViewport.LaneHeight - 8);
        bool target = drop.Kind == TimelineDropKind.NewTrack;
        if (target)
            context.FillRectangle(new SolidColorBrush(AccentCoolColor(), DropRowOpacity), new Rect(lanes.X, top, lanes.Width, TimelineViewport.LaneHeight));
        context.DrawRectangle(null, new Pen(Brush(target ? "AccentCool" : "LineStrong"), 1, new DashStyle(new double[] { 4, 3 }, 0)), row, 4, 4);
        // The ghost sits where the hint would, and says the same thing.
        if (target) return;
        string hint = sequence.Tracks.Count == 0
            ? "Empty sequence. Drag a chain from the library here to start."
            : "Drop a chain here to make a new track";
        var text = Text(hint, 11, Brush("InkMid"));
        context.DrawText(text, new Point(row.X + 10, row.Y + (row.Height - text.Height) / 2));
    }

    // The whole row (header and lane) a dragged chip would land on, so a far-off ghost still says which track.
    private void DrawDropRow(DrawingContext context, Rect row)
    {
        var color = AccentCoolColor();
        context.FillRectangle(new SolidColorBrush(color, DropRowOpacity), row);
        var edge = new Pen(new SolidColorBrush(color), 1);
        context.DrawLine(edge, new Point(row.Left, row.Top + 0.5), new Point(row.Right, row.Top + 0.5));
        context.DrawLine(edge, new Point(row.Left, row.Bottom - 0.5), new Point(row.Right, row.Bottom - 0.5));
    }

    private void DrawChipGhost(DrawingContext context, TimelineDropPlan drop)
    {
        if (_chipGhost is not { } ghost || drop.Kind == TimelineDropKind.None) return;
        double top = Viewport.LaneTop(drop.Row);
        var rect = new Rect(drop.Left, top + TimelineLayout.BodyTop, drop.Width, TimelineLayout.BodyBottom - TimelineLayout.BodyTop);
        context.DrawRectangle(null, new Pen(Brush("AccentCool"), 2, new DashStyle(new double[] { 4, 3 }, 0)), rect, 3, 3);
        using (context.PushClip(rect))
            context.DrawText(Text(ghost.Chip.Chain.Name, 11, Brush("AccentCool")), new Point(rect.X + 6, rect.Y + 4));
    }

    // A thin dotted line down from the ruler at the drop time, with its value on the ruler.
    private void DrawDropTime(DrawingContext context, Size size, TimelineDropPlan drop)
    {
        if (drop.Kind == TimelineDropKind.None) return;
        double x = Snap(drop.Left);
        if (x < TimelineViewport.HeaderWidth || x > size.Width) return;
        var brush = Brush("AccentCool");
        context.DrawLine(new Pen(brush, 1, new DashStyle(new double[] { 2, 2 }, 0)), new Point(x, TimelineViewport.RulerHeight), new Point(x, size.Height));

        var label = Text(drop.TimeLabel, 10, Brush("Ink"), FontWeight.SemiBold);
        var pill = new Rect(x + 3, 2, label.Width + 10, TimelineViewport.RulerHeight - 5);
        if (pill.Right > size.Width) pill = pill.WithX(x - 3 - pill.Width);
        context.FillRectangle(Brush("BgPanel"), pill, 3);
        context.DrawRectangle(null, new Pen(brush, 1), pill, 3, 3);
        context.DrawText(label, new Point(pill.X + 5, pill.Y + (pill.Height - label.Height) / 2));
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
