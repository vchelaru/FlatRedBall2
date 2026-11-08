using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A <c>.seqj</c> tab's timeline for one sequence: a ruler, a lane per track with its chains, and a dashed row that
/// makes a new track. Drag a chain to move it in time or to another lane; drag its right edge to change its loop
/// count; drop a library chip to add a chain; drag a track header up or down to reorder the lanes (the top lane
/// draws in front, see <see cref="TrackReorder"/>). Each gesture is one undo entry, recorded on release. Ctrl+wheel zooms
/// around the pointer; the wheel scrolls (Shift+wheel always sideways). Pressing or dragging on the ruler scrubs: the
/// host moves its clock on <see cref="Scrubbed"/> and sets <see cref="Playhead"/> back. Geometry and hit-testing live in
/// <see cref="TimelineViewport"/> and <see cref="TimelineLayout"/>.
/// </summary>
public sealed partial class SequenceTimelineControl : Control, IZoomTarget
{
    private const double DragThresholdPixels = 3;
    private const double WheelScrollPixels = 60;
    private const double RenameBoxHeight = 24;
    // A hidden track keeps its lane and blocks, drawn faint so they stay readable and editable.
    private const double HiddenOpacity = 0.4;

    private enum DragKind { None, Pending, Move, Edge, TrackPending, Track, MarqueePending, Marquee }

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

    // Snapping: the targets a move or resize snaps to, taken when the drag starts, and the one it is snapped to now.
    private IReadOnlyList<SnapTarget> _snapOthers = Array.Empty<SnapTarget>();

    // A chain drag moves every selected chain: where each began, the lanes the pointer asks for, and the selection to put back on Escape.
    private sealed record DragItem(SequenceChainReferenceSave Reference, int Track, double Start);
    private IReadOnlyList<DragItem> _dragItems = Array.Empty<DragItem>();
    private int _requestedTrackDelta;
    private IReadOnlyList<TimelineChainId> _dragSelectionBefore = Array.Empty<TimelineChainId>();
    private TimelineChainId? _dragPrimaryBefore;
    private IReadOnlyList<double>? _frameBoundaries;
    private bool _dragRepeats = true;
    private SnapTarget? _snapGuide;
    private bool _chipBypass;

    private int? _hoverEyeTrack;
    private Point _eyePoint;

    // A track-header drag: which lane, the gap the pointer points at, and the pointer to release on Escape.
    private int _dragTrack;
    private int _trackSlot;
    private IPointer? _dragPointer;

    private InlineRenameBox? _renameBox;
    private int _renameTrack;

    private (LibraryChip Chip, Point Point)? _chipGhost;

    // While a dragged chain is over the new-track row: the same plan a chip dropped there would have. The drop makes the track(s).
    private TimelineDropPlan? _moveDrop;
    private double _playhead;
    private bool _scrubbing;
    private bool _panning;
    private Point _panLast;
    private Point _lastPoint;
    private TimelineCursor _shownCursor;

    public SequenceTimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;
        ContextMenu = _menu;
        _menu.Opening += (_, e) => e.Cancel = _menu.Items.Count == 0;
        _selectionLayer = new SelectionLayer(this);
        VisualChildren.Add(_selectionLayer);
        LogicalChildren.Add(_selectionLayer);
        _popupSurface = new PopupSurface(this);
        VisualChildren.Add(_popupSurface);
        LogicalChildren.Add(_popupSurface);
        ActualThemeVariantChanged += (_, _) =>
        {
            ClearThemeCaches();
            RefreshPlayheadLayer();
            _selectionLayer.InvalidateVisual();
            InvalidatePopups();
            InvalidateVisual();
        };
        _filmstrip.TextureChanged += () =>
        {
            InvalidatePopups();
            InvalidateVisual();
        };
        Viewport.Changed += () =>
        {
            Diagnostics.NoteInput();
            RefreshPlayheadLayer();
            // The selection layer draws at the chains' positions, so it follows every scroll, zoom and lane change.
            _selectionLayer.InvalidateVisual();
            ViewChanged?.Invoke();
        };
        // The same smooth preset-stepping wheel zoom as the preview and texture viewport, over the timeline's own
        // zoom limits. The pivot is a time axis, so only its x matters; the time under it stays fixed.
        _zoomAnimator = new ZoomAnimator(
            () => Viewport.Zoom,
            (x, _, factor) => { Viewport.ZoomAround(x, factor); AfterZoomTick(); },
            zoom => { Viewport.SetPixelsPerSecondAround(_zoomPivotX, TimelineViewport.PixelsPerSecondForZoom(zoom)); AfterZoomTick(); },
            () => ZoomPresetStepper.Presets,
            TimelineViewport.MinZoom,
            TimelineViewport.MaxZoom);
        DetachedFromVisualTree += (_, _) => _zoomAnimator.StopTimer();
    }

    private readonly ZoomAnimator _zoomAnimator;
    private double _zoomPivotX;

    private void AfterZoomTick()
    {
        ZoomChanged?.Invoke(Zoom * 100f);
        InvalidateVisual();
    }

    /// <summary>True while a wheel zoom is easing toward its preset.</summary>
    internal bool IsZoomAnimating => _zoomAnimator.IsAnimating;

    /// <summary>Test-only: advances the in-flight wheel zoom by <paramref name="dtSeconds"/>; true while still easing.</summary>
    internal bool StepZoomAnimation(float dtSeconds) => _zoomAnimator.Step(dtSeconds);

    /// <summary>Test-only: runs the in-flight wheel zoom to its preset synchronously.</summary>
    internal void SettleZoomAnimation() => _zoomAnimator.Settle();

    public TimelineViewport Viewport { get; } = new();

    /// <summary>The cursor the timeline last asked for. The OS cursor itself is not observable headlessly.</summary>
    public TimelineCursor ShownCursor => _shownCursor;

    /// <summary>True while the pointer is over the "+ new track" cell of the header column.</summary>
    public bool IsNewTrackHot { get; private set; }

    /// <summary>True while a chain is being moved or resized and the change is not yet an undo entry.</summary>
    public bool IsDraggingChain => _drag is DragKind.Move or DragKind.Edge;

    /// <summary>Whether track <paramref name="track"/> of the shown sequence is hidden: its <see cref="SequenceTrackSave.Visible"/> is false.</summary>
    public bool IsTrackHidden(int track) =>
        Sequence is { } sequence && track >= 0 && track < sequence.Tracks.Count && !sequence.Tracks[track].Visible;

    /// <summary>Hides track <paramref name="track"/>, or shows it again. One undo entry, saved with the file; the track stays on the timeline, faint.</summary>
    public void ToggleTrackVisibility(int track)
    {
        if (_document is not { } document || _undo is not { } undo || Sequence is null || track < 0 || track >= TrackCount) return;
        document.SetTrackVisible(undo, _sequenceIndex, track, IsTrackHidden(track));
    }

    /// <summary>Where lane <paramref name="lane"/>'s eye and name are centered vertically, in this control's coordinates. They share one line.</summary>
    public (double Eye, double Name) HeaderCenters(int lane)
    {
        double top = Viewport.LaneTop(lane);
        return (top + TimelineLayout.EyeCenterY, top + TimelineLayout.HeaderCenterY);
    }

    /// <summary>The track whose eye the pointer is over, or null.</summary>
    public int? HoverEyeTrack => _hoverEyeTrack;

    /// <summary>The tooltip for the eye under the pointer ("Visible" or "Hidden"), or null when the pointer is not over one. <see cref="EyeTooltipNote"/> goes under it.</summary>
    public string? EyeTooltip =>
        _hoverEyeTrack is { } track ? (IsTrackHidden(track) ? "Hidden" : "Visible") : null;

    /// <summary>The second line of the eye's tooltip: the eye is saved state, not just a preview setting.</summary>
    public const string EyeTooltipNote = "Saved in the file; runtimes may skip hidden tracks";

    /// <summary>The lane whose header is being dragged to reorder it, or null. A press that has not moved past the threshold is a click, not a drag.</summary>
    public int? DraggedTrack => _drag == DragKind.Track ? _dragTrack : null;

    /// <summary>
    /// The gap the dragged header would drop into, 0 above the first lane to the track count below the last, or null
    /// when no header is dragged or the gap is beside the dragged lane itself (dropping there changes nothing).
    /// </summary>
    public int? TrackDropSlot => _drag == DragKind.Track && _trackSlot != _dragTrack && _trackSlot != _dragTrack + 1 ? _trackSlot : null;

    /// <summary>The y of the line marking <see cref="TrackDropSlot"/>, in this control's coordinates.</summary>
    public double? TrackDropLineY => TrackDropSlot is { } slot ? TrackReorder.SlotY(Viewport, slot) : null;

    /// <summary>
    /// The target the current drag (a move, a resize, or a chip over a lane) is snapped to, or null when it is free. The
    /// timeline draws a guide line at its time.
    /// </summary>
    public SnapTarget? SnapGuide => _snapGuide ?? DropPreview?.Snapped;

    /// <summary>The time of <see cref="SnapGuide"/>, or null when nothing is snapped.</summary>
    public double? SnapGuideTime => SnapGuide?.Time;

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

    /// <summary>
    /// Raised while the ruler is pressed or dragged, and once when empty lane space is clicked, with the time under the
    /// pointer clamped to the sequence.
    /// </summary>
    public event Action<double>? Scrubbed;

    /// <summary>Where the playhead line is drawn, in seconds.</summary>
    public double Playhead
    {
        get => _playhead;
        set
        {
            if (_playhead == value) return;
            _playhead = value;
            // On its own layer the playhead moves without repainting the lanes under it.
            if (PlayheadLayer is { } layer) layer.InvalidateVisual();
            else InvalidateVisual();
        }
    }

    /// <summary>Seconds until the shown sequence ends (<see cref="SequenceTimeline.Length"/>); 0 when nothing is shown.</summary>
    public double SequenceLength =>
        Sequence is { } sequence && _document is { } document
            ? SequenceTimeline.Length(sequence, reference => document.CycleLength(reference) ?? 0)
            : 0;

    // IZoomTarget: 100% is the default pixels per second.
    public float Zoom => Viewport.Zoom;

    // Set by ZoomControl.Attach, which also drives the +/- buttons. The wheel animator reads the same shared list
    // (ZoomPresetStepper.Presets) directly, so this value is not read.
    public int[]? WheelZoomPresets { get; set; }

    public event Action<float>? ZoomChanged;

    public void SetZoomPercent(int percent)
    {
        _zoomAnimator.Cancel(); // an explicit zoom overrides any in-flight wheel ease
        ClearHover();
        Viewport.SetPixelsPerSecond(TimelineViewport.DefaultPixelsPerSecond * percent / 100.0);
        ZoomChanged?.Invoke(Zoom * 100f);
        InvalidateVisual();
    }

    /// <summary>
    /// Sets the zoom a file opens at (#1665): instantly, with no ease, and never past the timeline limits. The view start
    /// is kept within the shown sequence, since the zoom and the sequence may both differ from what it was scrolled for.
    /// Call it after <see cref="Show"/>.
    /// </summary>
    public void RestoreZoomPercent(int percent)
    {
        _zoomAnimator.Cancel();
        ClearHover();
        Viewport.SetPixelsPerSecond(TimelineViewport.DefaultPixelsPerSecond * percent / 100.0);
        double view = Math.Max(0, Bounds.Width - TimelineViewport.HeaderWidth) / Viewport.PixelsPerSecond;
        double furthest = Math.Max(0, SequenceLength + TimelineScrollBars.EndMarginPixels / Viewport.PixelsPerSecond - view);
        if (Viewport.ScrollSeconds > furthest) Viewport.SetScrollSeconds(furthest);
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
        // A track move, or its undo or redo, names the track to keep selected: the lane it ended up in.
        if (document?.TakeTrackSelection() is { } moved && moved < TrackCount)
            SetSelection(null, moved);
        _filmstrip.Invalidate();
        InvalidateEvents();
        Rebuild();
    }

    /// <summary>Removes every selected chain, or every selected track with its chains, as one undo entry. Returns false when nothing is selected.</summary>
    public bool DeleteSelected()
    {
        if (_document is null || _undo is null) return false;
        if (_model.Kind == TimelineSelectionKind.Chains)
        {
            var chains = _model.Chains.ToList();
            NotifySelection(_model.Clear());
            _document.DeleteChains(_undo, _sequenceIndex, chains);
        }
        else if (_model.Kind == TimelineSelectionKind.Tracks)
        {
            var tracks = _model.Tracks.ToList();
            NotifySelection(_model.Clear());
            _document.DeleteTracks(_undo, _sequenceIndex, tracks);
        }
        else
        {
            return false;
        }
        Rebuild();
        return true;
    }

    /// <summary>
    /// Adds an empty track at the end (the same edit a chip dropped on the new-track row makes, with no chain), selects it and
    /// opens its name for editing, with the default selected so typing replaces it and Escape keeps it. One undo entry.
    /// </summary>
    public void AddTrackAndRename()
    {
        if (_document is null || _undo is not { } undo || Sequence is null) return;
        _document.AddTrack(undo, _sequenceIndex);
        SetSelection(null, TrackCount - 1);
        Rebuild();
        BeginRenameTrack(TrackCount - 1);
    }

    /// <summary>
    /// Moves the one selected track a lane up (<paramref name="delta"/> -1) or down (+1), as dragging its header there does: one undo
    /// entry, the track stays selected. False when no single track is selected or it is already in the last lane that way.
    /// </summary>
    public bool MoveSelectedTrack(int delta)
    {
        if (_document is null || _undo is not { } undo || Sequence is null || _model.Kind != TimelineSelectionKind.Tracks || _model.Tracks.Count != 1) return false;
        int track = _model.Tracks[0];
        if (TrackReorder.StepSlot(track, TrackCount, delta) is not { } slot) return false;
        if (_document.MoveTrack(undo, _sequenceIndex, track, slot) is not { } moved) return false;
        SetSelection(null, moved);
        Rebuild();
        return true;
    }

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
            // Losing focus commits in the middle of the press that took it; see CommitTrackRename.
            _renameBox.Committed += (_, _) => CommitTrackRename(deferred: true);
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

    /// <summary>The track whose name is being edited in place, or null.</summary>
    public int? RenamingTrack => IsRenamingTrack ? _renameTrack : null;

    /// <summary>
    /// Applies the name typed into an open track rename now, as Enter would, for a host that is about to do something
    /// else (switch tabs, undo, close). Does nothing when no rename is open.
    /// </summary>
    public void CommitRename()
    {
        if (IsRenamingTrack) CommitTrackRename(deferred: false);
    }

    // A click on another row loses the box's focus in the middle of that press, before the row is selected. Renaming
    // rebuilds the list, which would drop the press's selection, so a focus-loss commit (deferred) waits for the press
    // to finish and no longer pulls focus back from whatever was clicked; Enter keeps the focus on the timeline.
    private void CommitTrackRename(bool deferred)
    {
        if (_renameBox is null) return;
        string name = _renameBox.Text ?? string.Empty;
        int track = _renameTrack;
        var document = _document;
        int sequenceIndex = _sequenceIndex;
        bool refocus = _renameBox.IsFocused;
        EndTrackRename();
        if (refocus) Focus();
        void Apply()
        {
            // The click that took the focus may have selected another sequence; the rename still belongs to the one it began in.
            if (document is null || !ReferenceEquals(document, _document) || _undo is not { } undo
                || sequenceIndex >= document.Save.Sequences.Count || track >= document.Save.Sequences[sequenceIndex].Tracks.Count) return;
            if (document.RenameTrack(undo, sequenceIndex, track, name, keepSelection: true) is { } error)
                ErrorRaised?.Invoke(error);
        }
        if (deferred) Dispatcher.UIThread.Post(Apply);
        else Apply();
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
        _selectionLayer.Measure(availableSize);
        _popupSurface.Measure(availableSize);
        _renameBox?.Measure(new Size(TimelineViewport.HeaderWidth - 4, RenameBoxHeight));
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _selectionLayer.Arrange(new Rect(finalSize));
        _popupSurface.Arrange(new Rect(finalSize));
        if (_renameBox is { } box && VisualChildren.Contains(box))
        {
            box.Arrange(new Rect(2, Viewport.LaneTop(_renameTrack) + TimelineLayout.HeaderCenterY - RenameBoxHeight / 2,
                TimelineViewport.HeaderWidth - 4, RenameBoxHeight));
        }
        return finalSize;
    }

    /// <summary>
    /// Draws where <paramref name="chip"/> would land if dropped at <paramref name="point"/> (this control's
    /// coordinates), or clears that when <paramref name="chip"/> or <paramref name="point"/> is null.
    /// </summary>
    public void UpdateChipDrag(LibraryChip? chip, Point? point, bool bypassSnap = false)
    {
        ClearHover();
        // Kept after the drag ends: the drop that follows plans from it.
        if (chip is not null) _chipBypass = bypassSnap;
        _chipGhost = chip is not null && point is { } p && PlanDrop(chip, p).Kind != TimelineDropKind.None ? (chip, p) : null;
        InvalidateVisual();
    }

    /// <summary>
    /// The current drop plan, or null when nothing is over a droppable row: a dragged chip over a lane or the new-track
    /// row, or a dragged chain over the new-track row. The ghost is drawn from it and the drop uses the same computation.
    /// </summary>
    public TimelineDropPlan? DropPreview => _moveDrop ?? (_chipGhost is { } ghost ? PlanDrop(ghost.Chip, ghost.Point) : null);

    /// <summary>The track lane a dragged chip is over (its whole row is highlighted), or null over the new-track row or nowhere.</summary>
    public int? DropTargetTrack => DropPreview is { Kind: TimelineDropKind.Track } plan ? plan.Row : null;

    /// <summary>True while a dragged chip is over the new-track row.</summary>
    public bool IsDropTargetNewTrack => DropPreview is { Kind: TimelineDropKind.NewTrack };

    private TimelineDropPlan PlanDrop(LibraryChip chip, Point point) =>
        Sequence is null
            ? TimelineDropPlan.None
            : TimelineDropPlan.For(Viewport, Bounds.Width, TrackCount, point.X, point.Y, ChainClock.TotalLength(chip.Chain),
                TimelineSnapTargets.Others(_blocks, Array.Empty<SequenceChainReferenceSave>(), _playhead), _chipBypass);

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
        PlaceAndSelect(chip, plan.Kind == TimelineDropKind.Track ? plan.Row : null, plan.Start);
        return true;
    }

    // The drop and "add without dragging" end the same way: one PlaceChain (so one undo entry, named alike) and the new chain selected.
    private void PlaceAndSelect(LibraryChip chip, int? track, double start)
    {
        _document!.PlaceChain(_undo!, _sequenceIndex, track, chip.Source, chip.Chain.Name, start);
        var sequence = Sequence!;
        int placed = track ?? sequence.Tracks.Count - 1;
        SetSelection(new TimelineChainId(placed, sequence.Tracks[placed].Chains.Count - 1), null);
        Rebuild();
    }

    /// <summary>True when the selection names exactly one track, so <see cref="ChipAddMode.SelectedTrack"/> has somewhere to add.</summary>
    public bool CanAddToSelectedTrack =>
        Sequence is not null && ChipAddPlan.For(ChipAddMode.SelectedTrack, SelectedChains, SelectedTracks, _playhead).CanAdd;

    /// <summary>
    /// Adds <paramref name="chip"/>'s chain at the playhead without a drag: on a new track at the end of the list, or on the
    /// one selected track (see <see cref="ChipAddPlan"/>). The new chain is selected and scrolled into view, as a drop's is.
    /// Returns null when added, else why nothing was (no sequence in the file, or no single selected track).
    /// </summary>
    public string? AddChip(LibraryChip chip, ChipAddMode mode)
    {
        if (_document is null || _undo is null || Sequence is null) return "Add a sequence first: the file has none to put the chain on.";
        var plan = ChipAddPlan.For(mode, SelectedChains, SelectedTracks, _playhead);
        if (!plan.CanAdd) return plan.Refusal;
        PlaceAndSelect(chip, plan.Track, plan.Start);
        RevealPlaced(plan.Start);
        return null;
    }

    // A chain added at the playhead can land off screen (a lane below the visible ones, a playhead scrolled away).
    private void RevealPlaced(double start)
    {
        int track = Selection?.Track ?? -1;
        if (track < 0) return;
        double top = Viewport.LaneTop(track), bottom = top + Viewport.LaneHeightOf(track);
        if (bottom > Bounds.Height) ScrollToY(Viewport.ScrollY + bottom - Bounds.Height);
        else if (top < TimelineViewport.RulerHeight) ScrollToY(Viewport.ScrollY - (TimelineViewport.RulerHeight - top));
        if (start < Viewport.ScrollSeconds || Viewport.XAt(start) > Bounds.Width) ScrollToSeconds(Math.Max(0, start - 0.1));
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
        _trackStarts = TimelineLayout.TrackStarts(_blocks, TrackCount);
        RetainSelection();
        if (IsRenamingTrack && _renameTrack >= TrackCount)
            EndTrackRename();
        Viewport.SetLaneHeights(TimelineLayout.LaneHeights(_blocks, TrackCount, HasEvents));
        Viewport.ClampVertical(TrackCount, Bounds.Height);
        InvalidateArrange();
        InvalidateVisual();
        _selectionLayer.InvalidateVisual();
        ContentChanged?.Invoke();
    }

    // ── Pointer ──────────────────────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ClearHover();
        if (_document is null || Sequence is not { } sequence) return;
        // A press inside the rename box is the box's own (caret, word select).
        if (_renameBox is { } box && e.Source is Visual source && (source == box || box.IsVisualAncestorOf(source))) return;
        Focus();
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var row = Viewport.RowAt(point.Y, sequence.Tracks.Count);

        // A middle-button drag pans from anywhere in the timeline, without touching selection or anything dragged.
        if (properties.IsMiddleButtonPressed)
        {
            if (_drag != DragKind.None || _scrubbing || _panning) return;
            _panning = true;
            _panLast = point;
            e.Pointer.Capture(this);
            ShowCursor(point);
            e.Handled = true;
            return;
        }
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

        if (row.Kind == TimelineRowKind.NewTrack && point.X < TimelineViewport.HeaderWidth)
        {
            AddTrackAndRename();
            return;
        }

        if (row.Kind == TimelineRowKind.Track && point.X < TimelineViewport.HeaderWidth)
        {
            // The eye is a button: it neither selects the track nor starts a rename.
            if (TimelineLayout.IsOverEye(point.X, point.Y - Viewport.LaneTop(row.Track)))
            {
                ToggleTrackVisibility(row.Track);
                return;
            }
            if (IsToggleModifier(e.KeyModifiers))
            {
                NotifySelection(_model.ToggleTrack(row.Track));
                return;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                NotifySelection(_model.ExtendTracksTo(row.Track));
                return;
            }
            SetSelection(null, row.Track);
            InvalidateVisual();
            if (e.ClickCount == 2)
            {
                BeginRenameTrack(row.Track);
                return;
            }
            // A drag of the header reorders the lanes once it passes the movement threshold; until then it is a click.
            _drag = DragKind.TrackPending;
            _pressedPart = TimelineHitPart.Body;
            _pressPoint = point;
            _dragTrack = row.Track;
            _trackSlot = row.Track;
            _dragPointer = e.Pointer;
            e.Pointer.Capture(this);
            ShowCursor(point);
            return;
        }

        var hit = point.X >= TimelineViewport.HeaderWidth
            ? TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y)
            : null;
        if (hit is not { } found)
        {
            BeginMarquee(point, e, IsToggleModifier(e.KeyModifiers), row.Kind == TimelineRowKind.Track);
            return;
        }

        var block = found.Block;
        var id = new TimelineChainId(block.Track, block.Chain);
        // Ctrl toggles and Shift extends; neither starts a drag, so a modifier click only edits the selection.
        if (IsToggleModifier(e.KeyModifiers))
        {
            NotifySelection(_model.ToggleChain(id));
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            NotifySelection(_model.ExtendChainsTo(id, _blocks));
            return;
        }
        // Pressing a chain that is part of a multi-selection keeps it, so the drag moves them all; releasing without a
        // drag then narrows the selection to the chain that was clicked.
        if (e.ClickCount == 1 && found.Part == TimelineHitPart.Body && _model.IsMulti && _model.Contains(id))
        {
            NotifySelection(_model.MakePrimary(id));
            _collapseOnRelease = id;
        }
        else
        {
            SetSelection(id, null);
        }
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
        // A body drag takes every selected chain along; an edge drag resizes just this one.
        var dragIds = found.Part == TimelineHitPart.Body && _model.Contains(id) ? _model.Chains : new[] { id };
        _dragItems = dragIds
            .Select(chain => _blocks.First(b => b.Track == chain.Track && b.Chain == chain.Chain))
            .Select(b => new DragItem(b.Reference, b.Track, b.Start)).ToList();
        _requestedTrackDelta = 0;
        _dragSelectionBefore = _model.Chains.ToList();
        _dragPrimaryBefore = _model.PrimaryChain;
        _snapOthers = TimelineSnapTargets.Others(_blocks, _dragItems.Select(item => item.Reference).ToList(), _playhead);
        _frameBoundaries = _document.FindChain(block.Reference) is { } chain ? TimelineSnapTargets.FrameBoundaries(chain) : null;
        _dragRepeats = _document.FindChain(block.Reference)?.Loop ?? true;
        _gestureBefore = _document.BeginGesture();
        e.Pointer.Capture(this);
        ShowCursor(point);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        UpdateEyeHover(e.GetPosition(this));
        UpdateHover(e.GetPosition(this));
        UpdateNewTrackHot(e.GetPosition(this));
        if (_panning)
        {
            var at = e.GetPosition(this);
            Viewport.Pan(at.X - _panLast.X, at.Y - _panLast.Y, TrackCount, Bounds.Height);
            _panLast = at;
            ShowCursor(at);
            InvalidateArrange();
            InvalidateVisual();
            return;
        }
        if (_scrubbing)
        {
            Scrubbed?.Invoke(Viewport.ScrubTime(e.GetPosition(this).X, SequenceLength));
            return;
        }
        var point = e.GetPosition(this);
        if (_drag is DragKind.TrackPending or DragKind.Track)
        {
            MoveTrackDrag(point);
            return;
        }
        if (_drag is DragKind.MarqueePending or DragKind.Marquee)
        {
            MoveMarquee(point);
            return;
        }
        ShowCursor(point);
        if (_drag == DragKind.None || Sequence is not { } sequence || _dragged is null || _dragBlock is null) return;
        if (_drag == DragKind.Pending)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThresholdPixels && Math.Abs(point.Y - _pressPoint.Y) < DragThresholdPixels) return;
            _drag = _pressedPart == TimelineHitPart.Edge ? DragKind.Edge : DragKind.Move;
        }

        double dt = (point.X - _pressPoint.X) / Viewport.PixelsPerSecond;
        bool bypass = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (_drag == DragKind.Move)
        {
            var snap = TimelineSnap.Move(_dragOriginStart + dt, _dragBlock.Full, _snapOthers, Viewport.PixelsPerSecond, bypass);
            _snapGuide = snap.Target;
            var row = Viewport.RowAt(point.Y, sequence.Tracks.Count);
            if (row.Kind == TimelineRowKind.Track) _requestedTrackDelta = row.Track - _dragOriginTrack;
            var plan = TimelineMultiMove.Plan(_dragItems.Select(item => new TimelineMultiMove.Origin(item.Track, item.Start)).ToList(),
                snap.Time - _dragOriginStart, _requestedTrackDelta, sequence.Tracks.Count);
            MoveDragItems(sequence, plan);
            // Over the new-track row nothing is created yet (Escape must leave no trace); the release makes the track(s).
            var onNewTrack = TimelineDropPlan.ForMove(Viewport, sequence.Tracks.Count, point.Y, _dragged.Start, _dragBlock.Full, snap.Target);
            _moveDrop = onNewTrack.Kind == TimelineDropKind.None ? null : onNewTrack;
        }
        else
        {
            var result = TimelineSnapTargets.ResizeEdge(_dragBlock, _dragOriginEdge + dt, _snapOthers, _frameBoundaries,
                Viewport.PixelsPerSecond, bypass, _dragRepeats);
            _dragged.Loops = result.Loops;
            _snapGuide = result.Snap.Target;
        }
        Rebuild();
    }

    // Puts every dragged chain at its origin plus the plan, in the lane it asks for, and keeps the selection on them.
    private void MoveDragItems(SequenceSave sequence, TimelineMultiMove.Result plan)
    {
        foreach (var item in _dragItems)
        {
            item.Reference.Start = item.Start + plan.TimeDelta;
            int target = item.Track + plan.TrackDelta;
            int current = sequence.Tracks.FindIndex(t => t.Chains.Contains(item.Reference));
            if (current == target) continue;
            sequence.Tracks[current].Chains.Remove(item.Reference);
            sequence.Tracks[target].Chains.Add(item.Reference);
        }
        SelectDragged(sequence, _dragItems, _dragged!);
    }

    // Selects the dragged chains where they are now, with the pressed one as the primary.
    private void SelectDragged(SequenceSave sequence, IReadOnlyList<DragItem> items, SequenceChainReferenceSave primary)
    {
        TimelineChainId IdOf(SequenceChainReferenceSave reference)
        {
            int track = sequence.Tracks.FindIndex(t => t.Chains.Contains(reference));
            return new TimelineChainId(track, sequence.Tracks[track].Chains.IndexOf(reference));
        }
        NotifySelection(_model.SelectChains(items.Select(item => IdOf(item.Reference)).ToList(), IdOf(primary)));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPoint = e.GetPosition(this);
        if (_panning)
        {
            if (e.InitialPressMouseButton == MouseButton.Middle) EndPan(e.Pointer);
            return;
        }
        if (_scrubbing)
        {
            _scrubbing = false;
            e.Pointer.Capture(null);
            return;
        }
        if (_drag == DragKind.None) return;
        if (e.InitialPressMouseButton == MouseButton.Left) SeekFromEmptyClick();
        // Commit before releasing the capture: releasing it raises CaptureLost, which cancels a track drag.
        if (_drag is DragKind.TrackPending or DragKind.Track)
        {
            EndDrag();
            e.Pointer.Capture(null);
            return;
        }
        e.Pointer.Capture(null);
        EndDrag();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _scrubbing = false;
        if (_panning)
        {
            _panning = false;
            ReleaseCursor();
        }
        // Losing the pointer mid-drag (the window deactivated) abandons a reorder rather than dropping it somewhere.
        if (_drag is DragKind.TrackPending or DragKind.Track)
        {
            ResetDrag();
            ReleaseCursor();
            InvalidateVisual();
            return;
        }
        EndDrag();
    }

    private void EndPan(IPointer pointer)
    {
        _panning = false;
        pointer.Capture(null);
        ReleaseCursor();
    }

    private void MoveTrackDrag(Point point)
    {
        if (_drag == DragKind.TrackPending)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThresholdPixels && Math.Abs(point.Y - _pressPoint.Y) < DragThresholdPixels)
            {
                ShowCursor(point);
                return;
            }
            _drag = DragKind.Track;
        }
        _trackSlot = TrackReorder.SlotAt(Viewport, TrackCount, point.Y);
        ShowCursor(point);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
        UpdateEyeHover(null);
        UpdateNewTrackHot(new Point(-1, -1));
        if (_drag != DragKind.None || _scrubbing || _panning) return;
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
        _shownCursor = _panning ? TimelineCursor.Pan
            : _drag == DragKind.Track ? TimelineCursor.Reorder
            : TimelineCursors.For(hover, _drag == DragKind.None ? null : _pressedPart);
        Cursor = DragCursors.ForHover(_shownCursor);
        if (_drag != DragKind.None || _panning) DragCursors.Apply(this, _shownCursor);
    }

    private void UpdateEyeHover(Point? point)
    {
        int? track = null;
        if (point is { } p && _drag == DragKind.None && !_scrubbing && _chipGhost is null && p.X < TimelineViewport.HeaderWidth
            && Sequence is { } sequence && Viewport.RowAt(p.Y, sequence.Tracks.Count) is { Kind: TimelineRowKind.Track } row
            && TimelineLayout.IsOverEye(p.X, p.Y - Viewport.LaneTop(row.Track)))
            track = row.Track;
        if (track == _hoverEyeTrack) return;
        _hoverEyeTrack = track;
        if (point is { } at) _eyePoint = at;
        InvalidatePopups();
        InvalidateVisual();
    }

    // The "+ new track" cell lights up under the pointer, except while a chain or the ruler is being dragged.
    private void UpdateNewTrackHot(Point point)
    {
        bool hot = _drag == DragKind.None && !_scrubbing && !_panning && Sequence is { } sequence && point.X >= 0
            && point.X < TimelineViewport.HeaderWidth && Viewport.RowAt(point.Y, sequence.Tracks.Count).Kind == TimelineRowKind.NewTrack;
        if (hot == IsNewTrackHot) return;
        IsNewTrackHot = hot;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F4 && Diagnostics.Enabled)
        {
            CycleProbeMode();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _drag is DragKind.TrackPending or DragKind.Track)
        {
            // Nothing was applied while dragging, so cancelling is just forgetting the drag.
            ResetDrag();
            _dragPointer?.Capture(null);
            ReleaseCursor();
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (HandleSelectionKey(e)) return;
        if (e.Key != Key.Escape || _drag == DragKind.None || _document is null || _gestureBefore is null) return;
        // Escape puts the dragged chain back with no undo entry.
        string before = _gestureBefore;
        var selectedBefore = _dragSelectionBefore;
        var primaryBefore = _dragPrimaryBefore;
        ResetDrag();
        _document.CancelGesture(before);
        NotifySelection(_model.SelectChains(selectedBefore, primaryBefore));
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
        if (_drag is DragKind.TrackPending or DragKind.Track)
        {
            CommitTrackDrag();
            return;
        }
        if (_drag is DragKind.MarqueePending or DragKind.Marquee)
        {
            ResetDrag();
            EndMarquee();
            return;
        }
        var kind = _drag;
        var dragged = _dragged;
        int originTrack = _dragOriginTrack;
        string? before = _gestureBefore;
        var collapse = _collapseOnRelease;
        var items = _dragItems;
        int movedCount = items.Count;
        bool toNewTrack = _moveDrop is { Kind: TimelineDropKind.NewTrack };
        ResetDrag();
        if (kind == DragKind.Pending && collapse is { } clicked) SetSelection(clicked, null);
        if (kind == DragKind.Pending || dragged is null || before is null || _document is null || _undo is null
            || Sequence is not { } sequence)
            return;

        string description;
        if (kind == DragKind.Edge)
        {
            description = $"Set Cycles of '{dragged.Chain}' to {dragged.Loops.ToString("0.##", CultureInfo.InvariantCulture)}";
        }
        else if (toNewTrack)
        {
            // Released on the new-track row: the chains are already at their new times; now they leave for new tracks.
            var created = TimelineNewTrackMove.Apply(sequence, items.Select(item => item.Reference).ToList());
            SelectDragged(sequence, items, dragged);
            description = movedCount > 1
                ? $"Move {Plural.Format(movedCount, "chain")} to {(created.Count == 1 ? $"new track '{sequence.Tracks[created[0]].Name}'" : Plural.Format(created.Count, "new track"))}"
                : $"Move '{dragged.Chain}' to new track '{sequence.Tracks[created[0]].Name}'";
        }
        else
        {
            var track = sequence.Tracks.First(t => t.Chains.Contains(dragged));
            description = movedCount > 1 ? $"Move {Plural.Format(movedCount, "chain")}"
                : sequence.Tracks.IndexOf(track) == originTrack ? $"Move '{dragged.Chain}'"
                : $"Move '{dragged.Chain}' to '{track.Name}'";
        }
        _document.CommitGesture(_undo, description, before);
    }

    /// <summary>Drops the dragged header into the gap it points at: one undo entry, and the moved track stays selected.</summary>
    private void CommitTrackDrag()
    {
        int? slot = TrackDropSlot;
        int track = _dragTrack;
        ResetDrag();
        InvalidateVisual();
        if (slot is not { } target || _document is null || _undo is null || Sequence is null) return;
        if (_document.MoveTrack(_undo, _sequenceIndex, track, target) is { } moved)
        {
            SetSelection(null, moved);
            Rebuild();
        }
    }

    private void ResetDrag()
    {
        _dragPointer = null;
        _drag = DragKind.None;
        _dragged = null;
        _dragBlock = null;
        _gestureBefore = null;
        _snapGuide = null;
        _snapOthers = Array.Empty<SnapTarget>();
        _frameBoundaries = null;
        _dragRepeats = true;
        _collapseOnRelease = null;
        _moveDrop = null;
        _dragItems = Array.Empty<DragItem>();
        _marqueeBase = Array.Empty<TimelineChainId>();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ClearHover();
        if (Sequence is null) return;
        var point = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            _zoomPivotX = Math.Max(point.X, TimelineViewport.HeaderWidth);
            _zoomAnimator.Wheel((float)_zoomPivotX, 0, e.Delta.Y);
        }
        else if (e.Delta.X != 0 || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            double delta = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
            Viewport.ScrollHorizontally(-delta * WheelScrollPixels);
        }
        else if (Viewport.ContentHeight(TrackCount) > Bounds.Height)
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
        _selectionLayer.InvalidateVisual();
        Viewport.ClampVertical(TrackCount, e.NewSize.Height);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    /// <summary>Raised when the menu's Copy, Cut or Paste is picked. The host moves the clipboard text; it acts on the pane that owns the edit hotkeys.</summary>
    public event Action<TimelineMenuCommand>? ClipboardCommandRequested;

    // A track header, a chain block, or blank space (empty lane space, the new-track row, below it). The ruler has no menu.
    private void BuildMenu(Point point, TimelineRow row)
    {
        _menu.Items.Clear();
        if (_document is null || _undo is null || Sequence is null || row.Kind == TimelineRowKind.Ruler) return;
        var target = TimelineMenuTarget.Blank;
        TimelineBlock? hitBlock = null;
        int track = row.Track;
        if (row.Kind == TimelineRowKind.Track && point.X < TimelineViewport.HeaderWidth)
        {
            target = TimelineMenuTarget.TrackHeader;
            if (!_model.ContainsTrack(track)) SetSelection(null, track);
        }
        else if (row.Kind == TimelineRowKind.Track && TimelineLayout.HitTest(Viewport, _blocks, TrackCount, point.X, point.Y) is { } hit)
        {
            target = TimelineMenuTarget.Chain;
            hitBlock = hit.Block;
            var hitId = new TimelineChainId(hit.Block.Track, hit.Block.Chain);
            if (!_model.Contains(hitId)) SetSelection(hitId, null);
            else NotifySelection(_model.MakePrimary(hitId));
        }
        InvalidateVisual();

        int selected = _model.Count;
        bool trackHidden = target == TimelineMenuTarget.TrackHeader && IsTrackHidden(track);
        var groups = TimelineMenuLayout.For(target, Sequence.Tracks.Any(t => !t.Visible));
        for (int i = 0; i < groups.Count; i++)
        {
            if (i > 0) _menu.Items.Add(new Separator());
            foreach (var command in groups[i])
            {
                Action onClick = command switch
                {
                    TimelineMenuCommand.Copy or TimelineMenuCommand.Cut or TimelineMenuCommand.Paste => () => ClipboardCommandRequested?.Invoke(command),
                    TimelineMenuCommand.Duplicate => DuplicateSelected,
                    TimelineMenuCommand.Delete => () => DeleteSelected(),
                    TimelineMenuCommand.RenameTrack => () => RenameTrackRequested?.Invoke(track),
                    TimelineMenuCommand.ToggleVisibility => () => ToggleTrackVisibility(track),
                    TimelineMenuCommand.ShowAllTracks => () => _document.ShowAllTracks(_undo, _sequenceIndex),
                    TimelineMenuCommand.GoToDefinition => () => GoToDefinitionRequested?.Invoke(hitBlock!.Reference),
                    TimelineMenuCommand.AddTrack => AddTrackAndRename,
                    _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
                };
                string header = TimelineMenuLayout.Header(target, command, selected, trackHidden);
                _menu.Items.Add(TimelineMenuLayout.Icon(command) is { } icon
                    ? TreeMenuRenderer.CreateMenuItem(header, onClick, icon)
                    : CreatePlainItem(header, onClick));
            }
        }
    }

    private static MenuItem CreatePlainItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        RenderCount++;
        Diagnostics.Frame(context, DrawTimeline, TimelineViewport.HeaderWidth + 6, TimelineViewport.RulerHeight + 6);
    }

    private void DrawTimeline(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brush("BgCanvas"), new Rect(size));
        if (ProbeMode == TimelineProbeMode.BackgroundOnly) return;
        if (Sequence is not { } sequence) return;

        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        var line = new Pen(Brush("LineBrush"), 1);
        var lanes = new Rect(header, ruler, Math.Max(0, size.Width - header), Math.Max(0, size.Height - ruler));
        double step = TimelineRuler.TickStep(Viewport.PixelsPerSecond);
        double firstTick = Math.Floor(Viewport.ScrollSeconds / step) * step;
        var drop = DropPreview ?? TimelineDropPlan.None;

        using (context.PushClip(lanes))
        {
            if (!TryDrawLaneBackdrop(context, sequence, size, step, firstTick, drop))
            {
                for (int lane = 0; lane < sequence.Tracks.Count; lane++)
                {
                    double top = Viewport.LaneTop(lane);
                    double laneHeight = Viewport.LaneHeightOf(lane);
                    Stats.LegacyDraws += 2;
                    if (lane % 2 == 1)
                        context.FillRectangle(Brush("TreeRowStripe"), new Rect(header, top, lanes.Width, laneHeight));
                    context.DrawLine(line, new Point(header, top + laneHeight - 0.5), new Point(size.Width, top + laneHeight - 0.5));
                    if (drop.Kind == TimelineDropKind.Track && drop.Row == lane)
                        DrawDropRow(context, new Rect(header, top, lanes.Width, laneHeight));
                }
                for (double t = firstTick; Viewport.XAt(t) < size.Width; t += step)
                {
                    Stats.GridLines++;
                    context.DrawLine(line, new Point(Snap(Viewport.XAt(t)), ruler), new Point(Snap(Viewport.XAt(t)), size.Height));
                }
            }

            Stats.Blocks = _blocks.Count;
            foreach (var block in _blocks)
            {
                using (PushOpacityUnlessOpaque(context, IsTrackHidden(block.Track) ? HiddenOpacity : 1))
                {
                    DrawBlock(context, block);
                    DrawEvents(context, block, size.Width);
                }
            }
            DrawNewTrackRow(context, sequence, lanes, drop);
            DrawChipGhost(context, drop);
        }

        DrawHeaders(context, sequence, size, drop);
        DrawTrackDrop(context, size);
        DrawRuler(context, size, step, firstTick);
        DrawDropTime(context, size, drop);
        DrawSnapGuide(context, size);
        if (PlayheadLayer is null) DrawPlayhead(context, size);
    }

    // A thin accent line at the snap target, over every lane, plus a tick on the edge of the chain it snapped to.
    private void DrawSnapGuide(DrawingContext context, Size size)
    {
        if (SnapGuide is not { } target || Sequence is null) return;
        double x = Snap(Viewport.XAt(target.Time));
        if (x < TimelineViewport.HeaderWidth || x > size.Width) return;
        double bottom = Math.Min(size.Height, Viewport.LaneTop(TrackCount));
        var brush = Brush("Accent");
        using (context.PushClip(new Rect(TimelineViewport.HeaderWidth, TimelineViewport.RulerHeight, Math.Max(0, size.Width - TimelineViewport.HeaderWidth), Math.Max(0, size.Height - TimelineViewport.RulerHeight))))
        {
            context.DrawLine(new Pen(brush, 1), new Point(x, TimelineViewport.RulerHeight), new Point(x, bottom));
            if (target.Kind == SnapKind.ChainEdge && target.Track >= 0 && target.Track < TrackCount)
            {
                double top = Viewport.LaneTop(target.Track) + TimelineLayout.BodyTop;
                context.DrawLine(new Pen(brush, 3), new Point(x, top), new Point(x, top + TimelineLayout.LabelHeight + TimelineLayout.FilmHeight));
            }
        }
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
        _headerSelectionShown = _model.Tracks.Count > 0;
        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        var area = new Rect(0, ruler, header, Math.Max(0, size.Height - ruler));
        using (context.PushClip(area))
        {
            context.FillRectangle(Brush("BgPanel"), area);
            for (int lane = 0; lane < sequence.Tracks.Count; lane++)
            {
                double top = Viewport.LaneTop(lane);
                double laneHeight = Viewport.LaneHeightOf(lane);
                if (_model.ContainsTrack(lane))
                    context.FillRectangle(new SolidColorBrush(AccentColor(), 0.25), new Rect(0, top, header, laneHeight));
                if (drop.Kind == TimelineDropKind.Track && drop.Row == lane)
                    DrawDropRow(context, new Rect(0, top, header, laneHeight));
                bool hidden = IsTrackHidden(lane);
                var centers = HeaderCenters(lane);
                DrawEye(context, new Point(TimelineLayout.EyeCenterX, centers.Eye), hidden, _hoverEyeTrack == lane);
                if (DraggedTrack == lane)
                {
                    // The grabbed header: a stronger fill and an outline, so it reads as lifted.
                    var grabbed = new Rect(1, top + 1, header - 3, laneHeight - 2);
                    context.FillRectangle(new SolidColorBrush(AccentColor(), 0.45), grabbed);
                    context.DrawRectangle(null, new Pen(Brush("Accent"), 2), grabbed);
                }
                var text = TrimmedText(sequence.Tracks[lane].Name, 12, Brush("Ink"), FontWeight.SemiBold, header - TimelineLayout.HeaderNameLeft - 8);
                using (PushOpacityUnlessOpaque(context, hidden ? HiddenOpacity + 0.2 : 1))
                    context.DrawText(text, new Point(TimelineLayout.HeaderNameLeft, centers.Name - text.Height / 2));
                context.DrawLine(new Pen(Brush("LineBrush"), 1), new Point(0, top + laneHeight - 0.5),
                    new Point(header, top + laneHeight - 0.5));
            }
            double newTop = Viewport.LaneTop(sequence.Tracks.Count);
            if (IsNewTrackHot)
            {
                context.FillRectangle(new SolidColorBrush(AccentCoolColor(), DropRowOpacity), new Rect(0, newTop, header, TimelineViewport.LaneHeight));
                context.DrawRectangle(null, new Pen(Brush("AccentCool"), 1), new Rect(1.5, newTop + 1.5, header - 3, TimelineViewport.LaneHeight - 3), 3, 3);
            }
            var newTrack = Text("+ new track", 11, Brush(IsNewTrackHot ? "Ink" : "InkMid"));
            context.DrawText(newTrack, new Point(8, newTop + (TimelineViewport.LaneHeight - newTrack.Height) / 2));
        }
        context.DrawLine(new Pen(Brush("LineStrong"), 1), new Point(header - 0.5, 0), new Point(header - 0.5, size.Height));
    }

    // A small drawn eye (an almond outline and a pupil); struck through when the track is hidden in the preview.
    private void DrawEye(DrawingContext context, Point center, bool hidden, bool hover)
    {
        var ink = Brush(hidden ? "InkMid" : "Ink");
        if (hover)
        {
            var wash = Brush("Ink") is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            double inset = TimelineLayout.EyeHitHalf - 1;
            context.FillRectangle(new SolidColorBrush(wash, 0.16),
                new Rect(center.X - inset, center.Y - inset, 2 * inset, 2 * inset), 3);
        }
        var pen = new Pen(ink, 1.25, lineCap: PenLineCap.Round);
        var almond = new StreamGeometry();
        using (var g = almond.Open())
        {
            g.BeginFigure(new Point(center.X - 6.5, center.Y), false);
            g.QuadraticBezierTo(new Point(center.X, center.Y - 8), new Point(center.X + 6.5, center.Y));
            g.QuadraticBezierTo(new Point(center.X, center.Y + 8), new Point(center.X - 6.5, center.Y));
            g.EndFigure(true);
        }
        context.DrawGeometry(null, pen, almond);
        if (hidden)
            context.DrawLine(pen, new Point(center.X - 6, center.Y + 5), new Point(center.X + 6, center.Y - 5));
        else
            context.DrawEllipse(ink, null, center, 2, 2);
    }

    // Drawn, not a ToolTip, like the filmstrip card: it follows the pointer and can be read headlessly (EyeTooltip).
    private void DrawEyeTooltip(DrawingContext context, Size size)
    {
        if (EyeTooltip is not { } tip) return;
        var text = Text(tip, 11, Brush("Ink"), FontWeight.SemiBold);
        var note = Text(EyeTooltipNote, 10, Brush("InkMid"));
        var pill = new Rect(_eyePoint.X + 12, _eyePoint.Y + 16, Math.Max(text.Width, note.Width) + 12, text.Height + note.Height + 8);
        pill = pill.WithX(Math.Clamp(pill.X, 0, Math.Max(0, size.Width - pill.Width)))
                   .WithY(Math.Clamp(pill.Y, 0, Math.Max(0, size.Height - pill.Height)));
        context.FillRectangle(Brush("BgPanel"), pill, 3);
        context.DrawRectangle(null, new Pen(Brush("LineStrong"), 1), pill, 3, 3);
        context.DrawText(text, new Point(pill.X + 6, pill.Y + 3));
        context.DrawText(note, new Point(pill.X + 6, pill.Y + 4 + text.Height));
    }

    // While a header is dragged: the dragged lane tinted across the lanes, and a line across the whole timeline at the gap it would drop into.
    private void DrawTrackDrop(DrawingContext context, Size size)
    {
        if (DraggedTrack is not { } dragged) return;
        double ruler = TimelineViewport.RulerHeight;
        using (context.PushClip(new Rect(0, ruler, size.Width, Math.Max(0, size.Height - ruler))))
        {
            context.FillRectangle(new SolidColorBrush(AccentColor(), 0.12),
                new Rect(TimelineViewport.HeaderWidth, Viewport.LaneTop(dragged), Math.Max(0, size.Width - TimelineViewport.HeaderWidth), Viewport.LaneHeightOf(dragged)));
            if (TrackDropLineY is not { } y) return;
            var brush = Brush("Accent");
            context.DrawLine(new Pen(brush, 3), new Point(0, y), new Point(size.Width, y));
            context.DrawEllipse(brush, null, new Point(4, y), 4, 4);
        }
    }

    private void DrawRuler(DrawingContext context, Size size, double step, double firstTick)
    {
        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        context.FillRectangle(Brush("BgRail"), new Rect(0, 0, size.Width, ruler));
        // Which end of the lane stack draws in front; the header column is otherwise empty up here.
        var order = Text("↑ front   ↓ back", 10, Brush("InkMid"));
        context.DrawText(order, new Point(8, (ruler - order.Height) / 2));
        using (context.PushClip(new Rect(header, 0, Math.Max(0, size.Width - header), ruler)))
        {
            var tick = new Pen(Brush("LineStrong"), 1);
            for (double t = firstTick; Viewport.XAt(t) < size.Width; t += step)
            {
                double x = Snap(Viewport.XAt(t));
                context.DrawLine(tick, new Point(x, ruler - 7), new Point(x, ruler));
                Stats.RulerLabels++;
                context.DrawText(Text(TimelineRuler.FormatTick(t, step), 10, Brush("InkMid")), new Point(x + 3, 3));
            }
        }
        context.DrawLine(new Pen(Brush("LineStrong"), 1), new Point(0, ruler - 0.5), new Point(size.Width, ruler - 0.5));
    }

    // The selection is not drawn here but by the selection layer above, so selecting never redraws every block.
    private void DrawBlock(DrawingContext context, TimelineBlock block)
    {
        if (!TimelineCulling.IsBlockVisible(Viewport, block, Bounds.Width, Bounds.Height)) return;
        double top = Viewport.LaneTop(block.Track);
        double left = Viewport.XAt(block.Start);
        double pps = Viewport.PixelsPerSecond;
        var body = new Rect(left, top + TimelineLayout.BodyTop, Math.Max(1, block.Played * pps),
            TimelineLayout.BodyBottom - TimelineLayout.BodyTop);
        Stats.BlocksIssued++;
        if (body.Right > TimelineViewport.HeaderWidth && body.Left < Bounds.Width) Stats.BlocksVisible++;

        if (block.IsCut)
        {
            var tail = new Rect(left + block.Played * pps, top + TimelineLayout.TailTop, (block.Full - block.Played) * pps,
                TimelineLayout.TailBottom - TimelineLayout.TailTop);
            DrawHatched(context, tail, Brush("InkMid"), 0.75);
        }

        string colorKey = block.IsMissing ? "Accent" : "AccentCool";
        var color = Brush(colorKey) is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;
        bool paintedByOp = false;
        if (block.IsMissing)
        {
            context.FillRectangle(new SolidColorBrush(color, 0.15), body, 3);
            DrawHatched(context, body, Brush(colorKey), 0.4);
        }
        else
        {
            var film = new Rect(left, top + TimelineLayout.FilmTop, body.Width, TimelineLayout.FilmHeight);
            if (ProbeMode != TimelineProbeMode.LegacyDrawing && BlockPaintFor(block, body, film, color, left, pps) is { } paint)
            {
                // The fill, backdrop, outline and cycle marks go with the filmstrip in one operation.
                DrawFilmstrip(context, block, film, paint);
                paintedByOp = true;
            }
            else
            {
                Stats.LegacyDraws += 2;
                context.FillRectangle(new SolidColorBrush(color, 0.35), body, 3);
                context.FillRectangle(Brush("BgCanvas"), film);
                DrawFilmstrip(context, block, film);
            }
        }
        if (!paintedByOp)
        {
            Stats.LegacyDraws++;
            context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 1), body, 3, 3);

            // Whole-cycle marks, so a fractional length in cycles reads at a glance (also where a non-looping chain's hold starts).
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
        }

        if (!TimelineLayout.ShowsLabel(body.Width)) return;
        string label = block.IsMissing ? $"⚠ {block.Reference.Chain} (missing)" : block.Reference.Chain;
        if (!block.IsMissing && Math.Abs(block.Reference.Loops - 1) > 1e-6)
            label += $"  ×{block.Reference.Loops.ToString("0.##", CultureInfo.InvariantCulture)}";
        using (context.PushClip(body.Deflate(new Thickness(5, 0))))
        {
            var text = TrimmedText(label, 11, Brush("Ink"), FontWeight.Normal, Math.Max(1, body.Width - 12));
            // A missing chain has no filmstrip, so its label centers in the whole block.
            double strip = block.IsMissing ? body.Height : TimelineLayout.LabelHeight;
            context.DrawText(text, new Point(body.X + 6, body.Y + (strip - text.Height) / 2));
        }
    }

    private const double DropRowOpacity = 0.14;

    private Color AccentColor() => Brush("Accent") is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;

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
        string? name = _chipGhost?.Chip.Chain.Name ?? (_moveDrop is not null ? _dragged?.Chain : null);
        if (name is null || drop.Kind == TimelineDropKind.None) return;
        double top = Viewport.LaneTop(drop.Row);
        var rect = new Rect(drop.Left, top + TimelineLayout.BodyTop, drop.Width, TimelineLayout.BodyBottom - TimelineLayout.BodyTop);
        context.DrawRectangle(null, new Pen(Brush("AccentCool"), 2, new DashStyle(new double[] { 4, 3 }, 0)), rect, 3, 3);
        using (context.PushClip(rect))
            context.DrawText(Text(name, 11, Brush("AccentCool")), new Point(rect.X + 6, rect.Y + 4));
    }

    // A thin dotted line down from the ruler at the drop time, with its value on the ruler.
    private void DrawDropTime(DrawingContext context, Size size, TimelineDropPlan drop)
    {
        if (drop.Kind == TimelineDropKind.None) return;
        double x = Snap(drop.Left);
        if (x < TimelineViewport.HeaderWidth || x > size.Width) return;
        var brush = Brush("AccentCool");
        context.DrawLine(new Pen(brush, 1, new DashStyle(new double[] { 2, 2 }, 0)), new Point(x, TimelineViewport.RulerHeight), new Point(x, size.Height));

        var label = Text(drop.TimeLabel, 10, Brush("Ink"), FontWeight.SemiBold, mono: true);
        var pill = new Rect(x + 3, 2, label.Width + 10, TimelineViewport.RulerHeight - 5);
        if (pill.Right > size.Width) pill = pill.WithX(x - 3 - pill.Width);
        context.FillRectangle(Brush("BgPanel"), pill, 3);
        context.DrawRectangle(null, new Pen(brush, 1), pill, 3, 3);
        context.DrawText(label, new Point(pill.X + 5, pill.Y + (pill.Height - label.Height) / 2));
    }

    private void DrawHatched(DrawingContext context, Rect rect, IBrush brush, double opacity)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var pen = new Pen(brush, 1) { };
        using (context.PushOpacity(opacity))
        using (context.PushClip(rect))
        {
            for (double x = rect.Left - rect.Height; x < rect.Right; x += 5)
            {
                Stats.HatchLines++;
                context.DrawLine(pen, new Point(x, rect.Bottom), new Point(x + rect.Height, rect.Top));
            }
        }
    }

    private static double Snap(double x) => Math.Round(x) + 0.5;
}
