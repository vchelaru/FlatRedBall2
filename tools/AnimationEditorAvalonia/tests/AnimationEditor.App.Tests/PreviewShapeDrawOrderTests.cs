using AnimationEditor.App.Controls;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Controls.PreviewControl;

namespace AnimationEditor.App.Tests;

public class PreviewShapeDrawOrderTests
{
    [Fact]
    public void ShapeDrawOrder_MixedStates_NormalThenSelectedThenHovered()
    {
        var hovered  = new PreviewShapeInfo(PreviewShapeKind.Rect, 0, 0, 1, 1, IsSelected: false, IsHovered: true);
        var selected = new PreviewShapeInfo(PreviewShapeKind.Rect, 0, 0, 1, 1, IsSelected: true);
        var normalA  = new PreviewShapeInfo(PreviewShapeKind.Rect, 1, 0, 1, 1, IsSelected: false);
        var normalB  = new PreviewShapeInfo(PreviewShapeKind.Rect, 2, 0, 1, 1, IsSelected: false);

        var ordered = ShapeDrawOrder(new[] { hovered, selected, normalA, normalB });

        ordered.ShouldBe(new[] { normalA, normalB, selected, hovered });
    }
}
