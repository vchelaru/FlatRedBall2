using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Sequences;

/// <summary>What several selected tracks share; a null member means they disagree ("(mixed)" in the Inspector).</summary>
public sealed record TrackBulkState(bool? Visible);

/// <summary>What several selected chains share; a null member means they disagree.</summary>
public sealed record ChainBulkState(
    string? Source, string? Chain, double? Start, float? Loops);

/// <summary>The "(mixed)" detection for the sequence Inspector's multi-selection, as the .achx Inspector does for frames.</summary>
public static class SequenceBulkState
{
    public static TrackBulkState ForTracks(SequenceSave sequence, IReadOnlyList<int> tracks)
    {
        var selected = tracks.Select(index => sequence.Tracks[index]).ToList();
        return new TrackBulkState(MixedState.Common(selected.Select(t => t.Visible)));
    }

    public static ChainBulkState ForChains(SequenceSave sequence, IReadOnlyList<TimelineChainId> chains)
    {
        var selected = chains.Select(id => sequence.Tracks[id.Track].Chains[id.Chain]).ToList();
        return new ChainBulkState(
            MixedState.Common(selected.Select(c => c.Source)), MixedState.Common(selected.Select(c => c.Chain)),
            MixedState.Common(selected.Select(c => c.Start)), MixedState.Common(selected.Select(c => c.Loops)));
    }
}
