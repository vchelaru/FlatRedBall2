using AnimationEditor.App.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Drag-to-reorder for the sequence list. A press on a row arms it; past <see cref="DragThreshold"/> the drag starts,
/// the pointer is captured, and an insertion line follows the gap the pointer points at (<see cref="SequenceReorder"/>).
/// Release drops, Escape or a lost capture cancels. The rows that move are the list's selection: pressing an unselected
/// row selects it alone first (the list does that on the press), and pressing a selected row of a multi-selection keeps
/// the selection so the whole block can drag, narrowing it to that row on release when no drag followed. Ctrl, Shift and
/// Meta presses, a second press of a double-click, and presses in the rename box never arm it.
/// </summary>
internal sealed class SequenceListDragController
{
    private const double DragThreshold = 4;
    private const double AutoScrollEdge = 24;
    private const double AutoScrollMaxStep = 18;
    private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(40);

    private readonly ListBox _list;
    private readonly Func<IReadOnlyList<int>> _movedRows;
    private readonly Action _commitPendingRename;
    private readonly Action<IReadOnlyList<int>, int> _drop;
    private readonly Action<double, double, double> _showLine;
    private readonly Action _hideLine;
    private readonly Func<Point, Point?> _toOverlay;

    private int _pressRow = -1;
    private Point _pressPoint;
    private bool _collapseOnRelease;
    private bool _dragging;
    private IPointer? _pointer;
    private Point _lastPoint;
    private List<int> _moved = new();
    private readonly List<Control> _dimmed = new();
    private DispatcherTimer? _scrollTimer;

    /// <param name="list">The sequence list.</param>
    /// <param name="movedRows">The rows a drag moves when it starts: the list's selected rows.</param>
    /// <param name="commitPendingRename">Applies an inline rename that is still open, before the drag starts.</param>
    /// <param name="drop">Moves the rows to a gap (see <see cref="SequenceReorder"/>).</param>
    /// <param name="showLine">Draws the insertion line at (left, right, y) in the drag overlay's coordinates.</param>
    /// <param name="hideLine">Removes the insertion line.</param>
    /// <param name="toOverlay">Maps a point in the list to the drag overlay's coordinates.</param>
    public SequenceListDragController(ListBox list, Func<IReadOnlyList<int>> movedRows, Action commitPendingRename,
        Action<IReadOnlyList<int>, int> drop, Action<double, double, double> showLine, Action hideLine, Func<Point, Point?> toOverlay)
    {
        _list = list;
        _movedRows = movedRows;
        _commitPendingRename = commitPendingRename;
        _drop = drop;
        _showLine = showLine;
        _hideLine = hideLine;
        _toOverlay = toOverlay;
        // Tunnel, so a press on a selected row of a multi-selection can be held back from the list's own select-on-press.
        list.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        list.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>True while a row is being dragged (past the threshold, not yet dropped or cancelled).</summary>
    public bool IsDragging => _dragging;

    /// <summary>
    /// Abandons a drag in progress without dropping: the list is being rebuilt under it, so its rows are stale. A press
    /// that has not become a drag is left alone (an inline rename committing between the press and the first move
    /// rebuilds the list but keeps the order).
    /// </summary>
    public void CancelDrag()
    {
        if (_dragging) End();
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        Reset();
        if (!e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) return;
        if (e.Source is not Visual source || source is InlineRenameBox || source.FindAncestorOfType<InlineRenameBox>() is not null) return;
        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } item) return;
        int row = _list.IndexFromContainer(item);
        if (row < 0) return;
        _pressRow = row;
        _pressPoint = e.GetPosition(_list);
        // Pressing a row of a multi-selection must not narrow it before we know whether this is a drag. The second
        // press of a double-click can still start a drag (click a row, then grab it) but is left to the list.
        if (e.ClickCount == 1 && _list.SelectedItems!.Count > 1 && _list.SelectedItems.Contains(item.DataContext))
        {
            _collapseOnRelease = true;
            e.Handled = true;
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressRow < 0) return;
        if (!e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
        {
            End();
            return;
        }
        var point = e.GetPosition(_list);
        _lastPoint = point;
        if (!_dragging)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThreshold && Math.Abs(point.Y - _pressPoint.Y) < DragThreshold) return;
            if (!Begin(e)) return;
        }
        UpdateIndicator();
        DragCursors.Apply(_list, TimelineCursor.Reorder);
    }

    private bool Begin(PointerEventArgs e)
    {
        // The rename's own commit may have rebuilt the list (it keeps the order), so the rows are read after it.
        _commitPendingRename();
        _moved = _movedRows().OrderBy(row => row).ToList();
        if (!_moved.Contains(_pressRow))
        {
            End();
            return false;
        }
        _collapseOnRelease = false;
        _dragging = true;
        _pointer = e.Pointer;
        e.Pointer.Capture(_list);
        _list.ContainerFromIndex(_pressRow)?.Focus();
        foreach (int row in _moved)
        {
            if (_list.ContainerFromIndex(row) is not { } container) continue;
            container.Opacity = 0.6;
            _dimmed.Add(container);
        }
        _scrollTimer = new DispatcherTimer { Interval = AutoScrollInterval };
        _scrollTimer.Tick += (_, _) => AutoScroll();
        _scrollTimer.Start();
        return true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressRow < 0) return;
        if (_dragging)
        {
            int slot = SlotAt(e.GetPosition(_list).Y);
            var moved = _moved;
            End();
            _drop(moved, slot);
            return;
        }
        int row = _pressRow;
        bool collapse = _collapseOnRelease;
        End();
        // A click on a selected row of a multi-selection narrows to it, as a plain click on a list row does.
        if (collapse && row < _list.ItemCount)
        {
            _list.SelectedIndex = row;
            _list.ContainerFromIndex(row)?.Focus();
        }
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        // Our own release of the capture arrives here after the state is already cleared.
        if (_dragging) End();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_dragging) return;
        End();
        e.Handled = true;
    }

    private void AutoScroll()
    {
        if (!_dragging || _list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroller) return;
        double delta = SequenceReorder.AutoScrollDelta(_lastPoint.Y, scroller.Viewport.Height, AutoScrollEdge, AutoScrollMaxStep);
        if (delta == 0) return;
        double max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        double offset = Math.Clamp(scroller.Offset.Y + delta, 0, max);
        if (offset == scroller.Offset.Y) return;
        scroller.Offset = new Vector(scroller.Offset.X, offset);
        _list.UpdateLayout();
        UpdateIndicator();
    }

    // The realized rows' tops and heights in list coordinates, with the index of the first one.
    private (int First, List<(double Top, double Height)> Rows) RealizedRows()
    {
        var rows = new List<(double, double)>();
        int first = -1;
        for (int i = 0; i < _list.ItemCount; i++)
        {
            if (_list.ContainerFromIndex(i) is not Control container || container.TranslatePoint(default, _list) is not { } origin)
            {
                if (first >= 0) break;
                continue;
            }
            if (first < 0) first = i;
            rows.Add((origin.Y, container.Bounds.Height));
        }
        return (Math.Max(first, 0), rows);
    }

    private int SlotAt(double y)
    {
        var (first, rows) = RealizedRows();
        return rows.Count == 0 ? 0 : first + SequenceReorder.SlotAt(rows, y);
    }

    private void UpdateIndicator()
    {
        int slot = SlotAt(_lastPoint.Y);
        if (SequenceReorder.Plan(_list.ItemCount, _moved, slot) is null)
        {
            _hideLine();
            return;
        }
        var (first, rows) = RealizedRows();
        int index = slot - first;
        // The gap is above the first row drawn or below the last one realized: scrolled out of sight, so no line.
        if (rows.Count == 0 || index < 0 || index > rows.Count)
        {
            _hideLine();
            return;
        }
        double y = index < rows.Count ? rows[index].Top : rows[^1].Top + rows[^1].Height;
        if (y < -1 || y > _list.Bounds.Height + 1 || _toOverlay(new Point(0, y)) is not { } origin
            || _toOverlay(new Point(_list.Bounds.Width, y)) is not { } end)
        {
            _hideLine();
            return;
        }
        _showLine(origin.X, end.X, origin.Y);
    }

    private void Reset()
    {
        _pressRow = -1;
        _collapseOnRelease = false;
    }

    private void End()
    {
        bool wasDragging = _dragging;
        var pointer = _pointer;
        _dragging = false;
        _pointer = null;
        Reset();
        _moved = new List<int>();
        _scrollTimer?.Stop();
        _scrollTimer = null;
        foreach (var container in _dimmed) container.Opacity = 1;
        _dimmed.Clear();
        if (!wasDragging) return;
        _hideLine();
        DragCursors.Restore(_list, null);
        pointer?.Capture(null);
    }
}
