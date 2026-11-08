using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>Where a library chip is added when it is not dragged.</summary>
public enum ChipAddMode
{
    /// <summary>On a new track in front (the top of the list), like a drop on the dashed new-track row.</summary>
    NewTrack,

    /// <summary>On the one track the timeline selection points at.</summary>
    SelectedTrack,
}

/// <summary>
/// What adding a library chip without a drag (double-click, Enter, the chip's menu) does: the target track and the start
/// time. The drag drop reads its own plan from <see cref="TimelineDropPlan"/>; both end in
/// <see cref="SequenceDocument.PlaceChain"/>, so the undo entry is the same.
/// </summary>
/// <param name="CanAdd">False when the mode has no target; <paramref name="Refusal"/> says why.</param>
/// <param name="Track">The existing track to add to, or null for a new track.</param>
/// <param name="Start">The chain's start in seconds: the playhead.</param>
/// <param name="Refusal">Why nothing is added, or null.</param>
public readonly record struct ChipAddPlan(bool CanAdd, int? Track, double Start, string? Refusal)
{
    public const string NeedsOneTrackMessage = "Select a single track first (or a chain on it) to add the chain to that track.";

    /// <summary>
    /// A new track starts at <paramref name="playhead"/> and ignores the selection. <see cref="ChipAddMode.SelectedTrack"/>
    /// needs the selection to name exactly one track: one track header, or chains that all sit on the same track.
    /// </summary>
    public static ChipAddPlan For(ChipAddMode mode, IReadOnlyList<TimelineChainId> selectedChains, IReadOnlyList<int> selectedTracks, double playhead)
    {
        double start = Math.Max(0, playhead);
        if (mode == ChipAddMode.NewTrack) return new ChipAddPlan(true, null, start, null);
        var tracks = selectedChains.Select(chain => chain.Track).Concat(selectedTracks).Distinct().ToList();
        return tracks.Count == 1
            ? new ChipAddPlan(true, tracks[0], start, null)
            : new ChipAddPlan(false, null, start, NeedsOneTrackMessage);
    }
}
