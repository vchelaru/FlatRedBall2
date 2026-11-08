using AnimationEditor.Core.CommandsAndState;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>An item in the SEQUENCES list's right-click menu.</summary>
public enum SequenceListMenuCommand
{
    Copy,
    Cut,
    Paste,
    DuplicateSequence,
    Rename,
    CopyQualifiedName,
    Delete,
    AddSequence,
    MoveToTop,
    MoveUp,
    MoveDown,
    MoveToBottom,
    SortSequences,
}

/// <summary>
/// Which commands the SEQUENCES list's menu offers for what was right-clicked, as groups the view separates with
/// a divider, in the order and wording of the ANIMATIONS tree's chain menu (Move To Top, Up, Down, To Bottom; Add; Copy, Cut,
/// Paste, Duplicate; Rename, Copy Qualified Name; Delete; Sort). Everything but Paste, Add Sequence and Sort acts on a sequence, so blank space never offers it.
/// </summary>
public static class SequenceListMenuLayout
{
    /// <summary>The item's text when <paramref name="selected"/> sequences are selected: Copy, Cut, Duplicate and Delete count several, as the tree's menu and the undo labels do.</summary>
    public static string Header(SequenceListMenuCommand command, int selected) => command switch
    {
        SequenceListMenuCommand.Copy => selected > 1 ? $"Copy {selected} Sequences" : "Copy",
        SequenceListMenuCommand.Cut => selected > 1 ? $"Cut {selected} Sequences" : "Cut",
        SequenceListMenuCommand.Paste => "Paste",
        SequenceListMenuCommand.DuplicateSequence => selected > 1 ? $"Duplicate {selected} Sequences" : "Duplicate",
        SequenceListMenuCommand.Rename => "Rename…",
        SequenceListMenuCommand.CopyQualifiedName => "Copy Qualified Name",
        SequenceListMenuCommand.Delete => selected > 1 ? $"Delete {selected} Sequences" : "Delete Sequence",
        SequenceListMenuCommand.AddSequence => "Add Sequence",
        SequenceListMenuCommand.MoveToTop => "Move To Top",
        SequenceListMenuCommand.MoveUp => "Move Up",
        SequenceListMenuCommand.MoveDown => "Move Down",
        SequenceListMenuCommand.MoveToBottom => "Move To Bottom",
        SequenceListMenuCommand.SortSequences => "Sort Sequences Alphabetically",
        _ => throw new System.ArgumentOutOfRangeException(nameof(command), command, null),
    };

    /// <summary>The same icon the ANIMATIONS tree's menu gives the matching item.</summary>
    public static TreeMenuIcon Icon(SequenceListMenuCommand command) => command switch
    {
        SequenceListMenuCommand.Copy => TreeMenuIcon.Copy,
        SequenceListMenuCommand.Cut => TreeMenuIcon.Cut,
        SequenceListMenuCommand.Paste => TreeMenuIcon.Paste,
        SequenceListMenuCommand.DuplicateSequence => TreeMenuIcon.Duplicate,
        SequenceListMenuCommand.Rename => TreeMenuIcon.Rename,
        SequenceListMenuCommand.CopyQualifiedName => TreeMenuIcon.CopyName,
        SequenceListMenuCommand.Delete => TreeMenuIcon.Delete,
        SequenceListMenuCommand.AddSequence => TreeMenuIcon.Sequence,
        SequenceListMenuCommand.MoveToTop => TreeMenuIcon.MoveToTop,
        SequenceListMenuCommand.MoveUp => TreeMenuIcon.MoveUp,
        SequenceListMenuCommand.MoveDown => TreeMenuIcon.MoveDown,
        SequenceListMenuCommand.MoveToBottom => TreeMenuIcon.MoveToBottom,
        SequenceListMenuCommand.SortSequences => TreeMenuIcon.Sort,
        _ => throw new System.ArgumentOutOfRangeException(nameof(command), command, null),
    };

    /// <summary>Whether the item is enabled with <paramref name="selected"/> sequences selected. Rename needs a single selection.</summary>
    public static bool IsEnabled(SequenceListMenuCommand command, int selected) =>
        command != SequenceListMenuCommand.Rename || selected <= 1;

    /// <summary>Whether a sequence can be renamed (menu, hotkey, inline box, Inspector) with <paramref name="selected"/> selected.</summary>
    public static bool CanRename(int selected) => IsEnabled(SequenceListMenuCommand.Rename, selected);

    /// <summary>The reorder a Move item performs.</summary>
    public static ReorderMove MoveOf(SequenceListMenuCommand command) => command switch
    {
        SequenceListMenuCommand.MoveToTop => ReorderMove.ToTop,
        SequenceListMenuCommand.MoveUp => ReorderMove.Up,
        SequenceListMenuCommand.MoveDown => ReorderMove.Down,
        SequenceListMenuCommand.MoveToBottom => ReorderMove.ToBottom,
        _ => throw new System.ArgumentOutOfRangeException(nameof(command), command, "Not a move item."),
    };

    /// <summary>
    /// The menu's groups. <paramref name="moves"/> are the reorders that would change something for the selection
    /// (<see cref="SequenceReorder.AvailableMoves"/>); like the tree's menu, one that can't happen is left out, and so is
    /// the divider under the Move group when none can. Both menus end with Sort Sequences Alphabetically.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<SequenceListMenuCommand>> For(bool onSequence, IReadOnlyList<ReorderMove>? moves = null)
    {
        var groups = new List<IReadOnlyList<SequenceListMenuCommand>>();
        if (onSequence)
        {
            var moveItems = new List<SequenceListMenuCommand>();
            if (moves is not null)
            {
                if (moves.Contains(ReorderMove.ToTop)) moveItems.Add(SequenceListMenuCommand.MoveToTop);
                if (moves.Contains(ReorderMove.Up)) moveItems.Add(SequenceListMenuCommand.MoveUp);
                if (moves.Contains(ReorderMove.Down)) moveItems.Add(SequenceListMenuCommand.MoveDown);
                if (moves.Contains(ReorderMove.ToBottom)) moveItems.Add(SequenceListMenuCommand.MoveToBottom);
            }
            if (moveItems.Count > 0) groups.Add(moveItems.ToArray());
            groups.Add(new[] { SequenceListMenuCommand.AddSequence });
            groups.Add(new[] { SequenceListMenuCommand.Copy, SequenceListMenuCommand.Cut, SequenceListMenuCommand.Paste, SequenceListMenuCommand.DuplicateSequence });
            groups.Add(new[] { SequenceListMenuCommand.Rename, SequenceListMenuCommand.CopyQualifiedName });
            groups.Add(new[] { SequenceListMenuCommand.Delete });
        }
        else
        {
            groups.Add(new[] { SequenceListMenuCommand.AddSequence });
            groups.Add(new[] { SequenceListMenuCommand.Paste });
        }
        groups.Add(new[] { SequenceListMenuCommand.SortSequences });
        return groups;
    }
}
