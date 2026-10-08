using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Every way a shape gets into a frame must leave the list in file order (rects, polygons, circles),
// so the tree order equals the order after a save and reload (#1293).
[Collection("SequentialSingletons")]
public class ShapeOrderCommandTests
{
    private static (TestServices Ctx, AnimationFrameSave Frame) FrameWithNoShapes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.ShapesSave = null;
        ctx.SelectedState.SelectedFrame = frame;
        return (ctx, frame);
    }

    private static void AssertOrderSurvivesReload(TestServices ctx, AnimationFrameSave frame)
    {
        var expected = frame.ShapesSave!.Shapes.Select(s => s.GetType()).ToArray();
        var json = ctx.Acls.ToJsonString();
        var reloaded = FlatRedBall2.AnimationEditorCommon.AnimationChainListSave.FromJsonString(json);
        var actual = reloaded.AnimationChains.SelectMany(c => c.Frames)
            .First(f => f.ShapesSave is not null).ShapesSave!.Shapes.Select(s => s.GetType()).ToArray();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void AddCircleThenRect_RectLandsBeforeCircle_AndUndoRemoves()
    {
        var (ctx, frame) = FrameWithNoShapes();
        ctx.AppCommands.AddCircle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        frame.ShapesSave!.Shapes[0].ShouldBeOfType<AARectSave>();
        frame.ShapesSave.Shapes[1].ShouldBeOfType<CircleSave>();
        AssertOrderSurvivesReload(ctx, frame);

        ctx.UndoManager.Undo();
        frame.ShapesSave!.Shapes.Single().ShouldBeOfType<CircleSave>();
    }

    [Fact]
    public void DuplicateRect_WithCircleBefore_CopyJoinsRectGroup()
    {
        var (ctx, frame) = FrameWithNoShapes();
        ctx.AppCommands.AddCircle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        var rect = frame.ShapesSave!.AARectSaves.Single();

        var copy = ctx.AppCommands.DuplicateShape(rect);

        copy.ShouldBeOfType<AARectSave>();
        frame.ShapesSave.Shapes.Select(s => s.GetType()).ShouldBe(
            [typeof(AARectSave), typeof(AARectSave), typeof(CircleSave)]);
        AssertOrderSurvivesReload(ctx, frame);
    }

    [Fact]
    public void PasteMixedShapes_IntoFrame_LandsInFileOrder()
    {
        var (ctx, frame) = FrameWithNoShapes();
        ctx.AppCommands.AddCircle(frame);
        var pasted = new object[] { new CircleSave { Name = "pc" }, new AARectSave { Name = "pr" } };

        ctx.AppCommands.PasteShapes(frame, pasted);

        frame.ShapesSave!.Shapes.Select(s => s.GetType()).ShouldBe(
            [typeof(AARectSave), typeof(CircleSave), typeof(CircleSave)]);
        AssertOrderSurvivesReload(ctx, frame);
    }

    [Fact]
    public void CloneFrame_PreservesShapeOrder()
    {
        var source = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        source.ShapesSave.Add(new CircleSave { Name = "c" });
        source.ShapesSave.Add(new AARectSave { Name = "r" });

        var copy = AnimationCloneHelper.CloneFrame(source);

        copy.ShapesSave!.Shapes.Select(s => ((ShapeSave)s).Name).ShouldBe(["r", "c"]);
    }

    [Fact]
    public void ClipboardShapes_MixedOrder_RoundTripInFileOrder()
    {
        var text = ClipboardPayload.SerializeShapes(
            [new CircleSave { Name = "c" }, new AARectSave { Name = "r" }]);

        ClipboardPayload.TryDeserialize(text, out _, out _, out var shapes).ShouldBeTrue();

        shapes!.Select(s => ((ShapeSave)s).Name).ShouldBe(["r", "c"]);
    }

    [Fact]
    public void DeleteRectThenUndo_WithCircleAfter_RestoresOriginalOrder()
    {
        var (ctx, frame) = FrameWithNoShapes();
        ctx.AppCommands.AddCircle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        var before = frame.ShapesSave!.Shapes.ToArray();

        ctx.AppCommands.DeleteShapes([before[0]]);
        ctx.UndoManager.Undo();

        frame.ShapesSave!.Shapes.ShouldBe(before);
    }
}
