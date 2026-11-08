using AnimationEditor.Core.Utilities;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The one wording for sequence undo/History labels, matching the ANIMATIONS tree's: a Title Case verb, then either
/// one quoted name (<c>Delete Track 'Body'</c>) or a count of Title Case nouns (<c>Delete 3 Tracks</c>), no trailing
/// punctuation. Nouns are passed singular and Title Case ("Track", "New Track").
/// </summary>
public static class SequenceLabel
{
    /// <summary><c>Delete Track 'Body'</c>.</summary>
    public static string Named(string verb, string noun, string name) => $"{verb} {noun} '{name}'";

    /// <summary><c>Delete 3 Tracks</c>.</summary>
    public static string Counted(string verb, int count, string noun) => $"{verb} {Count(count, noun)}";

    /// <summary><c>Rename Track 'Body' to 'Head'</c>.</summary>
    public static string Renamed(string noun, string oldName, string newName) => $"Rename {noun} '{oldName}' to '{newName}'";

    /// <summary><c>Turn Loop On for 'Idle'</c>; <paramref name="target"/> is a <see cref="Quoted"/> name or a <see cref="Count"/>.</summary>
    public static string Toggled(string what, bool on, string target) => $"Turn {what} {(on ? "On" : "Off")} for {target}";

    /// <summary><c>'Body'</c>.</summary>
    public static string Quoted(string name) => $"'{name}'";

    /// <summary><c>Move 2 Chains to 2 New Tracks</c>, or <c>Move 'Walk' to New Track 'Walk 2'</c> for one chain.</summary>
    public static string MoveChainsToNewTracks(int moved, string chain, IReadOnlyList<string> newTracks)
    {
        string target = newTracks.Count == 1 ? $"New Track '{newTracks[0]}'" : Count(newTracks.Count, "New Track");
        return moved > 1 ? $"Move {Count(moved, "Chain")} to {target}" : $"Move '{chain}' to {target}";
    }

    /// <summary><c>Move 2 Chains</c>, <c>Move 'Walk'</c> (same track) or <c>Move 'Walk' to 'Body'</c> (another track; <paramref name="toTrack"/> null for none).</summary>
    public static string MoveChains(int moved, string chain, string? toTrack) =>
        moved > 1 ? Counted("Move", moved, "Chain")
        : toTrack is null ? $"Move '{chain}'"
        : $"Move '{chain}' to '{toTrack}'";

    /// <summary><c>3 Tracks</c>, for a label that embeds a count mid-sentence.</summary>
    public static string Count(int count, string noun) => Plural.Format(count, noun);

    /// <summary><see cref="Named"/> for exactly one name, otherwise <see cref="Counted"/>.</summary>
    public static string OneOrMany(string verb, string noun, IReadOnlyList<string> names) =>
        names.Count == 1 ? Named(verb, noun, names[0]) : Counted(verb, names.Count, noun);
}
