using AnimationEditor.Core.IO;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
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
        source.ShapesSave.Shapes.Add(new AARectSave { Name = "HitBox" });

        var copy = AnimationCloneHelper.CloneFrame(source);

        Assert.NotNull(copy.ShapesSave);
        Assert.Single(copy.ShapesSave!.Shapes);
        Assert.NotSame(source.ShapesSave.Shapes[0], copy.ShapesSave.Shapes[0]);
    }
}
