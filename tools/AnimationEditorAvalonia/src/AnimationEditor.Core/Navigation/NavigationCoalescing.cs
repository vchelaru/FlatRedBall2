using System;

namespace AnimationEditor.Core.Navigation;

/// <summary>
/// Decides which user moves earn a history entry, so arrowing through frames does not bury the places that matter.
/// </summary>
/// <remarks>
/// The rule: a move is recorded when it changes the tab or the chain-level target (chain, sequence, track, or chain
/// in a track). A move that only changes the frame of the same chain collapses into the run it belongs to, unless
/// the user paused for at least <see cref="QuietWindow"/> first. The pause is measured from the previous change,
/// not from the first one, so a run lasts as long as the user keeps moving. To tune the rule, change
/// <see cref="QuietWindow"/> (use <see cref="TimeSpan.Zero"/> to record every frame move, a very large value to
/// never record one) or <see cref="IsSameTarget"/>.
/// </remarks>
public static class NavigationCoalescing
{
    /// <summary>How long the user must pause on a chain before the next frame move counts as its own entry.</summary>
    public static readonly TimeSpan QuietWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Whether moving from <paramref name="leaving"/> to <paramref name="arriving"/> should push
    /// <paramref name="leaving"/>. <paramref name="sinceLastChange"/> is the time since the user's previous move,
    /// or null when there was none.
    /// </summary>
    public static bool ShouldRecord(NavigationLocation leaving, NavigationLocation arriving, TimeSpan? sinceLastChange)
    {
        if (!IsSameTarget(leaving, arriving)) return true;
        return sinceLastChange is not { } elapsed || elapsed >= QuietWindow;
    }

    /// <summary>True when the two locations differ at most in the selected frame.</summary>
    public static bool IsSameTarget(NavigationLocation a, NavigationLocation b) =>
        a with { FrameIndex = null } == b with { FrameIndex = null };
}
