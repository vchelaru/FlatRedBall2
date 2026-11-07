namespace AnimationEditor.Core.Sequences;

/// <summary>What the mouse cursor looks like over the sequence timeline.</summary>
public enum TimelineCursor
{
    Default,

    /// <summary>Hand: over a chain's body, which can be grabbed and dragged in time or to another lane.</summary>
    Hand,

    /// <summary>West-east double arrow: a chain's right edge can be dragged to change its loop count.</summary>
    ResizeHorizontal,
}

public static class TimelineCursors
{
    /// <summary>
    /// The cursor for a pointer over <paramref name="hover"/> (as <see cref="TimelineLayout.HitTest"/> returned it), or
    /// for a press that gripped <paramref name="gripped"/> and has not been released. A grip decides it alone, so an edge
    /// drag keeps the resize arrow wherever the pointer goes.
    /// </summary>
    public static TimelineCursor For(TimelineHit? hover, TimelineHitPart? gripped)
    {
        return (gripped ?? hover?.Part) switch
        {
            TimelineHitPart.Edge => TimelineCursor.ResizeHorizontal,
            TimelineHitPart.Body => TimelineCursor.Hand,
            _ => TimelineCursor.Default,
        };
    }
}
