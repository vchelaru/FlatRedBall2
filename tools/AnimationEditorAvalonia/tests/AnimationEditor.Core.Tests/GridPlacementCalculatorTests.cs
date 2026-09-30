using AnimationEditor.Core.Rendering;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GridPlacementCalculatorTests
{
    [Fact]
    public void SnapToCell_ClickInsideCell_ReturnsFullCell()
    {
        // Click at (20,20) with a 16px grid snaps to the cell (16,16,32,32).
        var region = GridPlacementCalculator.SnapToCell(20f, 20f, 16);
        Assert.Equal((16, 16, 32, 32), region);
    }

    [Fact]
    public void SnapToCell_ClickAtCellBoundary_ReturnsThatCell()
    {
        var region = GridPlacementCalculator.SnapToCell(16f, 16f, 16);
        Assert.Equal((16, 16, 32, 32), region);
    }

    [Fact]
    public void SnapToCell_NonSquareGrid_ReturnsFullCell()
    {
        // 8px grid: 33→32, 5→0 → cell (32,0,40,8).
        var region = GridPlacementCalculator.SnapToCell(33f, 5f, 8);
        Assert.Equal((32, 0, 40, 8), region);
    }

    [Fact]
    public void SnapToCell_SpacedGridClickInGapAfterCell_ReturnsThatCellWithoutTheGap()
    {
        // 16px cells, margin 2, spacing 1: cell 1 is 19..35 and x=35 is the gap after it.
        var grid = new TileGrid(16, 16, Margin: 2, Spacing: 1);
        var region = GridPlacementCalculator.SnapToCell(35.5f, 20f, grid);
        Assert.Equal((19, 19, 35, 35), region);
    }

    [Fact]
    public void SpanCells_DragUpAndLeft_CoversEveryCellBetweenBothPoints()
    {
        // 32px cells: (100,70) is cell (3,2), (10,40) is cell (0,1); the span is columns 0..3, rows 1..2.
        var grid = TileGrid.Uniform(32);
        GridPlacementCalculator.SpanCells(100f, 70f, 10f, 40f, grid).ShouldBe((0, 32, 128, 96));
    }

    [Fact]
    public void SpanCells_SpacedGrid_IncludesInnerGapsButNotTheTrailingOne()
    {
        // 16px cells, margin 2, spacing 1: columns 0..1 span 2..35, rows 0..0 span 2..18.
        var grid = new TileGrid(16, 16, Margin: 2, Spacing: 1);
        GridPlacementCalculator.SpanCells(3f, 3f, 34.5f, 10f, grid).ShouldBe((2, 2, 35, 18));
    }
}
