using AnimationEditor.Core.CommandsAndState;
using System.Collections.Generic;

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
}

/// <summary>
/// Which commands the SEQUENCES list's menu offers for what was right-clicked, as groups the view separates with
/// a divider, in the order and wording of the ANIMATIONS tree's chain menu (Copy, Cut, Paste, Duplicate; Rename, Copy
/// Qualified Name; Delete). Everything but Paste and Add Sequence acts on a sequence, so blank space never offers it.
/// </summary>
public static class SequenceListMenuLayout
{
    /// <summary>The item's text when <paramref name="selected"/> sequences are selected: Copy, Cut, Duplicate and Delete count several, as the tree's menu and the undo labels do.</summary>
    public static string Header(SequenceListMenuCommand command, int selected) => command switch
    {
        SequenceListMenuCommand.Copy => selected > 1 ? $"Copy {selected} Sequences" : "Copy",
        SequenceListMenuCommand.Cut => selected > 1 ? $"Cut {selected} Sequences" : "Cut",
        SequenceListMenuCommand.Paste => "Paste",
        SequenceListMenuCommand.DuplicateSequence => selected > 1 ? $"Duplicate {selected} Sequences" : "Duplicate Sequence",
        SequenceListMenuCommand.Rename => "Rename…",
        SequenceListMenuCommand.CopyQualifiedName => "Copy Qualified Name",
        SequenceListMenuCommand.Delete => selected > 1 ? $"Delete {selected} Sequences" : "Delete",
        SequenceListMenuCommand.AddSequence => "Add Sequence",
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
        _ => throw new System.ArgumentOutOfRangeException(nameof(command), command, null),
    };

    /// <summary>Whether the item is enabled with <paramref name="selected"/> sequences selected. Rename needs a single selection.</summary>
    public static bool IsEnabled(SequenceListMenuCommand command, int selected) =>
        command != SequenceListMenuCommand.Rename || selected <= 1;

    /// <summary>Whether a sequence can be renamed (menu, hotkey, inline box, Inspector) with <paramref name="selected"/> selected.</summary>
    public static bool CanRename(int selected) => IsEnabled(SequenceListMenuCommand.Rename, selected);

    public static IReadOnlyList<IReadOnlyList<SequenceListMenuCommand>> For(bool onSequence) =>
        onSequence
            ? new IReadOnlyList<SequenceListMenuCommand>[]
            {
                new[] { SequenceListMenuCommand.Copy, SequenceListMenuCommand.Cut, SequenceListMenuCommand.Paste, SequenceListMenuCommand.DuplicateSequence },
                new[] { SequenceListMenuCommand.Rename, SequenceListMenuCommand.CopyQualifiedName },
                new[] { SequenceListMenuCommand.Delete },
                new[] { SequenceListMenuCommand.AddSequence },
            }
            : new IReadOnlyList<SequenceListMenuCommand>[]
            {
                new[] { SequenceListMenuCommand.AddSequence },
                new[] { SequenceListMenuCommand.Paste },
            };
}
