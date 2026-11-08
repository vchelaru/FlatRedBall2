using Shouldly;
using Xunit;
using static AnimationEditor.App.Controls.PreviewControl;

namespace AnimationEditor.App.Tests;

public class PreviewMirrorOnTrackTests
{
    [Fact]
    public void MirrorOnTrack_FlippedTrack_MirrorsThePolygonsOriginAndPointsAcrossX()
    {
        var polygon = new PreviewShapeInfo(PreviewShapeKind.Polygon, 3, 1, 0, 0, IsSelected: false, Points: new[] { 2f, 4f, -1f, 5f });

        var placed = MirrorOnTrack(polygon, flipHorizontal: true);

        (placed.X, placed.Y).ShouldBe((-3f, 1f));
        placed.Points.ShouldBe(new[] { -2f, 4f, 1f, 5f });
    }

    [Fact]
    public void MirrorOnTrack_UnflippedTrack_LeavesTheShapeWhereTheFrameHasIt()
    {
        var rect = new PreviewShapeInfo(PreviewShapeKind.Rect, 3, 1, 4, 2, IsSelected: false);

        MirrorOnTrack(rect, flipHorizontal: false).ShouldBe(rect);
    }
}
