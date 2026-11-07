namespace AnimationEditor.Core.Sequences;

/// <summary>What the mouse cursor looks like over the sequence timeline.</summary>
public enum TimelineCursor
{
    Default,

    /// <summary>West-east double arrow: a chain's right edge can be dragged to change its loop count.</summary>
    ResizeHorizontal,

    /// <summary>Four-way arrow: the timeline is being panned by a middle-button drag.</summary>
    Pan,
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
        var part = gripped ?? hover?.Part;
        return part == TimelineHitPart.Edge ? TimelineCursor.ResizeHorizontal : TimelineCursor.Default;
    }
}
