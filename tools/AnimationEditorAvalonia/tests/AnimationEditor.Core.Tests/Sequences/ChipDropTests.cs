using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class ChipDropTests
{
    private const double Width = 800;

    [Fact]
    public void TargetAt_OnALane_IsThatTrack()
    {
        var viewport = new TimelineViewport();
        double y = viewport.LaneTop(1) + 5;

        var target = ChipDrop.TargetAt(viewport, 2, TimelineViewport.HeaderWidth + 50, y, Width);

        target.ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
    }

    [Fact]
    public void TargetAt_OnTheNewTrackRow_IsNewTrack()
    {
        var viewport = new TimelineViewport();

        var target = ChipDrop.TargetAt(viewport, 2, TimelineViewport.HeaderWidth + 50, viewport.LaneTop(2) + 5, Width);

        target.ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
    }

    [Theory]
    [InlineData(10, 30)]       // over the track headers
    [InlineData(300, 5)]       // on the ruler
    [InlineData(900, 30)]      // past the right edge
    [InlineData(300, 5000)]    // below the new-track row
    public void TargetAt_AnywhereElse_IsNull(double x, double y)
    {
        ChipDrop.TargetAt(new TimelineViewport(), 1, x, y, Width).ShouldBeNull();
    }

    [Fact]
    public void For_Lane_CopiesAndNamesTheTrack()
    {
        var feedback = ChipDragFeedback.For(new TimelineRow(TimelineRowKind.Track, 0), "Body");

        feedback.ShouldBe(new ChipDragFeedback(ChipDragCursor.Copy, "Add to Body"));
    }

    [Fact]
    public void For_NewTrackRow_CopiesAndSaysNewTrack()
    {
        var feedback = ChipDragFeedback.For(new TimelineRow(TimelineRowKind.NewTrack), null);

        feedback.ShouldBe(new ChipDragFeedback(ChipDragCursor.Copy, "Add as a new track"));
    }

    [Fact]
    public void For_NoTarget_IsNotAllowed()
    {
        ChipDragFeedback.For(null, null).Cursor.ShouldBe(ChipDragCursor.NotAllowed);
    }
}
