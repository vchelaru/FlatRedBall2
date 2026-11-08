using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Which pane of a sequence tab owns the edit hotkeys, and the toast when they have nothing to act on.</summary>
public class SequenceEditRuleTests
{
    [Theory]
    [InlineData(EditSurface.None, true, false, EditSurface.List)]
    [InlineData(EditSurface.Timeline, true, false, EditSurface.List)]
    [InlineData(EditSurface.None, false, true, EditSurface.Timeline)]
    [InlineData(EditSurface.List, false, true, EditSurface.Timeline)]
    public void FocusEnteringAPane_MakesItTheOwner(EditSurface current, bool inList, bool inTimeline, EditSurface expected) =>
        SequenceEditRule.After(current, inList, inTimeline).ShouldBe(expected);

    [Theory]
    [InlineData(EditSurface.None)]
    [InlineData(EditSurface.List)]
    [InlineData(EditSurface.Timeline)]
    public void FocusLandingElsewhere_LeavesTheOwnerAlone(EditSurface current) =>
        SequenceEditRule.After(current, inList: false, inTimeline: false).ShouldBe(current);

    [Fact]
    public void NothingToDo_BeforeAnyPaneWasTouched_SaysToClickOne() =>
        SequenceEditRule.NothingToDo("copy", EditSurface.None, 0).ShouldBe("Nothing to copy: click the sequence list or the timeline first.");

    [Fact]
    public void NothingToDo_OnTheTimelineWithNothingSelected_SaysToSelectSomething() =>
        SequenceEditRule.NothingToDo("duplicate", EditSurface.Timeline, 0).ShouldBe("Nothing to duplicate: select a chain or track on the timeline first.");

    [Fact]
    public void NothingToDo_OnAnEmptyList_SaysTheFileHasNoSequence() =>
        SequenceEditRule.NothingToDo("delete", EditSurface.List, 0).ShouldBe("Nothing to delete: the file has no sequence.");

    [Theory]
    [InlineData(EditSurface.List, 1)]
    [InlineData(EditSurface.Timeline, 3)]
    public void NothingToDo_WithASelection_IsNull(EditSurface surface, int selected) =>
        SequenceEditRule.NothingToDo("copy", surface, selected).ShouldBeNull();

    [Theory]
    [InlineData(EditSurface.List, 1, 0, 0, false)]
    [InlineData(EditSurface.List, 2, 0, 0, true)]
    [InlineData(EditSurface.List, 0, 0, 0, true)]
    [InlineData(EditSurface.Timeline, 0, 1, 0, false)]
    [InlineData(EditSurface.Timeline, 0, 2, 0, true)]
    [InlineData(EditSurface.Timeline, 0, 0, 1, true)]
    [InlineData(EditSurface.None, 1, 0, 0, true)]
    public void RenameRefusal_NeedsExactlyOneSequenceOrOneTrack(EditSurface surface, int sequences, int tracks, int chains, bool refused) =>
        (SequenceEditRule.RenameRefusal(surface, sequences, tracks, chains) is not null).ShouldBe(refused);

    [Theory]
    [InlineData(EditSurface.List, 1, 0, 0, false)]
    [InlineData(EditSurface.List, 3, 0, 0, false)]
    [InlineData(EditSurface.List, 0, 0, 0, true)]
    [InlineData(EditSurface.Timeline, 0, 1, 0, false)]
    [InlineData(EditSurface.Timeline, 0, 2, 0, true)]
    [InlineData(EditSurface.Timeline, 0, 0, 2, true)]
    [InlineData(EditSurface.None, 1, 0, 0, true)]
    public void MoveRefusal_AllowsSequencesAsABlockButOnlyOneTrack(EditSurface surface, int sequences, int tracks, int chains, bool refused) =>
        (SequenceEditRule.MoveRefusal(surface, sequences, tracks, chains) is not null).ShouldBe(refused);
}
