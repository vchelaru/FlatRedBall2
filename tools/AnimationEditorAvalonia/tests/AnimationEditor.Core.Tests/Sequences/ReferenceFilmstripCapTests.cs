using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The filmstrip of a very long clip.</summary>
public class ReferenceFilmstripCapTests
{
    [Fact]
    public void ReferenceFilmstripCells_VeryLongClipOfAShortTarget_IsCapped()
    {
        // The other strips stop at 50,000 cells (FilmstripLayout, EventMarkers).
        ReferenceFilmstrip.Cells(0.1, 100_000, 100).Count.ShouldBeLessThanOrEqualTo(50_000);
    }

    // 0.1 s: one cell per cycle. 3 s at 100 and 2000: several, then the most a cycle has.
    [Theory]
    [InlineData(0.1, 100)]
    [InlineData(3, 100)]
    [InlineData(3, 2000)]
    public void Cells_OfAView_AreTheOnesOfTheWholeStripThatReachIntoIt(double target, double pixelsPerSecond)
    {
        const double Length = 100.05;
        var whole = ReferenceFilmstrip.Cells(target, Length, pixelsPerSecond);

        foreach (double from in new[] { -50, 0, 1234.5, Length * pixelsPerSecond - 700 })
            ReferenceFilmstrip.Cells(target, Length, pixelsPerSecond, from, from + 1500)
                .ShouldBe(whole.Where(cell => cell.X + cell.Width >= from && cell.X <= from + 1500));
    }

    [Fact]
    public void Cells_OfAViewFarIntoAVeryLongClip_AreStillThere()
    {
        var cells = ReferenceFilmstrip.Cells(0.1, 100_000, 100, 9_000_000, 9_001_000);

        cells[0].X.ShouldBe(9_000_000, 10.001);
        (cells[^1].X + cells[^1].Width).ShouldBe(9_001_000, 10.001);
    }
}
