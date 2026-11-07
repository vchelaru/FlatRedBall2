using Shouldly;
using Xunit;
using static AnimationEditor.App.Controls.PreviewControl;

namespace AnimationEditor.App.Tests;

public class PreviewPlaceOnTrackTests
{
    [Fact]
    public void PlaceOnTrack_FlippedTrack_MirrorsThePolygonsOriginAndPointsAcrossTheTrack()
    {
        var polygon = new PreviewShapeInfo(PreviewShapeKind.Polygon, 3, 1, 0, 0, IsSelected: false, Points: new[] { 2f, 4f, -1f, 5f });

        var placed = PlaceOnTrack(polygon, x: 10, y: 6, flipHorizontal: true);

        (placed.X, placed.Y).ShouldBe((7f, 7f));
        placed.Points.ShouldBe(new[] { -2f, 4f, 1f, 5f });
    }

    [Fact]
    public void PlaceOnTrack_UnflippedTrack_OnlyMovesTheShape()
    {
        var rect = new PreviewShapeInfo(PreviewShapeKind.Rect, 3, 1, 4, 2, IsSelected: false);

        var placed = PlaceOnTrack(rect, x: 10, y: 6, flipHorizontal: false);

        placed.ShouldBe(rect with { X = 13, Y = 7 });
    }
}
