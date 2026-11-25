using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using AnimationEditor.SaveModel;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AnimationCloneHelperTests
{
    [Fact]
    public void CloneFrame_WithEvents_DeepCopiesEvents()
    {
        var source = new AnimationFrameSave();
        source.Events.Add(new AnimationFrameEvent { Name = "Footstep", Data = "left" });

        var copy = AnimationCloneHelper.CloneFrame(source);

        var copied = Assert.Single(copy.Events);
        Assert.Equal("Footstep", copied.Name);
        Assert.Equal("left", copied.Data);
        Assert.NotSame(source.Events[0], copied);
    }

    [Fact]
    public void CloneFrame_AllFieldsSetToNonDefaultValues_CopiesEveryField()
    {
        var source = new AnimationFrameSave
        {
            TextureName = "walk.png",
            LeftCoordinate = 0.1f,
            RightCoordinate = 0.9f,
            TopCoordinate = 0.2f,
            BottomCoordinate = 0.8f,
            FrameLength = 0.5f,
            FlipHorizontal = true,
            FlipVertical = true,
            FlipDiagonal = true,
            RelativeX = 3f,
            RelativeY = -4f,
            Red = 10,
            Green = 20,
            Blue = 30,
            Alpha = 40,
            ColorOperation = ColorOperation.Add,
        };

        var copy = AnimationCloneHelper.CloneFrame(source);

        Assert.Equal(source.TextureName, copy.TextureName);
        Assert.Equal(source.LeftCoordinate, copy.LeftCoordinate);
        Assert.Equal(source.RightCoordinate, copy.RightCoordinate);
        Assert.Equal(source.TopCoordinate, copy.TopCoordinate);
        Assert.Equal(source.BottomCoordinate, copy.BottomCoordinate);
        Assert.Equal(source.FrameLength, copy.FrameLength);
        Assert.Equal(source.FlipHorizontal, copy.FlipHorizontal);
        Assert.Equal(source.FlipVertical, copy.FlipVertical);
        Assert.Equal(source.FlipDiagonal, copy.FlipDiagonal);
        Assert.Equal(source.RelativeX, copy.RelativeX);
        Assert.Equal(source.RelativeY, copy.RelativeY);
        Assert.Equal(source.Red, copy.Red);
        Assert.Equal(source.Green, copy.Green);
        Assert.Equal(source.Blue, copy.Blue);
        Assert.Equal(source.Alpha, copy.Alpha);
        Assert.Equal(source.ColorOperation, copy.ColorOperation);
    }

    // #941: TextureName is a verbatim string copy with no path normalization, so a mixed-case
    // on-disk name must survive clone/duplicate/copy-paste unchanged.
    [Fact]
    public void CloneFrame_MixedCaseTextureName_PreservesExactCase()
    {
        var source = new AnimationFrameSave { TextureName = "Items.PNG" };

        var copy = AnimationCloneHelper.CloneFrame(source);

        Assert.Equal("Items.PNG", copy.TextureName);
    }

    // #937: cloning (duplicate frame/chain, copy-paste) must preserve ShapesSave presence like
    // every other path now does -- a shapeless source frame must not gain an empty ShapesSave
    // through the clone, or the copy bakes an empty shapesSave/ShapeCollectionSave block on save.
    [Fact]
    public void CloneFrame_SourceHasNullShapesSave_CopyAlsoNull()
    {
        var source = new AnimationFrameSave { TextureName = "walk.png", ShapesSave = null };

        var copy = AnimationCloneHelper.CloneFrame(source);

        Assert.Null(copy.ShapesSave);
    }

    [Fact]
    public void CloneFrame_SourceHasShapes_CopyHasClonedShapes()
    {
        var source = new AnimationFrameSave { TextureName = "walk.png" };
        source.ShapesSave = new ShapesSave();
        source.ShapesSave.Add(new AARectSave { Name = "HitBox" });

        var copy = AnimationCloneHelper.CloneFrame(source);

        Assert.NotNull(copy.ShapesSave);
        Assert.Single(copy.ShapesSave!.Shapes);
        Assert.NotSame(source.ShapesSave.Shapes[0], copy.ShapesSave.Shapes[0]);
    }

    public static TheoryData<string> CopyPaths => new() { "Clone", "Clipboard chain", "Clipboard frame", "Clipboard shapes" };

    // Guards against a field added to the save model later but never copied: every stored member, of every
    // shape type, gets a distinct non-default value by reflection, so the test fails as soon as a copy misses one.
    [Theory]
    [MemberData(nameof(CopyPaths))]
    public void Copy_EveryStoredMemberNonDefault_CopiesEveryStoredMember(string path)
    {
        int counter = 100;
        var source = (AnimationChainSave)SaveModelFill.CreateFilled(typeof(AnimationChainSave), ref counter);
        var frame = source.Frames[0];
        var differences = new List<string>();

        switch (path)
        {
            case "Clone":
                SaveModelFill.Compare(source, AnimationCloneHelper.CloneChain(source), "chain", differences);
                break;
            case "Clipboard chain":
                SaveModelFill.Compare(source, RoundTrip(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = [source] }).Chains![0], "chain", differences);
                break;
            case "Clipboard frame":
                SaveModelFill.Compare(frame, RoundTrip(new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = [frame] }).Frames![0], "frame", differences);
                break;
            case "Clipboard shapes":
                // One at a time and together: a single rectangle or circle has its own clipboard format.
                foreach (var shape in frame.ShapesSave!.Shapes)
                    SaveModelFill.Compare(shape, RoundTrip(new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = [shape] }).Shapes![0], shape.GetType().Name, differences);
                SaveModelFill.Compare(frame.ShapesSave.Shapes, RoundTrip(new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = frame.ShapesSave.Shapes }).Shapes, "shapes", differences);
                break;
        }

        differences.ShouldBeEmpty();
    }

    private static (List<AnimationChainSave>? Chains, List<AnimationFrameSave>? Frames, List<object>? Shapes) RoundTrip(
        CopySelectionPayload payload)
    {
        var unit = FrameLengthUnit.Seconds;
        ClipboardPayload.TryDeserialize(ClipboardPayload.SerializeFromPayload(payload, unit), unit, _ => null,
            out var chains, out var frames, out var shapes).ShouldBeTrue();
        return (chains, frames, shapes);
    }
}
