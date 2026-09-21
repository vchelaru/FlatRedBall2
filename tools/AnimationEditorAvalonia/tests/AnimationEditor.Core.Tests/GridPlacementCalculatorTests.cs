using AnimationEditor.Core.Rendering;
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
}
