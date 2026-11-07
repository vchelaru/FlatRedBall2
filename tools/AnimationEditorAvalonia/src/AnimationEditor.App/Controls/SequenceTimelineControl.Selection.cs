using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
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
        InvalidateVisual();
        if (changed) SelectionChanged?.Invoke();
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
        InvalidateVisual();
        if (point.X < TimelineViewport.HeaderWidth) return;
        _marqueeBase = additive ? _model.Chains.ToList() : Array.Empty<TimelineChainId>();
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
        var touched = TimelineMarquee.Chains(Viewport, _blocks, _marqueeStart.X, _marqueeStart.Y, point.X, point.Y);
        NotifySelection(_model.SelectChains(_marqueeBase.Concat(touched)));
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
