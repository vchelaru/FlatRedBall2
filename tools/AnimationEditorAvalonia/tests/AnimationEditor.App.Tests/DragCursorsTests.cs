using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

public class DragCursorsTests
{
    [Fact]
    public void StockCursorId_CopyIsHand_NotAllowedIsNo()
    {
        DragCursors.StockCursorId(ChipDragCursor.Copy).ShouldBe(32649);
        DragCursors.StockCursorId(ChipDragCursor.NotAllowed).ShouldBe(32648);
    }

    [Fact]
    public void StockCursorId_ResizeHorizontalIsSizeWE_DefaultIsArrow()
    {
        DragCursors.StockCursorId(TimelineCursor.ResizeHorizontal).ShouldBe(32644);
        DragCursors.StockCursorId(TimelineCursor.Default).ShouldBe(32512);
    }

    [Fact]
    public void StockCursorId_ReorderIsSizeNS()
    {
        DragCursors.StockCursorId(TimelineCursor.Reorder).ShouldBe(32645);
    }

    [Fact]
    public void StockCursorId_PanIsSizeAll()
    {
        DragCursors.StockCursorId(TimelineCursor.Pan).ShouldBe(32646);
    }

    [AvaloniaFact]
    public void ForHover_GivesEachTimelineCursorItsOwnShape_AndNothingForTheDefault()
    {
        DragCursors.ForHover(TimelineCursor.Default).ShouldBeNull();

        var shapes = new[] { TimelineCursor.ResizeHorizontal, TimelineCursor.Reorder, TimelineCursor.Pan }
            .Select(cursor => DragCursors.ForHover(cursor).ShouldNotBeNull()).ToArray();

        shapes.Distinct().Count().ShouldBe(3);
    }
}
