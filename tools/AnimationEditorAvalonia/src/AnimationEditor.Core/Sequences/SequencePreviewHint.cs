using FlatRedBall2.AnimationEditorCommon;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>The line that explains an empty sequence preview.</summary>
public static class SequencePreviewHint
{
    /// <summary>
    /// What to say when no chain plays at <paramref name="time"/>: null when something shows, or when the sequence has
    /// no chains at all (the timeline already invites a drop). It names no time of its own, so it stays the same while
    /// playback crosses a gap.
    /// </summary>
    public static string? For(SequenceSave? sequence, int spritesShown, double time)
    {
        if (sequence is null || spritesShown > 0) return null;
        var starts = sequence.Tracks.SelectMany(track => track.Chains).Select(chain => chain.Start).ToList();
        if (starts.Count == 0) return null;
        var later = starts.Where(start => start > time).ToList();
        return later.Count == 0
            ? "Nothing plays at the playhead."
            : string.Create(CultureInfo.InvariantCulture, $"Nothing plays at the playhead. The next chain starts at {later.Min():0.00} s.");
    }
}
