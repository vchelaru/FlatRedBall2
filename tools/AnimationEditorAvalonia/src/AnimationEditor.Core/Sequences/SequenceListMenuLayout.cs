using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>An item in the SEQUENCES list's right-click menu.</summary>
public enum SequenceListMenuCommand
{
    Rename,
    Delete,
    AddSequence,
}

/// <summary>
/// Which commands the SEQUENCES list's menu offers for what was right-clicked, as groups the view separates with
/// a divider. Rename and Delete act on a sequence, so blank space never offers them.
/// </summary>
public static class SequenceListMenuLayout
{
    public static IReadOnlyList<IReadOnlyList<SequenceListMenuCommand>> For(bool onSequence) =>
        onSequence
            ? new IReadOnlyList<SequenceListMenuCommand>[]
            {
                new[] { SequenceListMenuCommand.Rename, SequenceListMenuCommand.Delete },
                new[] { SequenceListMenuCommand.AddSequence },
            }
            : new IReadOnlyList<SequenceListMenuCommand>[]
            {
                new[] { SequenceListMenuCommand.AddSequence },
            };
}
