using System;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Value signature of everything the timeline strip's cells are built from: the source chain's
/// identity plus, per frame, its width driver (<see cref="AnimationFrameSave.FrameLength"/>) and
/// thumbnail signature. Two equal signatures yield an identical strip — same cell count, widths,
/// and thumbnails — so <c>RefreshTimelineStrip</c> can skip the clear-and-rebuild and only move
/// the playhead/highlight. A scrub that crosses a frame boundary changes only the selection, not
/// the structure, so the signature stays equal and the costly rebuild (per-frame Skia thumbnail
/// regeneration plus playhead-VM teardown) is skipped (#452).
/// </summary>
public sealed class TimelineStripSignature : IEquatable<TimelineStripSignature>
{
    // Reference identity, not value identity: selecting a different chain object rebuilds even
    // when frame data matches. Held only for comparison — never dereferenced.
    private readonly object? _chain;
    private readonly (float FrameLength, ThumbnailSource Thumbnail)[] _frames;

    private TimelineStripSignature(object? chain, (float, ThumbnailSource)[] frames)
    {
        _chain = chain;
        _frames = frames;
    }

    public static TimelineStripSignature From(AnimationChainSave? chain)
    {
        if (chain is null)
            return new TimelineStripSignature(null, Array.Empty<(float, ThumbnailSource)>());

        var frames = new (float, ThumbnailSource)[chain.Frames.Count];
        for (int i = 0; i < chain.Frames.Count; i++)
        {
            var frame = chain.Frames[i];
            frames[i] = (frame.FrameLength, ThumbnailSource.FromFrame(frame));
        }
        return new TimelineStripSignature(chain, frames);
    }

    public bool Equals(TimelineStripSignature? other)
    {
        if (other is null) return false;
        if (!ReferenceEquals(_chain, other._chain)) return false;
        if (_frames.Length != other._frames.Length) return false;
        for (int i = 0; i < _frames.Length; i++)
            if (!_frames[i].Equals(other._frames[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as TimelineStripSignature);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_chain);
        foreach (var frame in _frames)
            hash.Add(frame);
        return hash.ToHashCode();
    }
}
