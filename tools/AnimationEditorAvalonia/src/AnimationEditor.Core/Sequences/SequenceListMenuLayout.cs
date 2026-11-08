using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>An item in the SEQUENCES list's right-click menu.</summary>
public enum SequenceListMenuCommand
{
    CopyQualifiedName,
    Rename,
    DuplicateSequence,
    Delete,
    AddSequence,
}

/// <summary>
/// Which commands the SEQUENCES list's menu offers for what was right-clicked, as groups the view separates with
/// a divider. Copy Qualified Name, Rename, Duplicate Sequence and Delete act on a sequence, so blank space never offers them.
/// </summary>
public static class SequenceListMenuLayout
{
    /// <summary>The item's text when <paramref name="selected"/> sequences are selected: Duplicate and Delete count several, as their undo labels do.</summary>
    public static string Header(SequenceListMenuCommand command, int selected) => command switch
    {
        SequenceListMenuCommand.CopyQualifiedName => "Copy Qualified Name",
        SequenceListMenuCommand.Rename => "Rename…",
        SequenceListMenuCommand.DuplicateSequence => selected > 1 ? $"Duplicate {selected} Sequences" : "Duplicate Sequence",
        SequenceListMenuCommand.Delete => selected > 1 ? $"Delete {selected} Sequences" : "Delete",
        SequenceListMenuCommand.AddSequence => "Add Sequence",
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
                new[] { SequenceListMenuCommand.CopyQualifiedName },
                new[] { SequenceListMenuCommand.Rename, SequenceListMenuCommand.DuplicateSequence, SequenceListMenuCommand.Delete },
                new[] { SequenceListMenuCommand.AddSequence },
            }
            :new IReadOnlyList<SequenceListMenuCommand>[]
            {
                new[] { SequenceListMenuCommand.AddSequence },
            };
}
