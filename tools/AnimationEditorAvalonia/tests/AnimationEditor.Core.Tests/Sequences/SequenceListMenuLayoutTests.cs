using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Linq;
using Xunit;
using Cmd = AnimationEditor.Core.Sequences.SequenceListMenuCommand;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Which commands the SEQUENCES list's right-click menu offers, by what was hit.</summary>
public class SequenceListMenuLayoutTests
{
    [Fact]
    public void OnASequence_FollowsTheTreesChainMenu_AddSequence_CopyCutPasteDuplicate_RenameCopyName_Delete()
    {
        SequenceListMenuLayout.For(onSequence: true).Select(group => group.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.AddSequence },
            new[] { Cmd.Copy, Cmd.Cut, Cmd.Paste, Cmd.DuplicateSequence },
            new[] { Cmd.Rename, Cmd.CopyQualifiedName },
            new[] { Cmd.Delete },
        });
    }

    [Fact]
    public void OnBlankSpace_OffersAddSequenceAndPaste()
    {
        SequenceListMenuLayout.For(onSequence: false).Select(group => group.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.AddSequence },
            new[] { Cmd.Paste },
        });
    }

    [Theory]
    [InlineData(Cmd.Copy, TreeMenuIcon.Copy)]
    [InlineData(Cmd.Cut, TreeMenuIcon.Cut)]
    [InlineData(Cmd.Paste, TreeMenuIcon.Paste)]
    [InlineData(Cmd.DuplicateSequence, TreeMenuIcon.Duplicate)]
    [InlineData(Cmd.Rename, TreeMenuIcon.Rename)]
    [InlineData(Cmd.CopyQualifiedName, TreeMenuIcon.CopyName)]
    [InlineData(Cmd.Delete, TreeMenuIcon.Delete)]
    public void EachItem_UsesTheIconTheTreeMenuGivesTheSameItem(Cmd command, TreeMenuIcon expected) =>
        SequenceListMenuLayout.Icon(command).ShouldBe(expected);
}
