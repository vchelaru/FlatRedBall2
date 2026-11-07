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
    /// <summary>Feedback for a chip over a drop of <paramref name="kind"/> (from <see cref="TimelineDropPlan"/>); <paramref name="trackName"/> names the track for <see cref="TimelineDropKind.Track"/>.</summary>
    public static ChipDragFeedback For(TimelineDropKind kind, string? trackName) => kind switch
    {
        TimelineDropKind.Track => new ChipDragFeedback(ChipDragCursor.Copy, $"Add to {trackName}"),
        TimelineDropKind.NewTrack => new ChipDragFeedback(ChipDragCursor.Copy, "Add as a new track"),
        _ => new ChipDragFeedback(ChipDragCursor.NotAllowed, "Drop on a track"),
    };
}
