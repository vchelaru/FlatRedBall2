using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Moves chains out of their tracks into new tracks: what dropping a dragged chain (or selection) on the new-track row
/// does. Edits the sequence in place, so it runs inside a gesture that commits it as one undo entry.
/// </summary>
public static class TimelineNewTrackMove
{
    private const string DefaultTrackName = "Track";

    /// <summary>
    /// Takes <paramref name="chains"/> out of the tracks they are in and appends one new track per source track, in lane
    /// order, each holding that track's chains in their old order. A new track is named after its first chain (numbered
    /// when taken; "Track" for a chain with no name), exactly as a chip dropped on the row names it. A source track
    /// left empty stays. Returns the new tracks' indices.
    /// </summary>
    public static IReadOnlyList<int> Apply(SequenceSave sequence, IReadOnlyList<SequenceChainReferenceSave> chains)
    {
        var groups = new List<List<SequenceChainReferenceSave>>();
        foreach (var track in sequence.Tracks.ToList())
        {
            var moving = track.Chains.Where(chains.Contains).ToList();
            if (moving.Count == 0) continue;
            foreach (var chain in moving) track.Chains.Remove(chain);
            groups.Add(moving);
        }

        var created = new List<int>();
        foreach (var group in groups)
        {
            string baseName = string.IsNullOrWhiteSpace(group[0].Chain) ? DefaultTrackName : group[0].Chain;
            var track = new SequenceTrackSave
            {
                Name = NameGenerator.Unique(sequence.Tracks.Select(t => t.Name), baseName),
                Z = sequence.Tracks.Count,
            };
            track.Chains.AddRange(group);
            created.Add(sequence.Tracks.Count);
            sequence.Tracks.Add(track);
        }
        return created;
    }
}
