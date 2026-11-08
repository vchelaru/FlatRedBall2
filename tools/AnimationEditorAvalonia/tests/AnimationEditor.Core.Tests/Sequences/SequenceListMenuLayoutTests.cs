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
    private static readonly ReorderMove[] AllMoves = { ReorderMove.ToTop, ReorderMove.Up, ReorderMove.Down, ReorderMove.ToBottom };

    [Fact]
    public void OnASequence_FollowsTheTreesChainMenu_Moves_AddSequence_CopyCutPasteDuplicate_RenameCopyName_Delete_Sort()
    {
        SequenceListMenuLayout.For(onSequence: true, AllMoves).Select(group => group.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.MoveToTop, Cmd.MoveUp, Cmd.MoveDown, Cmd.MoveToBottom },
            new[] { Cmd.AddSequence },
            new[] { Cmd.Copy, Cmd.Cut, Cmd.Paste, Cmd.DuplicateSequence },
            new[] { Cmd.Rename, Cmd.CopyQualifiedName },
            new[] { Cmd.Delete },
            new[] { Cmd.SortSequences },
        });
    }

    [Fact]
    public void OnASequence_LeavesOutTheMovesThatCannotHappen_AndTheirSeparator_AsTheTreeDoes()
    {
        SequenceListMenuLayout.For(onSequence: true, new[] { ReorderMove.Down, ReorderMove.ToBottom }).First().ShouldBe(new[] { Cmd.MoveDown, Cmd.MoveToBottom });
        SequenceListMenuLayout.For(onSequence: true, System.Array.Empty<ReorderMove>()).First().ShouldBe(new[] { Cmd.AddSequence });
    }

    [Fact]
    public void OnBlankSpace_OffersAddSequenceAndPaste_ThenSort()
    {
        SequenceListMenuLayout.For(onSequence: false).Select(group => group.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.AddSequence },
            new[] { Cmd.Paste },
            new[] { Cmd.SortSequences },
        });
    }

    [Theory]
    [InlineData(Cmd.MoveToTop, ReorderMove.ToTop)]
    [InlineData(Cmd.MoveUp, ReorderMove.Up)]
    [InlineData(Cmd.MoveDown, ReorderMove.Down)]
    [InlineData(Cmd.MoveToBottom, ReorderMove.ToBottom)]
    public void EachMoveItem_MapsToItsMove(Cmd command, ReorderMove move) =>
        SequenceListMenuLayout.MoveOf(command).ShouldBe(move);

    [Theory]
    [InlineData(Cmd.MoveToTop, TreeMenuIcon.MoveToTop)]
    [InlineData(Cmd.MoveUp, TreeMenuIcon.MoveUp)]
    [InlineData(Cmd.MoveDown, TreeMenuIcon.MoveDown)]
    [InlineData(Cmd.MoveToBottom, TreeMenuIcon.MoveToBottom)]
    [InlineData(Cmd.SortSequences, TreeMenuIcon.Sort)]
    public void MoveAndSortItems_UseTheIconsOfTheTreesMoveAndSortItems(Cmd command, TreeMenuIcon expected) =>
        SequenceListMenuLayout.Icon(command).ShouldBe(expected);

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
