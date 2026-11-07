using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class FilmstripLayoutTests
{
    private const double Height = 25;

    // Square 32 px sprites, so each draws 25 px wide at a 25 px cell height.
    private static FilmstripFrame[] Frames(params double[] durations) =>
        durations.Select(d => new FilmstripFrame(d, 32, 32)).ToArray();

    private static int[] Order(IReadOnlyList<FilmstripCell> cells) => cells.Select(c => c.Frame).ToArray();

    private static void ShouldBeSpans(IReadOnlyList<FilmstripCell> cells, params (double X, double Width)[] expected)
    {
        cells.Count.ShouldBe(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            cells[i].X.ShouldBe(expected[i].X, 1e-9);
            cells[i].Width.ShouldBe(expected[i].Width, 1e-9);
        }
    }

    [Fact]
    public void Cells_WidthsAreToScaleInTime()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.2, 0.3), false, false, 0.6, 100, Height);

        ShouldBeSpans(cells, (0, 10), (10, 20), (30, 30));
        cells.Select(c => c.Start).ShouldBe(new[] { 0.0, 0.1, 0.3 }, 1e-9);
    }

    [Fact]
    public void Cells_RepeatThePatternEachCycle_AndTheLastCycleIsClippedAtThePlayedEnd()
    {
        // Two 0.1 s frames, 2.25 cycles: 0 1 0 1 0, the last one cut at 0.45 s (half of its 0.1 s).
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1), false, false, 0.45, 100, Height);

        Order(cells).ShouldBe(new[] { 0, 1, 0, 1, 0 });
        cells.Select(c => c.Visits[0].Cycle).ShouldBe(new[] { 0, 0, 1, 1, 2 });
        cells[4].End.ShouldBe(0.45, 1e-9);
        cells[4].Width.ShouldBe(5, 1e-9);
    }

    [Fact]
    public void Cells_ACutChainDrawsOnlyUpToThePlayedEnd()
    {
        var cells = FilmstripLayout.Cells(Frames(0.5, 0.5), false, false, 0.7, 100, Height);

        Order(cells).ShouldBe(new[] { 0, 1 });
        cells.Last().End.ShouldBe(0.7, 1e-9);
        (cells.Last().X + cells.Last().Width).ShouldBe(70, 1e-9);
    }

    [Fact]
    public void Cells_ClippingKeepsTheWindowOfTheFullCell()
    {
        // A 50 px cell for a 25 px sprite is left-aligned; cut to 10 px it still shows the sprite's left 10 px.
        var cells = FilmstripLayout.Cells(Frames(0.5), false, false, 0.1, 100, Height);

        cells.Single().Width.ShouldBe(10, 1e-9);
        cells.Single().SpriteX.ShouldBe(0);
    }

    [Fact]
    public void Cells_PingPongPlaysForwardThenBack_AndTheTurnaroundFrameIsOneDoubleLengthCell()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), true, false, 0.6, 100, Height);

        Order(cells).ShouldBe(new[] { 0, 1, 2, 1, 0 });
        cells[2].Width.ShouldBe(20, 1e-9);
        cells[2].Visits.Count.ShouldBe(2);
        cells[2].Start.ShouldBe(0.2, 1e-9);
        cells[2].End.ShouldBe(0.4, 1e-9);
    }

    [Fact]
    public void Cells_PingPongMergesTheFirstFrameAcrossTheLoopPoint()
    {
        // Two cycles: the first cycle ends on frame 0 and the second starts on it, so they are one cell over the loop point.
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), true, false, 1.2, 100, Height);

        Order(cells).ShouldBe(new[] { 0, 1, 2, 1, 0, 1, 2, 1, 0 });
        cells[4].X.ShouldBe(50, 1e-9);
        cells[4].Width.ShouldBe(20, 1e-9);
        cells[4].Visits.Select(v => v.Cycle).ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void Cells_ReverseDrawsLastFrameFirst()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.2, 0.3), false, true, 0.6, 100, Height);

        Order(cells).ShouldBe(new[] { 2, 1, 0 });
        ShouldBeSpans(cells, (0, 30), (30, 20), (50, 10));
    }

    [Fact]
    public void Cells_ReversedPingPongDoublesTheFirstFrameAtTheTurnaround()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), true, true, 0.6, 100, Height);

        Order(cells).ShouldBe(new[] { 2, 1, 0, 1, 2 });
        cells[2].Visits.Count.ShouldBe(2);
    }

    [Fact]
    public void Cells_FramesUnderThreePixelsMergeIntoOneCellThatDrawsItsLongestFrame()
    {
        // 1, 1 and 50 px: the two slivers never reach 3 px on their own, so they join the next frame.
        var cells = FilmstripLayout.Cells(Frames(0.01, 0.01, 0.5, 0.1), false, false, 0.62, 100, Height);

        cells.Count.ShouldBe(2);
        cells[0].IsMerged.ShouldBeTrue();
        cells[0].Frame.ShouldBe(2);
        cells[0].Visits.Select(v => v.Frame).ShouldBe(new[] { 0, 1, 2 });
        cells[0].Width.ShouldBe(52, 1e-9);
        cells[1].IsMerged.ShouldBeFalse();
        cells[1].X.ShouldBe(52, 1e-9);
    }

    [Fact]
    public void Cells_SliversGroupUntilThreePixels_AndALeftoverJoinsThePreviousGroup()
    {
        // Five 1 px frames: 1+1+1 = 3 closes a group, the last two join it.
        var cells = FilmstripLayout.Cells(Frames(0.01, 0.01, 0.01, 0.01, 0.01), false, false, 0.05, 100, Height);

        cells.Count.ShouldBe(1);
        cells[0].IsMerged.ShouldBeTrue();
        cells[0].Visits.Count.ShouldBe(5);
        cells[0].Width.ShouldBe(5, 1e-9);
    }

    [Fact]
    public void Cells_AFrameOfExactlyThreePixelsStandsAlone()
    {
        var cells = FilmstripLayout.Cells(Frames(0.03, 0.03), false, false, 0.06, 100, Height);

        cells.Count.ShouldBe(2);
        cells.ShouldAllBe(c => !c.IsMerged);
    }

    [Theory]
    [InlineData(5, -10)]
    [InlineData(10, -7.5)]
    [InlineData(20, -2.5)]
    public void Cells_ANarrowCellIsAWindowCenteredOnTheImage(double cellWidth, double expectedSpriteX)
    {
        var cells = FilmstripLayout.Cells(Frames(cellWidth / 100), false, false, cellWidth / 100, 100, Height);

        cells.Single().Width.ShouldBe(cellWidth, 1e-9);
        cells.Single().SpriteWidth.ShouldBe(25, 1e-9);
        cells.Single().SpriteX.ShouldBe(expectedSpriteX, 1e-9);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(24.5)]
    [InlineData(25)]
    [InlineData(40)]
    [InlineData(300)]
    public void Cells_TheWindowNeverSlidesPastTheSpritesEdge(double cellWidth)
    {
        var cells = FilmstripLayout.Cells(new[] { new FilmstripFrame(cellWidth / 100, 64, 32) }, false, false, cellWidth / 100, 100, Height);
        var cell = cells.Single();

        cell.SpriteWidth.ShouldBe(50, 1e-9);
        cell.SpriteX.ShouldBeLessThanOrEqualTo(0);
        cell.SpriteX.ShouldBeGreaterThanOrEqualTo(System.Math.Min(0, cell.Width - cell.SpriteWidth) - 1e-9);
    }

    [Fact]
    public void Cells_AWideCellLeftAlignsTheSpriteAtTheFramesStart()
    {
        var cells = FilmstripLayout.Cells(Frames(1), false, false, 1, 100, Height);

        cells.Single().Width.ShouldBe(100, 1e-9);
        cells.Single().SpriteX.ShouldBe(0);
    }

    [Fact]
    public void Cells_TheSpriteKeepsItsAspectAtTheCellHeight()
    {
        var cells = FilmstripLayout.Cells(new[] { new FilmstripFrame(1, 48, 16) }, false, false, 1, 100, Height);

        cells.Single().SpriteWidth.ShouldBe(75, 1e-9);
    }

    [Fact]
    public void Cells_NoFramesOrNothingPlayed_HaveNoCells()
    {
        FilmstripLayout.Cells(Frames(), false, false, 1, 100, Height).ShouldBeEmpty();
        FilmstripLayout.Cells(Frames(0.1), false, false, 0, 100, Height).ShouldBeEmpty();
    }

    [Fact]
    public void CellAt_FindsTheCellUnderAnXOffset_AndVisitAtTheShowingUnderATime()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), true, false, 0.6, 100, Height);

        FilmstripLayout.CellAt(cells, 25)!.Frame.ShouldBe(2);
        FilmstripLayout.CellAt(cells, 61).ShouldBeNull();
        var visit = FilmstripLayout.VisitAt(cells[2], 0.35);
        visit.Frame.ShouldBe(2);
        visit.Start.ShouldBe(0.3, 1e-9);
    }

    [Fact]
    public void FramesOf_ReadsDurationsAndSpriteSizesFromTheUvRegions()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.2f, LeftCoordinate = 0, RightCoordinate = 0.5f, TopCoordinate = 0, BottomCoordinate = 0.25f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0, LeftCoordinate = 0, RightCoordinate = 0.25f, TopCoordinate = 0, BottomCoordinate = 0.5f, FlipDiagonal = true });

        var frames = FilmstripLayout.FramesOf(chain, _ => (64, 64));

        frames[0].Duration.ShouldBe(0.2, 1e-6);
        (frames[0].SpriteWidth, frames[0].SpriteHeight).ShouldBe((32.0, 16.0));
        // An unset length plays for 0.1 s; a diagonal flip swaps the footprint.
        frames[1].Duration.ShouldBe(0.1, 1e-6);
        (frames[1].SpriteWidth, frames[1].SpriteHeight).ShouldBe((32.0, 16.0));
    }

    [Fact]
    public void FramesOf_AnUnreadableTextureHasNoSpriteSize()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.2f });

        var frame = FilmstripLayout.FramesOf(chain, _ => null).Single();

        (frame.SpriteWidth, frame.SpriteHeight).ShouldBe((0.0, 0.0));
    }

    [Fact]
    public void LaneBands_FilmRowIsAtLeast24PixelsAndEverythingFitsInTheLane()
    {
        TimelineLayout.FilmHeight.ShouldBeGreaterThanOrEqualTo(24);
        (TimelineLayout.BodyBottom - TimelineLayout.BodyTop).ShouldBe(TimelineLayout.LabelHeight + TimelineLayout.FilmHeight);
        TimelineLayout.TailTop.ShouldBeGreaterThanOrEqualTo(TimelineLayout.BodyBottom);
        (TimelineLayout.TailBottom - TimelineLayout.TailTop).ShouldBeGreaterThanOrEqualTo(3);
        TimelineLayout.TailBottom.ShouldBeLessThanOrEqualTo(TimelineViewport.LaneHeight);
    }
}
