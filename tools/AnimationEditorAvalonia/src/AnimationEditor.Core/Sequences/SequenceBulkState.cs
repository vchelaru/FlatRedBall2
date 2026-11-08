using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Sequences;

/// <summary>What several selected tracks share; a null member means they disagree ("(mixed)" in the Inspector).</summary>
public sealed record TrackBulkState(bool? Visible, bool? Flip);

/// <summary>What several selected chains share; a null member means they disagree.</summary>
public sealed record ChainBulkState(
    string? Source, string? Chain, double? Start, float? Loops);

/// <summary>The "(mixed)" detection for the sequence Inspector's multi-selection, as the .achx Inspector does for frames.</summary>
public static class SequenceBulkState
{
    public static TrackBulkState ForTracks(SequenceSave sequence, IReadOnlyList<int> tracks)
    {
        var selected = tracks.Select(index => sequence.Tracks[index]).ToList();
        return new TrackBulkState(
            Shared(selected.Select(t => t.Visible)), Shared(selected.Select(t => t.FlipHorizontal)));
    }

    public static ChainBulkState ForChains(SequenceSave sequence, IReadOnlyList<TimelineChainId> chains)
    {
        var selected = chains.Select(id => sequence.Tracks[id.Track].Chains[id.Chain]).ToList();
        return new ChainBulkState(
            Shared(selected.Select(c => c.Source)), Shared(selected.Select(c => c.Chain)),
            Shared(selected.Select(c => c.Start)), Shared(selected.Select(c => c.Loops)));
    }

    // The value every item has, or null when they differ (or there are none).
    private static T? Shared<T>(IEnumerable<T> values) where T : struct
    {
        var list = values.Distinct().Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }

    private static string? Shared(IEnumerable<string> values)
    {
        var list = values.Distinct().Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }
}
