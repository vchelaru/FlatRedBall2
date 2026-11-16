using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>A stretch of an inherited track, in seconds of the referenced sequence's own clock.</summary>
public sealed record InheritedSpan(double Start, double Length);

/// <summary>
/// One track the referenced sequence brings in, as the expanded reference lane lists it: read-only. A track of the target
/// itself shows its chains' spans; one that comes through a reference inside the target shows that clip's span.
/// </summary>
/// <param name="Name">The name relative to the target (<c>Body</c>, or <c>Dust/Body</c> when it comes through another reference).</param>
/// <param name="Visible">Whether the track and the references leading to it are visible.</param>
/// <param name="Spans">Where it plays in the target's first cycle.</param>
public sealed record InheritedRow(string Name, bool Visible, IReadOnlyList<InheritedSpan> Spans);

/// <summary>
/// A reference track as the timeline draws it: one clip, <see cref="Start"/> to <see cref="End"/>, with the inherited tracks
/// listed under it when the lane is expanded. A reference is not cut by anything, so there is no cut tail.
/// </summary>
/// <param name="Track">The lane.</param>
/// <param name="Reference">The clip, edited in place by a drag.</param>
/// <param name="TargetLength">The referenced sequence's length in seconds; 0 when it is missing or empty.</param>
/// <param name="IsMissing">Whether no sequence has the referenced name.</param>
/// <param name="Rows">The inherited tracks.</param>
/// <param name="Cycle">The names around the cycle this reference closes (<c>A, B, A</c>), or null. A cyclic reference plays
/// nothing (<see cref="SequenceReferences.Plays"/>), so it has no target length and no rows.</param>
public sealed record TimelineReference(int Track, SequenceReferenceSave Reference, double TargetLength, bool IsMissing, IReadOnlyList<InheritedRow> Rows,
    IReadOnlyList<string>? Cycle = null)
{
    /// <summary>Whether the clip plays nothing: its target is missing or plays this sequence back. Drawn hatched with a warning.</summary>
    public bool IsBroken => IsMissing || Cycle is not null;

    public double Start => Reference.Start;

    /// <summary>The clip's length: its cycles of the target, or <see cref="TimelineLayout.MissingLength"/> for a broken target or one with no length.</summary>
    public double Length => TargetLength > 0 ? SequenceTimeline.FullLength(Reference.Loops, TargetLength) : TimelineLayout.MissingLength;

    public double End => Start + Length;

    /// <summary>Whether dragging the right edge changes the cycles. A broken or empty target has no cycle to count.</summary>
    public bool CanResize => !IsBroken && TargetLength > 0;

    /// <summary>Whether there is anything to show when the lane is expanded.</summary>
    public bool Expandable => Rows.Count > 0;
}

/// <summary>What a pointer lands on in a reference clip.</summary>
public readonly record struct TimelineReferenceHit(TimelineReference Reference, TimelineHitPart Part);

/// <summary>Where the sequence timeline draws reference tracks.</summary>
public static class TimelineReferences
{
    /// <summary>The height of one inherited track in an expanded lane.</summary>
    public const double InheritedRowHeight = 16;

    /// <summary>Padding under the clip band before the inherited rows start.</summary>
    public const double InheritedTop = TimelineLayout.BodyBottom + 4;

    /// <summary>The lane height a reference adds when expanded.</summary>
    public static double ExtraHeight(TimelineReference reference, bool expanded) =>
        expanded && reference.Expandable ? InheritedTop + reference.Rows.Count * InheritedRowHeight + 4 - TimelineViewport.LaneHeight : 0;

    /// <summary>
    /// Every reference track of <paramref name="sequence"/> as a clip. <paramref name="file"/> is the file's sequences, which the
    /// references resolve against; <paramref name="cycleLength"/> returns a chain reference's cycle in seconds, or null when
    /// its chain is missing.
    /// </summary>
    public static IReadOnlyList<TimelineReference> For(SequenceSave sequence, IReadOnlyList<SequenceSave> file,
        Func<SequenceChainReferenceSave, double?> cycleLength)
    {
        double LengthOf(SequenceSave s) => SequenceTimeline.Length(s, reference => cycleLength(reference) ?? 0);
        var result = new List<TimelineReference>();
        for (int t = 0; t < sequence.Tracks.Count; t++)
        {
            if (sequence.Tracks[t].Reference is not { } reference) continue;
            var target = SequenceReferences.Find(file, reference.Sequence);
            if (target is null)
            {
                result.Add(new TimelineReference(t, reference, 0, true, Array.Empty<InheritedRow>()));
                continue;
            }
            if (SequenceReferences.CycleThrough(file, sequence.Name, target.Name) is { } cycle)
            {
                result.Add(new TimelineReference(t, reference, 0, false, Array.Empty<InheritedRow>(), cycle));
                continue;
            }
            var blocks = TimelineLayout.Blocks(target, cycleLength);
            var rows = new List<InheritedRow>();
            foreach (var inlined in SequenceReferences.Inline(file, target))
            {
                IReadOnlyList<InheritedSpan> spans = inlined.Hops.Count == 0
                    ? blocks.Where(block => ReferenceEquals(target.Tracks[block.Track], inlined.Track))
                        .Select(block => new InheritedSpan(block.Start, block.Played)).ToList()
                    : new[]
                    {
                        new InheritedSpan(inlined.Hops[0].Reference.Start,
                            SequenceTimeline.FullLength(inlined.Hops[0].Reference.Loops, LengthOf(inlined.Hops[0].Target))),
                    };
                rows.Add(new InheritedRow(inlined.Name, inlined.Visible, spans));
            }
            result.Add(new TimelineReference(t, reference, LengthOf(target), false, rows));
        }
        return result;
    }

    /// <summary>
    /// The clip under (<paramref name="x"/>, <paramref name="y"/>) and whether the point is on its resize edge. Only the clip
    /// band of the lane counts: the inherited rows under it are read-only.
    /// </summary>
    public static TimelineReferenceHit? HitTest(TimelineViewport viewport, IReadOnlyList<TimelineReference> references, int trackCount, double x, double y)
    {
        var row = viewport.RowAt(y, trackCount);
        if (row.Kind != TimelineRowKind.Track) return null;
        double inLane = y - viewport.LaneTop(row.Track);
        if (inLane < TimelineLayout.BodyTop || inLane > TimelineLayout.BodyBottom) return null;
        foreach (var reference in references.Where(r => r.Track == row.Track))
        {
            double left = viewport.XAt(reference.Start), right = viewport.XAt(reference.End);
            double grab = Math.Min(TimelineLayout.EdgeGrabPixels, (right - left) / 3);
            if (reference.CanResize && x >= right - grab && x <= right + TimelineLayout.EdgeGrabPixels)
                return new TimelineReferenceHit(reference, TimelineHitPart.Edge);
            if (x >= left && x <= right) return new TimelineReferenceHit(reference, TimelineHitPart.Body);
        }
        return null;
    }
}
