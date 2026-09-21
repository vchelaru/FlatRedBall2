using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TileGridTests
{
    // 16px cells, 2px margin, 1px spacing: cell 0 at 2..18, cell 1 at 19..35, cell 2 at 36..52.
    private static readonly TileGrid Spaced = new(16, 16, Margin: 2, Spacing: 1);

    [Fact]
    public void CellLeft_SpacedGrid_OffsetsByMarginAndSpacing()
    {
        Assert.Equal(2, Spaced.CellLeft(0));
        Assert.Equal(19, Spaced.CellLeft(1));
        Assert.Equal(36, Spaced.CellLeft(2));
    }

    [Fact]
    public void ColumnContaining_PointInsideGapAfterCell_ReturnsThatCell()
    {
        // x=18 is the 1px gap between cell 0 and cell 1.
        Assert.Equal(0, Spaced.ColumnContaining(18.5f));
        Assert.Equal(1, Spaced.ColumnContaining(19f));
    }

    [Fact]
    public void ColumnContaining_PointLeftOfMargin_ReturnsNegativeColumn()
    {
        Assert.Equal(-1, Spaced.ColumnContaining(1f));
    }

    [Fact]
    public void FrameCells_TwoByOneFootprint_ReturnsEachCellWithoutTheGap()
    {
        var cells = Spaced.FrameCells(left: 19, top: 2, width: 33, height: 16);

        Assert.Equal(2, cells.Count);
        Assert.Equal(new BoundsRect(19, 2, 35, 18), cells[0]);
        Assert.Equal(new BoundsRect(36, 2, 52, 18), cells[1]);
    }

    [Fact]
    public void FrameCells_UnalignedRect_ReturnsEmpty()
    {
        Assert.Empty(Spaced.FrameCells(left: 20, top: 2, width: 33, height: 16));
    }

    [Fact]
    public void SnapEndX_NearCellEnd_ReturnsCellEndNotCellStart()
    {
        // A right edge dragged to 34 lands on cell 1's end (35), not on a cell start.
        Assert.Equal(35f, Spaced.SnapEndX(34f));
        Assert.Equal(52f, Spaced.SnapEndX(45f));
    }

    [Fact]
    public void SnapStartX_NearCellStart_ReturnsCellStart()
    {
        Assert.Equal(19f, Spaced.SnapStartX(21f));
        Assert.Equal(2f, Spaced.SnapStartX(9f));
    }

    [Fact]
    public void SpanWidth_TwoCells_IncludesOneGap()
    {
        Assert.Equal(33, Spaced.SpanWidth(2));
        Assert.Equal(16, Spaced.SpanWidth(1));
    }

    [Fact]
    public void TryLocate_RectSpanningGap_ReturnsFootprint()
    {
        var footprint = Spaced.TryLocate(left: 19, top: 2, width: 33, height: 16, epsilon: 0.001f);

        Assert.Equal(new TileFootprint(Column: 1, Row: 0, Columns: 2, Rows: 1), footprint);
    }

    [Fact]
    public void TryLocate_RectWithoutGapOnSpacedGrid_ReturnsNull()
    {
        // 32 wide = two cells with no gap between them; that isn't a footprint on a spaced grid.
        Assert.Null(Spaced.TryLocate(left: 19, top: 2, width: 32, height: 16, epsilon: 0.001f));
    }

    [Fact]
    public void TryLocate_UniformGrid_MatchesPlainMultiples()
    {
        var footprint = TileGrid.Uniform(16).TryLocate(left: 32, top: 16, width: 48, height: 16, epsilon: 0.001f);

        Assert.Equal(new TileFootprint(Column: 2, Row: 1, Columns: 3, Rows: 1), footprint);
    }
}
