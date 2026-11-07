namespace AnimationEditor.Core.Navigation;

/// <summary>
/// A place in the editor the user can come back to: a tab plus what was selected in it. Fields that do not apply to
/// the tab's kind stay null. Equality is by value, so the same place pushed twice in a row is recognised.
/// </summary>
/// <param name="Path">Full path of the tab's file, or the sentinel path of an Untitled tab.</param>
/// <param name="ChainName">The selected chain in an achx tab.</param>
/// <param name="FrameIndex">The selected frame's index within <paramref name="ChainName"/>.</param>
/// <param name="SequenceIndex">The selected sequence in a seqj tab.</param>
/// <param name="TimelineTrack">The selected track, or the track of the selected chain, in a seqj tab's timeline.</param>
/// <param name="TimelineChain">The selected chain within <paramref name="TimelineTrack"/>; null when only the track header is selected.</param>
public readonly record struct NavigationLocation(
    string Path,
    string? ChainName = null,
    int? FrameIndex = null,
    int? SequenceIndex = null,
    int? TimelineTrack = null,
    int? TimelineChain = null);
