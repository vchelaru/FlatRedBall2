using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;
using Cmd = AnimationEditor.Core.Sequences.SequenceListMenuCommand;

namespace AnimationEditor.Core.Tests;

/// <summary>What the SEQUENCES row menu calls its items and which it enables, for the number of sequences selected.</summary>
public class SequenceListMenuLayoutTests
{
    [Theory]
    [InlineData(Cmd.Copy, 1, "Copy")]
    [InlineData(Cmd.Copy, 3, "Copy 3 Sequences")]
    [InlineData(Cmd.Cut, 2, "Cut 2 Sequences")]
    [InlineData(Cmd.Paste, 3, "Paste")]
    [InlineData(Cmd.Rename, 1, "Rename…")]
    [InlineData(Cmd.Rename, 3, "Rename…")]
    [InlineData(Cmd.DuplicateSequence, 1, "Duplicate Sequence")]
    [InlineData(Cmd.DuplicateSequence, 3, "Duplicate 3 Sequences")]
    [InlineData(Cmd.Delete, 1, "Delete")]
    [InlineData(Cmd.Delete, 2, "Delete 2 Sequences")]
    [InlineData(Cmd.CopyQualifiedName, 3, "Copy Qualified Name")]
    [InlineData(Cmd.AddSequence, 3, "Add Sequence")]
    public void Header_CountsTheSelection_OnlyWhereItActsOnAll(Cmd command, int selected, string expected) =>
        SequenceListMenuLayout.Header(command, selected).ShouldBe(expected);

    [Theory]
    [InlineData(Cmd.Rename, 0, true)]
    [InlineData(Cmd.Rename, 1, true)]
    [InlineData(Cmd.Rename, 2, false)]
    [InlineData(Cmd.DuplicateSequence, 3, true)]
    [InlineData(Cmd.Delete, 3, true)]
    [InlineData(Cmd.CopyQualifiedName, 3, true)]
    [InlineData(Cmd.AddSequence, 3, true)]
    public void IsEnabled_OnlyRenameNeedsASingleSelection(Cmd command, int selected, bool expected) =>
        SequenceListMenuLayout.IsEnabled(command, selected).ShouldBe(expected);
}
