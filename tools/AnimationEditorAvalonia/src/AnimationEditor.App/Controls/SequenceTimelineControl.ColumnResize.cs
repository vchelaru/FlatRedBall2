using System;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

// Dragging the right edge of the track-name column (#1765). The timeline is custom-drawn, so this is a hand-rolled
// splitter: the same west-east cursor as the app's GridSplitters, and a width the viewport clamps and every x mapping
// (lanes, ruler, playhead, hit-testing, drops, scrollbar alignment) already reads from.
public sealed partial class SequenceTimelineControl
{
    // Pixels left of the column edge (inside the header) that grab it. The lanes start at the edge, so none of them is
    // taken: a chain at time 0, the ruler start and the marquee all begin at x = ColumnWidth.
    private const double EdgeGrabLeft = 4;
    private const double EdgeHotWidth = 3;

    private bool _columnResizing;
    private bool _columnEdgeHot;
    private double _columnGrabOffset;

    /// <summary>True while the column edge is being dragged.</summary>
    public bool IsResizingColumn => _columnResizing;

    /// <summary>True while the pointer is over the column edge (it draws highlighted).</summary>
    public bool IsColumnEdgeHot => _columnEdgeHot;

    /// <summary>Raised once when a drag of the column edge ends, or a double-click fits it, with the new width.</summary>
    public event Action<double>? ColumnWidthCommitted;

    /// <summary>Sets the track-name column width (clamped to the viewport's limits) and redraws.</summary>
    public void SetColumnWidth(double width)
    {
        Viewport.SetColumnWidth(width);
        InvalidateArrange();
        InvalidateVisual();
    }

    private bool IsOverColumnEdge(Point point) =>
        point.X >= Viewport.ColumnWidth - EdgeGrabLeft && point.X < Viewport.ColumnWidth
        && point.Y >= 0 && point.Y <= Bounds.Height;

    // Handles a left press on the edge. A double-click fits the longest track name.
    private bool TryBeginColumnResize(PointerPressedEventArgs e, Point point)
    {
        if (!IsOverColumnEdge(point) || _drag != DragKind.None || _scrubbing || _panning) return false;
        if (e.ClickCount == 2)
        {
            SetColumnWidth(FitColumnWidth());
            ColumnWidthCommitted?.Invoke(Viewport.ColumnWidth);
            return true;
        }
        _columnResizing = true;
        _columnGrabOffset = point.X - Viewport.ColumnWidth;
        e.Pointer.Capture(this);
        ShowCursor(point);
        return true;
    }

    private void MoveColumnResize(Point point)
    {
        SetColumnWidth(point.X - _columnGrabOffset);
        ShowCursor(point);
    }

    private void EndColumnResize(IPointer? pointer)
    {
        if (!_columnResizing) return;
        _columnResizing = false;
        pointer?.Capture(null);
        ReleaseCursor();
        UpdateColumnEdgeHot(_lastPoint);
        ColumnWidthCommitted?.Invoke(Viewport.ColumnWidth);
    }

    private void UpdateColumnEdgeHot(Point point)
    {
        bool hot = _columnResizing || (_drag == DragKind.None && !_scrubbing && !_panning && IsOverColumnEdge(point));
        if (hot == _columnEdgeHot) return;
        _columnEdgeHot = hot;
        InvalidateVisual();
    }

    // The width that shows the longest track name whole, within the limits.
    private double FitColumnWidth()
    {
        double widest = 0;
        if (Sequence is { } sequence)
            foreach (var track in sequence.Tracks)
                widest = Math.Max(widest, Text(track.Name, 12, Brush("Ink"), FontWeight.SemiBold).Width);
        return Math.Ceiling(TimelineLayout.HeaderNameLeft + widest + 8);
    }
}
