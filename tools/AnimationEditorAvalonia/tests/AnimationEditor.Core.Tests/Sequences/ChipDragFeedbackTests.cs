using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class ChipDragFeedbackTests
{
    [Fact]
    public void For_Lane_CopiesAndNamesTheTrack()
    {
        var feedback = ChipDragFeedback.For(TimelineDropKind.Track, "Body");

        feedback.ShouldBe(new ChipDragFeedback(ChipDragCursor.Copy, "Add to Body"));
    }

    [Fact]
    public void For_NewTrackRow_CopiesAndSaysNewTrack()
    {
        var feedback = ChipDragFeedback.For(TimelineDropKind.NewTrack, null);

        feedback.ShouldBe(new ChipDragFeedback(ChipDragCursor.Copy, "Add as a new track"));
    }

    [Fact]
    public void For_NoDrop_IsNotAllowed()
    {
        ChipDragFeedback.For(TimelineDropKind.None, null).Cursor.ShouldBe(ChipDragCursor.NotAllowed);
    }
}
