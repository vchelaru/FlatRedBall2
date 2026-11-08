using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class FilmstripReadoutTests
{
    private static FilmstripCell Cell(int frame, params FilmstripVisit[] visits) =>
        new(frame, visits, visits[0].Start, visits[^1].End, 0, 10, 25, 0, false);

    [Fact]
    public void At_PlainFrame_SaysFrameNumberDurationAndStartOnTheTimeline()
    {
        var visit = new FilmstripVisit(2, 1.4, 1.52, 0);

        var card = FilmstripReadout.At("Walk", 8, 0.5, Cell(2, visit), visit);

        card.Title.ShouldBe("Walk");
        card.Frame.ShouldBe(2);
        card.Lines.ShouldBe(new[] { "3 / 8", "0.12 s", "at 1.90 s" });
    }

    [Fact]
    public void At_DoubledPingPongCell_SaysWhichHalfTheShowingIs()
    {
        var forward = new FilmstripVisit(3, 0.3, 0.4, 0, IsReturn: false);
        var back = new FilmstripVisit(3, 0.4, 0.5, 0, IsReturn: true);
        var cell = Cell(3, forward, back);

        FilmstripReadout.At("Chomp", 4, 0, cell, forward).Lines.Last().ShouldBe("forward");
        FilmstripReadout.At("Chomp", 4, 0, cell, back).Lines.Last().ShouldBe("return");
    }

    [Fact]
    public void At_HeldTail_SaysItIsHeld_AndItsDurationCoversTheWholeHold()
    {
        var visit = new FilmstripVisit(2, 0.2, 0.9, 0, IsHold: true);

        FilmstripReadout.At("Once", 3, 0, Cell(2, visit), visit).Lines.ShouldBe(new[] { "3 / 3", "0.70 s", "at 0.20 s", "held" });
    }

    [Fact]
    public void At_SingleShowingOfAPingPong_HasNoHalfLine()
    {
        var visit = new FilmstripVisit(1, 0.1, 0.2, 0, IsReturn: true);

        FilmstripReadout.At("Chomp", 4, 0, Cell(1, visit), visit).Lines.ShouldBe(new[] { "2 / 4", "0.10 s", "at 0.10 s" });
    }

    [Fact]
    public void At_MergedSliverGroup_ListsItsRealFrames()
    {
        var visits = Enumerable.Range(0, 3).Select(i => new FilmstripVisit(i, i * 0.01, i * 0.01 + 0.01, 0)).ToArray();
        var cell = new FilmstripCell(2, visits, 0, 0.03, 0, 3, 25, 0, IsMerged: true);

        var card = FilmstripReadout.At("Stress", 6, 0, cell, visits[1]);

        card.Lines.ShouldBe(new[] { "2 / 6", "0.01 s", "at 0.01 s", "merged: 1, 2, 3" });
    }

    [Fact]
    public void At_MergedGroupOfManyFrames_ListsTheFirstEightThenAnEllipsis()
    {
        var visits = Enumerable.Range(0, 10).Select(i => new FilmstripVisit(i, i * 0.01, i * 0.01 + 0.01, 0)).ToArray();
        var cell = new FilmstripCell(0, visits, 0, 0.1, 0, 3, 25, 0, IsMerged: true);

        FilmstripReadout.At("Stress", 10, 0, cell, visits[0]).Lines.Last().ShouldBe("merged: 1, 2, 3, 4, 5, 6, 7, 8 …");
    }

    [Fact]
    public void Cells_PingPongMarksTheWayBackAsReturn_AndAReversedOneMarksItsFirstHalf()
    {
        var frames = new[] { new FilmstripFrame(0.1, 32, 32), new FilmstripFrame(0.1, 32, 32), new FilmstripFrame(0.1, 32, 32) };

        var plain = FilmstripLayout.Cells(frames, true, false, 0.6, 100, 25);
        var reversed = FilmstripLayout.Cells(frames, true, true, 0.6, 100, 25);

        plain.SelectMany(c => c.Visits).Select(v => v.IsReturn).ShouldBe(new[] { false, false, false, true, true, true });
        reversed.SelectMany(c => c.Visits).Select(v => v.IsReturn).ShouldBe(new[] { true, true, true, false, false, false });
    }
}
