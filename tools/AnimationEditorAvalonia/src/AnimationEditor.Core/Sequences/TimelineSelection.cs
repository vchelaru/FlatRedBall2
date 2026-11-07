using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>A chain on the timeline: its track, and its index in that track's chain list.</summary>
public readonly record struct TimelineChainId(int Track, int Chain);

public enum TimelineSelectionKind { None, Chains, Tracks }

/// <summary>
/// What the timeline has selected: a set of chains, or a set of track headers, never both. View state only, so none of
/// it is an undo entry. One item is the <b>primary</b>: the one last clicked, which a Shift click extends a range from,
/// the Inspector shows when the set holds one item, and the navigation history records. Chains are kept in document
/// order (track, then index in the track's list). Every operation returns whether the selection changed.
/// </summary>
public sealed class TimelineSelection
{
    private readonly List<TimelineChainId> _chains = new();
    private readonly List<int> _tracks = new();
    // The same chains and tracks, for Contains: a timeline of thousands of chains asks once per chain on every draw.
    private readonly HashSet<TimelineChainId> _chainSet = new();
    private readonly HashSet<int> _trackSet = new();
    private TimelineChainId? _primaryChain;
    private int? _primaryTrack;

    public TimelineSelectionKind Kind =>
        _chains.Count > 0 ? TimelineSelectionKind.Chains : _tracks.Count > 0 ? TimelineSelectionKind.Tracks : TimelineSelectionKind.None;

    public IReadOnlyList<TimelineChainId> Chains => _chains;

    public IReadOnlyList<int> Tracks => _tracks;

    public TimelineChainId? PrimaryChain => _primaryChain;

    public int? PrimaryTrack => _primaryTrack;

    /// <summary>How many chains or tracks are selected.</summary>
    public int Count => _chains.Count + _tracks.Count;

    /// <summary>True when more than one chain or track is selected.</summary>
    public bool IsMulti => Count > 1;

    public bool Contains(TimelineChainId id) => _chainSet.Contains(id);

    public bool ContainsTrack(int track) => _trackSet.Contains(track);

    public bool Clear()
    {
        if (Count == 0) return false;
        _chains.Clear();
        _tracks.Clear();
        _chainSet.Clear();
        _trackSet.Clear();
        _primaryChain = null;
        _primaryTrack = null;
        return true;
    }

    /// <summary>A plain click on a chain: it becomes the only selected item.</summary>
    public bool SelectChain(TimelineChainId id) => ReplaceChains(new[] { id }, id);

    /// <summary>Ctrl+click on a chain: adds it, or removes it when already selected.</summary>
    public bool ToggleChain(TimelineChainId id)
    {
        var next = _chains.ToList();
        if (!next.Remove(id)) next.Add(id);
        return ReplaceChains(next, next.Contains(id) ? id : next.Count > 0 ? next[^1] : null);
    }

    /// <summary>
    /// Shift+click on a chain: selects every chain of its track from the primary chain to it, by start time. With no
    /// primary chain, or one on another track, it selects just the clicked chain.
    /// </summary>
    public bool ExtendChainsTo(TimelineChainId id, IReadOnlyList<TimelineBlock> blocks)
    {
        if (_primaryChain is not { } anchor || anchor.Track != id.Track || Kind != TimelineSelectionKind.Chains)
            return SelectChain(id);
        var order = blocks.Where(block => block.Track == id.Track)
            .OrderBy(block => block.Start).ThenBy(block => block.Chain)
            .Select(block => new TimelineChainId(block.Track, block.Chain)).ToList();
        int from = order.IndexOf(anchor), to = order.IndexOf(id);
        if (from < 0 || to < 0) return SelectChain(id);
        var range = order.Skip(Math.Min(from, to)).Take(Math.Abs(to - from) + 1);
        return ReplaceChains(range, anchor);
    }

    /// <summary>Replaces the selection with <paramref name="chains"/>; the primary is <paramref name="primary"/> when given and selected, else the last one. An empty set clears.</summary>
    public bool SelectChains(IEnumerable<TimelineChainId> chains, TimelineChainId? primary = null)
    {
        var set = chains.ToList();
        return ReplaceChains(set, primary is { } p && set.Contains(p) ? p : null);
    }

    /// <summary>Replaces the selection with the track headers <paramref name="tracks"/>. An empty set clears.</summary>
    public bool SelectTracks(IEnumerable<int> tracks)
    {
        var set = tracks.ToList();
        return Replace(null, null, set, null);
    }

    /// <summary>Makes an already selected chain the primary one, as pressing on it to drag the whole selection does. False when it is not selected or already is.</summary>
    public bool MakePrimary(TimelineChainId id)
    {
        if (!_chainSet.Contains(id) || _primaryChain == id) return false;
        _primaryChain = id;
        return true;
    }

    /// <summary>A plain click on a track header.</summary>
    public bool SelectTrack(int track) => Replace(null, null, new[] { track }, track);

    /// <summary>Ctrl+click on a track header.</summary>
    public bool ToggleTrack(int track)
    {
        var next = _tracks.ToList();
        if (!next.Remove(track)) next.Add(track);
        return Replace(null, null, next, next.Contains(track) ? track : next.Count > 0 ? next[^1] : null);
    }

    /// <summary>Shift+click on a track header: the tracks from the primary track to it. With no primary track, just the clicked one.</summary>
    public bool ExtendTracksTo(int track)
    {
        if (_primaryTrack is not { } anchor || Kind != TimelineSelectionKind.Tracks) return SelectTrack(track);
        var range = Enumerable.Range(Math.Min(anchor, track), Math.Abs(track - anchor) + 1);
        return Replace(null, null, range, anchor);
    }

    /// <summary>
    /// Ctrl+A. With tracks selected, every track. With chains selected, the chains of the primary chain's track, and once
    /// those already are all selected, every chain of the sequence. With nothing selected, every chain.
    /// </summary>
    public bool SelectAll(IReadOnlyList<TimelineBlock> blocks, int trackCount)
    {
        if (Kind == TimelineSelectionKind.Tracks)
            return Replace(null, null, Enumerable.Range(0, trackCount), _primaryTrack);

        var all = blocks.Select(block => new TimelineChainId(block.Track, block.Chain)).ToList();
        IReadOnlyList<TimelineChainId> take = all;
        if (_primaryChain is { } primary)
        {
            var onTrack = all.Where(id => id.Track == primary.Track).ToList();
            if (!onTrack.All(_chains.Contains)) take = onTrack;
        }
        if (take.Count == 0) return false;
        return ReplaceChains(take, _primaryChain is { } keep && take.Contains(keep) ? keep : take[0]);
    }

    /// <summary>
    /// Drops every chain for which <paramref name="exists"/> is false and every track past <paramref name="trackCount"/>,
    /// after the timeline was rebuilt. The primary moves to a remaining item when it went.
    /// </summary>
    public bool Retain(Func<TimelineChainId, bool> exists, int trackCount)
    {
        var chains = _chains.Where(exists).ToList();
        var tracks = _tracks.Where(track => track >= 0 && track < trackCount).ToList();
        if (chains.Count == _chains.Count && tracks.Count == _tracks.Count) return false;
        return Replace(chains, _primaryChain is { } c && chains.Contains(c) ? c : null, tracks, _primaryTrack is { } t && tracks.Contains(t) ? t : null);
    }

    /// <summary>
    /// After <paramref name="removed"/> chains left the document: removes them from the selection and shifts the index of
    /// every selected chain that sat after a removed one on its track.
    /// </summary>
    public void RemapAfterRemovingChains(IReadOnlyList<TimelineChainId> removed)
    {
        TimelineChainId? Shift(TimelineChainId id) =>
            removed.Contains(id) ? null : id with { Chain = id.Chain - removed.Count(r => r.Track == id.Track && r.Chain < id.Chain) };

        var chains = _chains.Select(Shift).OfType<TimelineChainId>().ToList();
        TimelineChainId? primary = _primaryChain is { } p ? Shift(p) : null;
        ReplaceChains(chains, primary ?? (chains.Count > 0 ? chains[^1] : null));
    }

    private bool ReplaceChains(IEnumerable<TimelineChainId> chains, TimelineChainId? primaryChain) =>
        Replace(chains, primaryChain, null, null);

    private bool Replace(IEnumerable<TimelineChainId>? chains, TimelineChainId? primaryChain, IEnumerable<int>? tracks, int? primaryTrack)
    {
        var nextChains = (chains ?? Array.Empty<TimelineChainId>()).Distinct()
            .OrderBy(id => id.Track).ThenBy(id => id.Chain).ToList();
        var nextTracks = (tracks ?? Array.Empty<int>()).Distinct().OrderBy(track => track).ToList();
        // A set holds chains or tracks; if both were handed over, chains win.
        if (nextChains.Count > 0) nextTracks.Clear();
        if (nextChains.Count == 0) primaryChain = null;
        if (nextTracks.Count == 0) primaryTrack = null;
        if (primaryChain is null && nextChains.Count > 0) primaryChain = nextChains[^1];
        if (primaryTrack is null && nextTracks.Count > 0) primaryTrack = nextTracks[^1];

        bool same = nextChains.SequenceEqual(_chains) && nextTracks.SequenceEqual(_tracks)
            && primaryChain == _primaryChain && primaryTrack == _primaryTrack;
        if (same) return false;
        _chains.Clear();
        _chains.AddRange(nextChains);
        _chainSet.Clear();
        _chainSet.UnionWith(nextChains);
        _tracks.Clear();
        _tracks.AddRange(nextTracks);
        _trackSet.Clear();
        _trackSet.UnionWith(nextTracks);
        _primaryChain = primaryChain;
        _primaryTrack = primaryTrack;
        return true;
    }
}

/// <summary>The chains a rubber-band rectangle dragged over empty lane space touches.</summary>
public static class TimelineMarquee
{
    /// <summary>
    /// Every chain whose drawn body overlaps the rectangle between (<paramref name="x1"/>, <paramref name="y1"/>) and
    /// (<paramref name="x2"/>, <paramref name="y2"/>), in control coordinates and in any corner order.
    /// </summary>
    public static IReadOnlyList<TimelineChainId> Chains(TimelineViewport viewport, IReadOnlyList<TimelineBlock> blocks,
        double x1, double y1, double x2, double y2)
    {
        double left = Math.Min(x1, x2), right = Math.Max(x1, x2);
        double top = Math.Min(y1, y2), bottom = Math.Max(y1, y2);
        var hit = new List<TimelineChainId>();
        foreach (var block in blocks)
        {
            double blockLeft = viewport.XAt(block.Start);
            double blockRight = Math.Max(blockLeft + 1, viewport.XAt(block.PlayedEnd));
            double blockTop = viewport.LaneTop(block.Track) + TimelineLayout.BodyTop;
            double blockBottom = viewport.LaneTop(block.Track) + TimelineLayout.BodyBottom;
            if (blockLeft <= right && blockRight >= left && blockTop <= bottom && blockBottom >= top)
                hit.Add(new TimelineChainId(block.Track, block.Chain));
        }
        return hit;
    }
}
