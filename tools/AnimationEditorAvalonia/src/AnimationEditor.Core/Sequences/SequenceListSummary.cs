using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.ViewModels;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The grey text after a sequence's name in the list, as the ANIMATIONS rows end in "3 fr · 0.30s": the track count and
/// the sequence's length, formatted the way a chain row formats its play time.
/// </summary>
public static class SequenceListSummary
{
    /// <summary>For example <c>"2 tracks · 0.60s"</c>, <c>"1 track · 0.30s"</c> or <c>"0 tracks · 0.00s"</c>.</summary>
    public static string Format(int trackCount, double seconds) =>
        $"{Plural.Format(trackCount, "track")} · {TimelineBuilder.FormatSeconds((float)seconds)}";
}
