using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

public class ChipDragCursorsTests
{
    [Fact]
    public void StockCursorId_CopyIsHand_NotAllowedIsNo()
    {
        ChipDragCursors.StockCursorId(ChipDragCursor.Copy).ShouldBe(32649);
        ChipDragCursors.StockCursorId(ChipDragCursor.NotAllowed).ShouldBe(32648);
    }
}
