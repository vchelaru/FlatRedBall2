using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Dragging whole animations in the Preview panel moves their shapes along with the sprites
/// (#1583); a frame-level grab stays sprite-only.
/// </summary>
public class PreviewChainDragShapesTests
{
    private static (AnimationChainSave Chain, AnimationFrameSave Frame, AARectSave Rect, CircleSave Circle) MakeChain(
        string name, float rectX, float rectY)
    {
        var rect   = new AARectSave { Name = "Hit", X = rectX, Y = rectY };
        var circle = new CircleSave { Name = "Ball", X = 1f, Y = 2f };
        var frame  = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave.Add(rect);
        frame.ShapesSave.Add(circle);
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(frame);
        return (chain, frame, rect, circle);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_WholeChainSelected_MovesShapesInEveryFrame()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, frame, rect, circle) = MakeChain("Walk", 10f, 20f);
        ctx.SelectedState.SelectedChain = chain;

        ctx.CreatePreviewControl().SimulateChainDrag(3f, 2f);

        Assert.Equal(3f, frame.RelativeX, precision: 3);
        Assert.Equal(13f, rect.X, precision: 3);
        Assert.Equal(22f, rect.Y, precision: 3);
        Assert.Equal(4f, circle.X, precision: 3);
        Assert.Equal(4f, circle.Y, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_MultipleChainsSelected_MovesShapesInAllOfThem()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 0f, 0f);
        var b = MakeChain("B", 50f, 60f);
        ctx.SelectedState.SelectedNodes = new List<object> { a.Chain, b.Chain };

        ctx.CreatePreviewControl().SimulateChainDrag(5f, -5f);

        Assert.Equal(5f, a.Rect.X, precision: 3);
        Assert.Equal(55f, b.Rect.X, precision: 3);
        Assert.Equal(55f, b.Rect.Y, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_Undo_RestoresShapesAndSpritesInOneStep()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, frame, rect, _) = MakeChain("Walk", 10f, 20f);
        ctx.SelectedState.SelectedChain = chain;

        ctx.CreatePreviewControl().SimulateChainDrag(3f, 2f);
        ctx.UndoManager.Undo();

        Assert.Equal(0f, frame.RelativeX, precision: 3);
        Assert.Equal(10f, rect.X, precision: 3);
        Assert.Equal(20f, rect.Y, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Redo();
        Assert.Equal(13f, rect.X, precision: 3);
        Assert.Equal(3f, frame.RelativeX, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_ShapeThatIsNotAKeyframe_IsLeftAlone()
    {
        var ctx = TestHelpers.BuildServices();
        var first  = MakeChain("Walk", 10f, 20f);
        var second = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        var inherited = new AARectSave { Name = "Hit", X = 10f, Y = 20f, SkipOffsetKeyframe = true };
        second.ShapesSave.Add(inherited);
        first.Chain.Frames.Add(second);
        ctx.SelectedState.SelectedChain = first.Chain;

        ctx.CreatePreviewControl().SimulateChainDrag(3f, 2f);

        Assert.Equal(13f, first.Rect.X, precision: 3);
        Assert.Equal(10f, inherited.X, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_LockedChain_ShapesStayPut()
    {
        var ctx = TestHelpers.BuildServices();
        var open   = MakeChain("Open", 0f, 0f);
        var locked = MakeChain("Locked", 7f, 7f);
        locked.Chain.IsLocked = true;
        ctx.SelectedState.SelectedNodes = new List<object> { open.Chain, locked.Chain };

        ctx.CreatePreviewControl().SimulateChainDrag(3f, 2f);

        Assert.Equal(3f, open.Rect.X, precision: 3);
        Assert.Equal(7f, locked.Rect.X, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateFrameDrag_SingleFrame_DoesNotMoveItsShapes()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, frame, rect, _) = MakeChain("Walk", 10f, 20f);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        ctx.CreatePreviewControl().SimulateFrameDrag(3f, 2f);

        Assert.Equal(3f, frame.RelativeX, precision: 3);
        Assert.Equal(10f, rect.X, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateMultiFrameDrag_FramesOnly_DoNotMoveShapes()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 10f, 20f);
        var b = MakeChain("B", 30f, 40f);
        ctx.SelectedState.SelectedNodes = new List<object> { a.Frame, b.Frame };

        ctx.CreatePreviewControl().SimulateMultiFrameDrag(3f, 2f);

        Assert.Equal(3f, a.Frame.RelativeX, precision: 3);
        Assert.Equal(10f, a.Rect.X, precision: 3);
        Assert.Equal(30f, b.Rect.X, precision: 3);
    }
}
