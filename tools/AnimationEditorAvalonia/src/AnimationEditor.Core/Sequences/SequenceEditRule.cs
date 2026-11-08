namespace AnimationEditor.Core.Sequences;

/// <summary>The pane of a sequence tab that owns the edit hotkeys and the Edit menu.</summary>
public enum EditSurface
{
    /// <summary>Neither pane was touched yet, so there is nothing for a hotkey to act on.</summary>
    None,

    /// <summary>The sequence list.</summary>
    List,

    /// <summary>The timeline: its selected chains or tracks.</summary>
    Timeline,
}

/// <summary>
/// Which pane the edit hotkeys (Copy, Cut, Duplicate, Delete, Rename, Alt+Up/Down) and the Edit menu act on in a sequence
/// tab, and what to say when there is nothing to act on. One rule for all of them: the last pane of the list and the
/// timeline that held the keyboard focus owns them, wherever the focus sits now. A press on the Play button, the preview,
/// the library or an Inspector checkbox moves the focus but not the owner. Paste is the exception: it follows what the
/// clipboard holds (<see cref="PasteRoute"/>).
/// </summary>
public static class SequenceEditRule
{
    /// <summary>
    /// The owner after the focus moved to a control inside the list (<paramref name="inList"/>), inside the timeline
    /// (<paramref name="inTimeline"/>), or elsewhere (neither): a pane takes ownership, anywhere else leaves it where it was.
    /// </summary>
    public static EditSurface After(EditSurface current, bool inList, bool inTimeline) =>
        inList ? EditSurface.List : inTimeline ? EditSurface.Timeline : current;

    /// <summary>
    /// Why F2 cannot rename now, or null when it can: a single sequence on the list, or a single track header on the timeline.
    /// <paramref name="sequences"/> is the list's selection, <paramref name="tracks"/> and <paramref name="chains"/> the timeline's.
    /// </summary>
    public static string? RenameRefusal(EditSurface surface, int sequences, int tracks, int chains) => surface switch
    {
        EditSurface.None => NothingToDo("rename", surface, 0),
        EditSurface.List when sequences == 0 => NothingToDo("rename", surface, 0),
        EditSurface.List when sequences > 1 => "Rename works on one sequence: select just one.",
        EditSurface.Timeline when tracks == 1 && chains == 0 => null,
        EditSurface.Timeline => "Rename works on one track: select a single track header.",
        _ => null,
    };

    /// <summary>
    /// Why Alt+Up or Alt+Down cannot move the selection now, or null when it can: the selected sequences on the list, or a single
    /// track header on the timeline (chains are moved in time, not in the list). Reaching the first or last place is not a refusal.
    /// </summary>
    public static string? MoveRefusal(EditSurface surface, int sequences, int tracks, int chains) => surface switch
    {
        EditSurface.None => NothingToDo("move", surface, 0),
        EditSurface.List when sequences == 0 => NothingToDo("move", surface, 0),
        EditSurface.Timeline when tracks == 1 && chains == 0 => null,
        EditSurface.Timeline when tracks == 0 && chains == 0 => NothingToDo("move", surface, 0),
        EditSurface.Timeline => "Move works on one track: select a single track header.",
        _ => null,
    };

    /// <summary>
    /// Why <paramref name="verb"/> ("copy", "cut", "duplicate", "delete", "rename", "move") has nothing to act on, or null when it
    /// has: the surface is unknown, or its selection is empty (<paramref name="selected"/> is what the surface has selected).
    /// </summary>
    public static string? NothingToDo(string verb, EditSurface surface, int selected)
    {
        if (surface == EditSurface.None)
            return $"Nothing to {verb}: click the sequence list or the timeline first.";
        if (selected > 0) return null;
        return surface == EditSurface.List
            ? $"Nothing to {verb}: the file has no sequence."
            : $"Nothing to {verb}: select a chain or track on the timeline first.";
    }
}
