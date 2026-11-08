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
    /// Takes <paramref name="chains"/> out of the tracks they are in and puts one new track per source track in front of
    /// all the others (at the top of the list, the way the new-track row sits), in lane order, each holding that track's
    /// chains in their old order. A new track is named after its first chain (numbered when taken; "Track" for a chain
    /// with no name), exactly as a chip dropped on the row names it. A source track left empty stays. Returns the new
    /// tracks' indices (0, 1, ...).
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
            };
            track.Chains.AddRange(group);
            created.Add(created.Count);
            sequence.Tracks.Insert(created.Count - 1, track);
        }
        return created;
    }
}
