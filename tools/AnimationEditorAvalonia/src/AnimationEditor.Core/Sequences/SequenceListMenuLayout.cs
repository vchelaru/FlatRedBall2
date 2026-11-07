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
