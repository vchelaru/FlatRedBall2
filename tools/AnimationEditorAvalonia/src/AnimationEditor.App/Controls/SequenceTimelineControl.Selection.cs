using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The timeline's selection: a <see cref="TimelineSelection"/> (chains or track headers, never both) plus the pointer
/// and key gestures that change it. Ctrl (Cmd) toggles, Shift takes a range, a rubber band over empty lane space takes
/// what it touches, Ctrl+A takes all, Escape clears. None of it is an undo entry.
/// </summary>
public sealed partial class SequenceTimelineControl
{
    private readonly TimelineSelection _model = new();

    // What is selected is drawn by this layer, a child that covers the control. Selecting, and dragging a rubber band, redraws
    // only it, so the cost follows what is selected and not every chain on a timeline of thousands.
    private readonly SelectionLayer _selectionLayer;

    // Whether the last draw of the track headers showed one as selected, so that the headers (drawn with the rest) are redrawn when that changes.
    private bool _headerSelectionShown;

    // Where each track's blocks start in _blocks, for the rubber band's binary search. Rebuilt with the blocks.
    private int[] _trackStarts = new int[1];

    private readonly TimelineMarqueeSelector _marquee = new();

    // SelectionChanged reaches the Inspector; during a rubber band it is raised at most this often, and once more when the band ends.
    private readonly SelectionChangeThrottle _marqueeThrottle = new(Stopwatch.Frequency / 30);

    /// <summary>How many times the timeline content (chains, lanes, ruler) has been drawn. A selection change draws only the selection layer, so this does not move.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>How many times the selection layer has been drawn.</summary>
    internal int SelectionLayerRenderCount { get; private set; }

    // A rubber band: where it started and ended (this control's coordinates), and the chains a Ctrl band adds to.
    private Point _marqueeStart;
    private Point _marqueeEnd;
    private IReadOnlyList<TimelineChainId> _marqueeBase = Array.Empty<TimelineChainId>();

    // What the press that began the band was, so a release without a drag can tell a click on empty lane space (which seeks) from the rest.
    private bool _marqueeOnLane;
    private bool _marqueeModified;

    // A press on a chain that is part of a multi-selection keeps the selection for a drag; a release with no drag narrows it to this chain.
    private TimelineChainId? _collapseOnRelease;

    /// <summary>
    /// The primary selected chain, or null: the one chosen last, which a Shift click ranges from and the Inspector shows
    /// when it is alone. Null while tracks are selected.
    /// </summary>
    public TimelineChainId? Selection => _model.Kind == TimelineSelectionKind.Chains ? _model.PrimaryChain : null;

    /// <summary>The primary selected track header, or null. Null while chains are selected.</summary>
    public int? SelectedTrack => _model.Kind == TimelineSelectionKind.Tracks ? _model.PrimaryTrack : null;

    /// <summary>Every selected chain, in document order. Empty while tracks are selected.</summary>
    public IReadOnlyList<TimelineChainId> SelectedChains => _model.Chains;

    /// <summary>Every selected track header, ascending. Empty while chains are selected.</summary>
    public IReadOnlyList<int> SelectedTracks => _model.Tracks;

    /// <summary>"3 chains selected" when more than one chain or track is, else null; the Inspector shows it in place of any field.</summary>
    public string? SelectionSummary =>
        !_model.IsMulti ? null
        : _model.Kind == TimelineSelectionKind.Chains ? $"{Plural.Format(_model.Count, "chain")} selected"
        : $"{Plural.Format(_model.Count, "track")} selected";

    /// <summary>True while a rubber band is being dragged over empty lane space.</summary>
    public bool IsMarqueeActive => _drag == DragKind.Marquee;

    /// <summary>The rubber band as a rectangle in this control's coordinates, or null when none is dragged.</summary>
    public Rect? MarqueeRect => IsMarqueeActive ? new Rect(_marqueeStart, _marqueeEnd).Normalize() : null;

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

    /// <summary>Selects exactly these chains (those that exist), the way a rubber band would. None clears the selection.</summary>
    public void SelectChains(IEnumerable<TimelineChainId> chains)
    {
        var existing = chains.Where(id => _blocks.Any(b => b.Track == id.Track && b.Chain == id.Chain)).ToList();
        NotifySelection(_model.SelectChains(existing));
    }

    /// <summary>Selects exactly these track headers (those that exist). None clears the selection.</summary>
    public void SelectTracks(IEnumerable<int> tracks) =>
        NotifySelection(_model.SelectTracks(tracks.Where(track => track >= 0 && track < TrackCount)));

    /// <summary>Deselects the chain or track, so the inspector shows the sequence.</summary>
    public void ClearSelection() => SetSelection(null, null);

    private void SetSelection(TimelineChainId? chain, int? track) =>
        NotifySelection(chain is { } id ? _model.SelectChain(id) : track is { } header ? _model.SelectTrack(header) : _model.Clear());

    private void NotifySelection(bool changed)
    {
        InvalidateSelection();
        if (changed) SelectionChanged?.Invoke();
    }

    // Selecting chains redraws only the selection layer. The track headers belong to the content, so they are redrawn when one is or was selected.
    private void InvalidateSelection()
    {
        _selectionLayer.InvalidateVisual();
        if (_model.Tracks.Count > 0 || _headerSelectionShown) InvalidateVisual();
    }

    // After a rebuild, what no longer exists leaves the selection. A chain stays selected while something is still at its place.
    private void RetainSelection()
    {
        var present = _blocks.Select(block => new TimelineChainId(block.Track, block.Chain)).ToHashSet();
        NotifySelection(_model.Retain(present.Contains, TrackCount));
    }

    private static bool IsToggleModifier(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    // ── Rubber band ──────────────────────────────────────────────────────────

    private void BeginMarquee(Point point, Avalonia.Input.PointerPressedEventArgs e, bool additive, bool onLane)
    {
        // Pressing empty space clears the selection, as it always did; Ctrl keeps it for the band to add to.
        if (!additive) SetSelection(null, null);
        _selectionLayer.InvalidateVisual();
        if (point.X < TimelineViewport.HeaderWidth) return;
        _marqueeBase = additive ? _model.Chains.ToList() : Array.Empty<TimelineChainId>();
        _marquee.Begin(_marqueeBase);
        _marqueeThrottle.Reset();
        _marqueeOnLane = onLane;
        _marqueeModified = additive || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _drag = DragKind.MarqueePending;
        _marqueeStart = _marqueeEnd = point;
        _pressPoint = point;
        _dragPointer = e.Pointer;
        e.Pointer.Capture(this);
    }

    // A release that never became a rubber band is a click: on empty lane space it moves the playhead, as the ruler does.
    private void SeekFromEmptyClick()
    {
        if (_drag is not (DragKind.MarqueePending or DragKind.Marquee)) return;
        if (TimelineEmptyClick.PlayheadTime(Viewport, SequenceLength, _marqueeOnLane, _marqueeModified, _drag == DragKind.Marquee, _marqueeStart.X) is { } time)
            Scrubbed?.Invoke(time);
    }

    private void MoveMarquee(Point point)
    {
        if (_drag == DragKind.MarqueePending)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThresholdPixels && Math.Abs(point.Y - _pressPoint.Y) < DragThresholdPixels) return;
            _drag = DragKind.Marquee;
        }
        _marqueeEnd = point;
        bool changed = _marquee.Update(_model, Viewport, _blocks, _trackStarts, _marqueeStart.X, _marqueeStart.Y, point.X, point.Y);
        // The band itself moved, so the layer redraws; the selection is only reported when it really changed, and not faster than the Inspector needs.
        _selectionLayer.InvalidateVisual();
        if (changed && _marqueeThrottle.Request(Stopwatch.GetTimestamp()))
            SelectionChanged?.Invoke();
    }

    /// <summary>Ends a rubber band: reports a selection change that was held back, so listeners end up with the final selection.</summary>
    private void EndMarquee()
    {
        bool held = _marqueeThrottle.Flush();
        _marqueeThrottle.Reset();
        _selectionLayer.InvalidateVisual();
        if (held) SelectionChanged?.Invoke();
    }

    private void RenderSelectionLayer(DrawingContext context)
    {
        SelectionLayerRenderCount++;
        if (Sequence is null) return;
        var size = Bounds.Size;
        double header = TimelineViewport.HeaderWidth;
        double ruler = TimelineViewport.RulerHeight;
        var lanes = new Rect(header, ruler, Math.Max(0, size.Width - header), Math.Max(0, size.Height - ruler));
        using (context.PushClip(lanes))
        {
            if (_model.Chains.Count > 0)
            {
                var pen = new Pen(Brush("Ink"), 2);
                foreach (var block in _blocks)
                {
                    double left = Viewport.XAt(block.Start);
                    double width = Math.Max(1, block.Played * Viewport.PixelsPerSecond);
                    if (left > size.Width || left + width < header || !_model.Contains(new TimelineChainId(block.Track, block.Chain))) continue;
                    var body = new Rect(left, Viewport.LaneTop(block.Track) + TimelineLayout.BodyTop, width, TimelineLayout.BodyBottom - TimelineLayout.BodyTop);
                    using (context.PushOpacity(IsTrackHidden(block.Track) ? HiddenOpacity : 1))
                        context.DrawRectangle(null, pen, body, 3, 3);
                }
            }
            DrawMarquee(context);
        }
    }

    /// <summary>The child control that draws the selection over the timeline. It takes no input.</summary>
    private sealed class SelectionLayer : Control
    {
        private readonly SequenceTimelineControl _owner;

        public SelectionLayer(SequenceTimelineControl owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context) => _owner.RenderSelectionLayer(context);
    }

    private void DrawMarquee(DrawingContext context)
    {
        if (MarqueeRect is not { } rect) return;
        var accent = Brush("Accent") is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;
        context.FillRectangle(new SolidColorBrush(accent, 0.12), rect);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(accent), 1, new DashStyle(new double[] { 3, 2 }, 0)), rect);
    }

    // ── Keys ─────────────────────────────────────────────────────────────────

    /// <summary>Ctrl+A selects all, Escape cancels a rubber band or clears the selection. True when the key was used.</summary>
    private bool HandleSelectionKey(KeyEventArgs e)
    {
        if (e.Key == Key.A && IsToggleModifier(e.KeyModifiers) && _drag == DragKind.None && !IsRenamingTrack)
        {
            NotifySelection(_model.SelectAll(_blocks, TrackCount));
            e.Handled = true;
            return true;
        }
        if (e.Key != Key.Escape) return false;
        if (_drag is DragKind.MarqueePending or DragKind.Marquee)
        {
            var before = _marqueeBase;
            var pointer = _dragPointer;
            _marqueeThrottle.Reset();
            ResetDrag();
            NotifySelection(_model.SelectChains(before));
            pointer?.Capture(null);
            e.Handled = true;
            return true;
        }
        if (_drag == DragKind.None && !IsRenamingTrack && _model.Count > 0)
        {
            NotifySelection(_model.Clear());
            e.Handled = true;
            return true;
        }
        return false;
    }
}
