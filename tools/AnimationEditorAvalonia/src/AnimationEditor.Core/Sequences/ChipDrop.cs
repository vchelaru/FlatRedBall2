namespace AnimationEditor.Core.Sequences;

/// <summary>What the mouse cursor should look like while a library chip is dragged.</summary>
public enum ChipDragCursor
{
    /// <summary>The drop would add the chain.</summary>
    Copy,

    /// <summary>The drop would do nothing.</summary>
    NotAllowed,
}

/// <summary>The cursor and the line of text that follow a dragged library chip.</summary>
public readonly record struct ChipDragFeedback(ChipDragCursor Cursor, string Hint)
{
    /// <summary>Feedback for a pointer over <paramref name="target"/>, as <see cref="ChipDrop.TargetAt"/> returned it.</summary>
    public static ChipDragFeedback For(TimelineRow? target, string? trackName) => target switch
    {
        { Kind: TimelineRowKind.Track } => new ChipDragFeedback(ChipDragCursor.Copy, $"Add to {trackName}"),
        { Kind: TimelineRowKind.NewTrack } => new ChipDragFeedback(ChipDragCursor.Copy, "Add as a new track"),
        _ => new ChipDragFeedback(ChipDragCursor.NotAllowed, "Drop on a track"),
    };
}

/// <summary>Where a library chip dropped on the sequence timeline lands.</summary>
public static class ChipDrop
{
    /// <summary>
    /// The row a chip dropped at (<paramref name="x"/>, <paramref name="y"/>) joins, or null where a drop does nothing:
    /// over the track headers, past the right edge, on the ruler, or below the new-track row.
    /// </summary>
    public static TimelineRow? TargetAt(TimelineViewport viewport, int trackCount, double x, double y, double width)
    {
        if (x < TimelineViewport.HeaderWidth || x > width) return null;
        var row = viewport.RowAt(y, trackCount);
        return row.Kind is TimelineRowKind.Track or TimelineRowKind.NewTrack ? row : null;
    }
}
