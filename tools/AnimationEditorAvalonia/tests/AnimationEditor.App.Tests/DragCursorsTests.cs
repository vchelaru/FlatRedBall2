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
    public void StockCursorId_ReorderIsThePlainArrow_NotAResizeArrow()
    {
        DragCursors.StockCursorId(TimelineCursor.Reorder).ShouldBe(32512);
    }

    [Fact]
    public void StockCursorId_PanIsSizeAll()
    {
        DragCursors.StockCursorId(TimelineCursor.Pan).ShouldBe(32646);
    }

    [AvaloniaFact]
    public void ForHover_ReorderIsThePlainArrow_ResizeAndPanKeepTheirOwnShapes()
    {
        DragCursors.ForHover(TimelineCursor.Default).ShouldBeNull();
        DragCursors.ForHover(TimelineCursor.Reorder).ShouldBeNull();

        var resize = DragCursors.ForHover(TimelineCursor.ResizeHorizontal).ShouldNotBeNull();
        var pan = DragCursors.ForHover(TimelineCursor.Pan).ShouldNotBeNull();
        resize.ShouldNotBeSameAs(pan);
    }
}
