using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Applies a rubber band to a <see cref="TimelineSelection"/> on every pointer move without doing work that grows with the
/// number of chains on the timeline or already selected. The blocks of a track are in playback order, where each one
/// plays up to the next one's start, so their time spans are sorted and never overlap: the chains the band touches on a
/// track are one run, found by binary search. A move that touches the same chains as the last one changes nothing, and says so.
/// </summary>
public sealed class TimelineMarqueeSelector
{
    private readonly List<TimelineChainId> _base = new();
    private readonly HashSet<TimelineChainId> _baseSet = new();
    private readonly List<TimelineChainId> _touched = new();

    /// <summary>How many blocks the last <see cref="Update"/> looked at. It grows with the chains the band touches, not with the timeline.</summary>
    public int LastExamined { get; private set; }

    /// <summary>Starts a band. <paramref name="baseSelection"/> stays selected while it is dragged (a Ctrl band adds to what was selected).</summary>
    public void Begin(IEnumerable<TimelineChainId> baseSelection)
    {
        _base.Clear();
        _baseSet.Clear();
        foreach (var id in baseSelection)
            if (_baseSet.Add(id)) _base.Add(id);
    }

    /// <summary>
    /// Makes <paramref name="selection"/> the base plus the chains the rectangle between the two corners touches (in control
    /// coordinates, any corner order). Returns whether the selection changed; false, with nothing touched, when the
    /// rectangle touches the same chains as before.
    /// </summary>
    /// <param name="trackStarts">From <see cref="TimelineLayout.TrackStarts"/> for <paramref name="blocks"/>.</param>
    public bool Update(TimelineSelection selection, TimelineViewport viewport, IReadOnlyList<TimelineBlock> blocks, IReadOnlyList<int> trackStarts,
        double x1, double y1, double x2, double y2)
    {
        Collect(viewport, blocks, trackStarts, Math.Min(x1, x2), Math.Max(x1, x2), Math.Min(y1, y2), Math.Max(y1, y2));

        int expected = _base.Count;
        foreach (var id in _touched)
            if (!_baseSet.Contains(id)) expected++;
        if (selection.Count == expected && selection.Kind != TimelineSelectionKind.Tracks
            && _base.All(selection.Contains) && _touched.All(selection.Contains))
            return false;
        return selection.SelectChains(_base.Concat(_touched));
    }

    private void Collect(TimelineViewport viewport, IReadOnlyList<TimelineBlock> blocks, IReadOnlyList<int> trackStarts,
        double left, double right, double top, double bottom)
    {
        _touched.Clear();
        LastExamined = 0;
        double fromTime = viewport.TimeAt(left), toTime = viewport.TimeAt(right);
        // A block is drawn at least a pixel wide, so a zero-length one still has a span to touch.
        double minSpan = 1 / viewport.PixelsPerSecond;
        for (int track = 0; track + 1 < trackStarts.Count; track++)
        {
            double laneTop = viewport.LaneTop(track);
            if (laneTop + TimelineLayout.BodyTop > bottom || laneTop + TimelineLayout.BodyBottom < top) continue;

            int first = trackStarts[track], end = trackStarts[track + 1];
            // The first block that ends at or after the left edge, and the first that starts after the right edge.
            int lo = Partition(blocks, first, end, block => Math.Max(block.Start + minSpan, block.PlayedEnd) >= fromTime);
            int hi = Partition(blocks, lo, end, block => block.Start > toTime);
            for (int i = lo; i < hi; i++)
                _touched.Add(new TimelineChainId(blocks[i].Track, blocks[i].Chain));
            LastExamined += hi - lo;
        }
    }

    // The first index in [from, to) where the predicate holds, given it is false and then true along the range.
    private int Partition(IReadOnlyList<TimelineBlock> blocks, int from, int to, Func<TimelineBlock, bool> predicate)
    {
        int low = from, high = to;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            LastExamined++;
            if (predicate(blocks[mid])) high = mid;
            else low = mid + 1;
        }
        return low;
    }
}

/// <summary>
/// Lets a stream of selection changes through at most once per interval, and says whether one was held back so the caller
/// can raise it when the stream ends. A rubber band changes the selection on many moves in a row; what listens to the
/// selection (the Inspector) only needs the latest, not every step.
/// </summary>
public sealed class SelectionChangeThrottle
{
    private readonly long _interval;
    private long _lastRaised;
    private bool _raisedBefore;
    private bool _pending;

    public SelectionChangeThrottle(long intervalTicks) => _interval = intervalTicks;

    /// <summary>A change happened at <paramref name="nowTicks"/>. True when it should be raised now; false when it is held for <see cref="Flush"/>.</summary>
    public bool Request(long nowTicks)
    {
        if (_raisedBefore && nowTicks - _lastRaised < _interval)
        {
            _pending = true;
            return false;
        }
        _raisedBefore = true;
        _lastRaised = nowTicks;
        _pending = false;
        return true;
    }

    /// <summary>The stream ended. True when a change was held back and has not been raised yet.</summary>
    public bool Flush()
    {
        bool held = _pending;
        _pending = false;
        return held;
    }

    /// <summary>Forgets everything, for the start of the next stream.</summary>
    public void Reset()
    {
        _raisedBefore = false;
        _pending = false;
    }
}
