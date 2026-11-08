namespace AnimationEditor.Core.Sequences;

public enum PasteRouteKind { Sequences, Timeline, Refused }

/// <summary>Where a paste goes, decided from the clipboard text alone.</summary>
/// <param name="Kind">Copied sequences, copied chains or tracks, or nothing that can be pasted.</param>
/// <param name="Sequences">The copied sequences, for <see cref="PasteRouteKind.Sequences"/>.</param>
/// <param name="Timeline">The copied chains or tracks, for <see cref="PasteRouteKind.Timeline"/>.</param>
/// <param name="Message">For <see cref="PasteRouteKind.Refused"/>, why nothing was pasted.</param>
/// <param name="IsDamaged">True when the text carries one of the copy markers but is broken (an error, not just the wrong text).</param>
public sealed record PasteRoute(PasteRouteKind Kind, SequenceClipboardContent? Sequences, TimelineClipboardContent? Timeline,
    string? Message, bool IsDamaged = false)
{
    public const string EmptyMessage = "Nothing to paste: the clipboard is empty.";
    public const string NotACopyMessage = "Nothing to paste: the clipboard holds no copied sequences, chains or tracks.";

    /// <summary>
    /// Reads what the clipboard holds and says where it goes: copied sequences go to the sequence list, copied chains or
    /// tracks to the timeline, whichever of the two held the focus. Anything else is refused with a reason, so a paste never
    /// does nothing without saying why.
    /// </summary>
    public static PasteRoute For(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText)) return Refused(EmptyMessage, damaged: false);
        if (SequenceClipboard.TryRead(clipboardText, out var sequences, out string? sequenceError))
            return new PasteRoute(PasteRouteKind.Sequences, sequences, null, null);
        if (sequenceError is not null) return Refused(sequenceError, damaged: true);
        if (TimelineClipboard.TryRead(clipboardText, out var timeline, out string? timelineError))
            return new PasteRoute(PasteRouteKind.Timeline, null, timeline, null);
        if (timelineError is not null) return Refused(timelineError, damaged: true);
        return Refused(NotACopyMessage, damaged: false);
    }

    private static PasteRoute Refused(string message, bool damaged) => new(PasteRouteKind.Refused, null, null, message, damaged);
}
