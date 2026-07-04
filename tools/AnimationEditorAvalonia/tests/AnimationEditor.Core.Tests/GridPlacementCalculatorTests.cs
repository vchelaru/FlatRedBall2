using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GridPlacementCalculatorTests
{
    [Fact]
    public void SnapOriginPreserveSize_ClickInsideCell_SnapsOriginKeepsSize()
    {
        // Click at (20,20) with a 16px grid snaps origin to (16,16); a 4×4 frame
        // stays 4×4 → (16,16,20,20).
        var region = GridPlacementCalculator.SnapOriginPreserveSize(20f, 20f, 16, 4, 4);
        Assert.Equal((16, 16, 20, 20), region);
    }

    [Fact]
    public void SnapOriginPreserveSize_ClickAtCellBoundary_ReturnsCellOrigin()
    {
        var region = GridPlacementCalculator.SnapOriginPreserveSize(16f, 16f, 16, 4, 4);
        Assert.Equal((16, 16, 20, 20), region);
    }

    [Fact]
    public void SnapOriginPreserveSize_NonSquareFrame_PreservesBothDimensions()
    {
        // 8px grid: 33→32, 5→0. A 10×3 frame keeps 10 wide, 3 tall → (32,0,42,3).
        var region = GridPlacementCalculator.SnapOriginPreserveSize(33f, 5f, 8, 10, 3);
        Assert.Equal((32, 0, 42, 3), region);
    }
}
