using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Drag-to-reorder for the library's source groups, a sibling of <see cref="SequenceListDragController"/> (one group, no
/// selection, a scrolling panel of unequal heights). A press on a group's header arms it; past the drag threshold the
/// pointer is captured and an insertion line follows the gap the pointer points at (<see cref="SequenceReorder"/>).
/// Release drops, Escape or a lost capture cancels. A press that never becomes a drag is a click on the header, which
/// collapses or expands the group on release; a drag never does. Presses on a chip or a header button never arm it.
/// </summary>
internal sealed class LibraryGroupDragController
{
    private const double DragThreshold = 4;
    private const double AutoScrollEdge = 24;
    private const double AutoScrollMaxStep = 18;
    private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(40);

    private readonly SequenceLibraryControl _library;
    private readonly Panel _groups;
    private readonly Action<int, int> _drop;
    private readonly Action<int> _click;
    private Action<double, double, double> _showLine = static (_, _, _) => { };
    private Action _hideLine = static () => { };
    private Func<Point, Point?> _toOverlay = static _ => null;

    private int _pressIndex = -1;
    private Point _pressPoint;
    private bool _dragging;
    private IPointer? _pointer;
    private Point _lastInScroller;
    private Control? _dimmed;
    private DispatcherTimer? _scrollTimer;
    private TopLevel? _topLevel;

    /// <param name="library">The library whose groups reorder.</param>
    /// <param name="drop">Moves source <c>index</c> to a gap (see <see cref="SequenceReorder"/>).</param>
    /// <param name="click">A press and release on header <c>index</c> that never became a drag.</param>
    public LibraryGroupDragController(SequenceLibraryControl library, Action<int, int> drop, Action<int> click)
    {
        _library = library;
        _groups = library.GroupsPanel;
        _drop = drop;
        _click = click;
        _groups.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        _groups.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _groups.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        _groups.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// Connects the insertion line: <paramref name="showLine"/> draws it at (left, right, y) in the drag overlay's
    /// coordinates, <paramref name="hideLine"/> removes it, and <paramref name="toOverlay"/> maps a point of the groups
    /// panel to the overlay.
    /// </summary>
    public void ConnectDropLine(Action<double, double, double> showLine, Action hideLine, Func<Point, Point?> toOverlay)
    {
        _showLine = showLine;
        _hideLine = hideLine;
        _toOverlay = toOverlay;
    }

    /// <summary>True while a group is being dragged (past the threshold, not yet dropped or cancelled).</summary>
    public bool IsDragging => _dragging;

    /// <summary>Abandons a drag or a pending press without dropping or clicking: the groups are being rebuilt under it.</summary>
    public void Cancel()
    {
        if (_dragging) End();
        else _pressIndex = -1;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressIndex = -1;
        if (!e.GetCurrentPoint(_groups).Properties.IsLeftButtonPressed) return;
        int index = _library.HeaderIndexAt(e.Source as Visual);
        if (index < 0) return;
        _pressIndex = index;
        _pressPoint = e.GetPosition(_groups);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressIndex < 0) return;
        if (!e.GetCurrentPoint(_groups).Properties.IsLeftButtonPressed)
        {
            End();
            return;
        }
        var point = e.GetPosition(_groups);
        if (!_dragging)
        {
            if (Math.Abs(point.X - _pressPoint.X) < DragThreshold && Math.Abs(point.Y - _pressPoint.Y) < DragThreshold) return;
            Begin(e);
        }
        _lastInScroller = _library.Scroller is { } scroller ? e.GetPosition(scroller) : point;
        UpdateIndicator();
        DragCursors.Apply(_groups, TimelineCursor.Reorder);
    }

    private void Begin(PointerEventArgs e)
    {
        _dragging = true;
        _pointer = e.Pointer;
        e.Pointer.Capture(_groups);
        if (_pressIndex < _groups.Children.Count)
        {
            _dimmed = _groups.Children[_pressIndex];
            _dimmed.Opacity = 0.6;
        }
        _scrollTimer = new DispatcherTimer { Interval = AutoScrollInterval };
        _scrollTimer.Tick += (_, _) => AutoScroll();
        _scrollTimer.Start();
        // Escape reaches us whatever holds the focus.
        _topLevel = TopLevel.GetTopLevel(_groups);
        _topLevel?.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressIndex < 0) return;
        int index = _pressIndex;
        if (_dragging)
        {
            int slot = SlotAt(ContentY(e));
            End();
            _drop(index, slot);
            return;
        }
        End();
        _click(index);
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

    // The pointer's y in the groups panel's coordinates, which stay put while the panel scrolls under it.
    private double ContentY(PointerEventArgs e) => e.GetPosition(_groups).Y;

    private double ContentY() =>
        _library.Scroller is { } scroller && scroller.TranslatePoint(_lastInScroller, _groups) is { } point ? point.Y : _lastInScroller.Y;

    private void AutoScroll()
    {
        if (!_dragging || _library.Scroller is not { } scroller) return;
        double delta = SequenceReorder.AutoScrollDelta(_lastInScroller.Y, scroller.Viewport.Height, AutoScrollEdge, AutoScrollMaxStep);
        if (delta == 0) return;
        double max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        double offset = Math.Clamp(scroller.Offset.Y + delta, 0, max);
        if (offset == scroller.Offset.Y) return;
        scroller.Offset = new Vector(scroller.Offset.X, offset);
        scroller.UpdateLayout();
        UpdateIndicator();
    }

    private int SlotAt(double y)
    {
        var rows = new List<(double Top, double Height)>();
        foreach (var child in _groups.Children) rows.Add((child.Bounds.Top, child.Bounds.Height));
        return SequenceReorder.SlotAt(rows, y);
    }

    private void UpdateIndicator()
    {
        int slot = SlotAt(ContentY());
        if (SequenceReorder.Plan(_groups.Children.Count, new[] { _pressIndex }, slot) is null)
        {
            _hideLine();
            return;
        }
        var children = _groups.Children;
        // The middle of the space between two groups, or the outer edge of the first or last.
        double y = slot == 0 ? children[0].Bounds.Top
            : slot >= children.Count ? children[^1].Bounds.Bottom
            : (children[slot - 1].Bounds.Bottom + children[slot].Bounds.Top) / 2;
        // A gap scrolled out of the visible panel shows no line.
        if (_library.Scroller is { } scroller && _groups.TranslatePoint(new Point(0, y), scroller) is { } inScroller
            && (inScroller.Y < -1 || inScroller.Y > scroller.Bounds.Height + 1))
        {
            _hideLine();
            return;
        }
        if (_toOverlay(new Point(0, y)) is not { } origin || _toOverlay(new Point(_groups.Bounds.Width, y)) is not { } end)
        {
            _hideLine();
            return;
        }
        _showLine(origin.X, end.X, origin.Y);
    }

    private void End()
    {
        bool wasDragging = _dragging;
        var pointer = _pointer;
        _dragging = false;
        _pointer = null;
        _pressIndex = -1;
        _scrollTimer?.Stop();
        _scrollTimer = null;
        if (_dimmed is not null) _dimmed.Opacity = 1;
        _dimmed = null;
        _topLevel?.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _topLevel = null;
        if (!wasDragging) return;
        _hideLine();
        DragCursors.Restore(_groups, null);
        pointer?.Capture(null);
    }
}
