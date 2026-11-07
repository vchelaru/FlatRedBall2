namespace AnimationEditor.Core.Sequences;

/// <summary>Which library chips animate. A chip plays while hovered, or all of them do while autoplay is on.</summary>
public static class LibraryChipPlayback
{
    /// <summary>
    /// True when a chip should be animating. A one-frame chain has nothing to play, and a collapsed source's chips are
    /// hidden, so neither ticks even with autoplay on.
    /// </summary>
    public static bool ShouldPlay(bool autoplayAll, bool hovered, bool sourceCollapsed, int frameCount) =>
        frameCount >= 2 && !sourceCollapsed && (autoplayAll || hovered);
}
