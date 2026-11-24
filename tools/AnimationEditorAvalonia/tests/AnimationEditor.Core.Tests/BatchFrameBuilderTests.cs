using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class BatchFrameBuilderTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    static AnimationFrameSave MakeLastFrame(
        float left, float right, float top, float bottom,
        string texture = "Tex.png", float frameLength = 0.1f)
        => new AnimationFrameSave
        {
            TextureName      = texture,
            FrameLength      = frameLength,
            LeftCoordinate   = left,
            RightCoordinate  = right,
            TopCoordinate    = top,
            BottomCoordinate = bottom,
            ShapesSave = new FlatRedBall2.AnimationEditorCommon.ShapesSave()
        };

    // ── Count / no-op ─────────────────────────────────────────────────────

    [Fact]
    public void ZeroCount_ReturnsEmpty()
    {
        var result = BatchFrameBuilder.BuildBatch(null, 0, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, null);
        Assert.Empty(result.Frames);
    }

    [Fact]
    public void Count_Returns_CorrectNumberOfFrames()
    {
        var result = BatchFrameBuilder.BuildBatch(null, 5, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, null);
        Assert.Equal(5, result.Frames.Count);
    }

    // ── No lastFrame (null) ───────────────────────────────────────────────

    [Fact]
    public void NoLastFrame_EachFrameHasDefaultUV()
    {
        var result = BatchFrameBuilder.BuildBatch(null, 3, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, null);
        foreach (var f in result.Frames)
        {
            Assert.Equal(0f, f.LeftCoordinate,   precision: 5);
            Assert.Equal(1f, f.RightCoordinate,  precision: 5);
            Assert.Equal(0f, f.TopCoordinate,    precision: 5);
            Assert.Equal(1f, f.BottomCoordinate, precision: 5);
            Assert.Equal(0.1f, f.FrameLength,    precision: 5);
        }
    }

    // ── Without increment (copy region) ───────────────────────────────────────

    [Fact]
    public void WithoutIncrement_AllFramesCopyLastFrameUV()
    {
        var last = MakeLastFrame(16f, 32f, 0f, 32f);
        var result = BatchFrameBuilder.BuildBatch(last, 3, incrementUV: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));

        foreach (var f in result.Frames)
        {
            Assert.Equal(16f, f.LeftCoordinate,   precision: 5);
            Assert.Equal(32f,  f.RightCoordinate,  precision: 5);
            Assert.Equal(0f,    f.TopCoordinate,    precision: 5);
            Assert.Equal(32f,  f.BottomCoordinate, precision: 5);
        }
    }

    [Fact]
    public void WithoutIncrement_CopiesTextureAndFrameLength()
    {
        var last = MakeLastFrame(0f, 64f, 0f, 64f, texture: "Hero.png", frameLength: 0.25f);
        var result = BatchFrameBuilder.BuildBatch(last, 2, incrementUV: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));
        foreach (var f in result.Frames)
        {
            Assert.Equal("Hero.png", f.TextureName);
            Assert.Equal(0.25f, f.FrameLength, precision: 5);
        }
    }

    // ── With increment — horizontal traversal ────────────────────────────

    [Fact]
    public void WithIncrement_4x4Sheet_AdvancesRightThenWraps()
    {
        // 4×4 cells of 16 pixels; last frame is cell (0,0)
        // Adding 3 more: cell(1,0), cell(2,0), cell(3,0)
        var last = MakeLastFrame(0f, 16f, 0f, 16f);
        var result = BatchFrameBuilder.BuildBatch(last, 3, incrementUV: true, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));

        Assert.Equal(3, result.Frames.Count);

        Assert.Equal(16f, result.Frames[0].LeftCoordinate,  precision: 4);
        Assert.Equal(32f, result.Frames[0].RightCoordinate, precision: 4);
        Assert.Equal(0f,    result.Frames[0].TopCoordinate,   precision: 4);

        Assert.Equal(32f, result.Frames[1].LeftCoordinate,  precision: 4);
        Assert.Equal(48f, result.Frames[1].RightCoordinate, precision: 4);
        Assert.Equal(0f,    result.Frames[1].TopCoordinate,   precision: 4);

        Assert.Equal(48f, result.Frames[2].LeftCoordinate,  precision: 4);
        Assert.Equal(64f, result.Frames[2].RightCoordinate, precision: 4);
        Assert.Equal(0f,    result.Frames[2].TopCoordinate,   precision: 4);
    }

    [Fact]
    public void WithIncrement_WrapsToNextRow_WhenEndOfRowReached()
    {
        // 4×4 sheet; last frame is cell (3,0)
        // Next frame should be cell (0,1)
        var last = MakeLastFrame(48f, 64f, 0f, 16f);
        var result = BatchFrameBuilder.BuildBatch(last, 1, incrementUV: true, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));

        Assert.Single(result.Frames);
        Assert.Equal(0f,    result.Frames[0].LeftCoordinate,   precision: 4);
        Assert.Equal(16f, result.Frames[0].RightCoordinate,  precision: 4);
        Assert.Equal(16f, result.Frames[0].TopCoordinate,    precision: 4);
        Assert.Equal(32f, result.Frames[0].BottomCoordinate, precision: 4);
    }

    [Fact]
    public void WithIncrement_BeyondBottomOfTexture_SetsExceededTextureBounds()
    {
        // 2×2 sheet; last frame is bottom-right cell (1,1)
        // One more frame would go beyond the texture (B > 64)
        var last = MakeLastFrame(32f, 64f, 32f, 64f);
        var result = BatchFrameBuilder.BuildBatch(last, 1, incrementUV: true, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));
        Assert.True(result.ExceededTextureBounds);
    }

    [Fact]
    public void WithIncrement_StillCreatesFramesEvenWhenExceedingBounds()
    {
        var last = MakeLastFrame(32f, 64f, 32f, 64f);
        var result = BatchFrameBuilder.BuildBatch(last, 3, incrementUV: true, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));
        Assert.Equal(3, result.Frames.Count); // frames created, warning is caller's job
    }

    [Fact]
    public void WithIncrement_WithinBounds_DoesNotSetExceededFlag()
    {
        // 4×4 sheet; first cell, adding 1 more — still within bounds
        var last = MakeLastFrame(0f, 16f, 0f, 16f);
        var result = BatchFrameBuilder.BuildBatch(last, 1, incrementUV: true, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));
        Assert.False(result.ExceededTextureBounds);
    }

    // ── ShapesSave left null until a shape is actually added (#937) ─────────

    [Fact]
    public void AllFrames_HaveNullShapesSave()
    {
        var result = BatchFrameBuilder.BuildBatch(null, 4, incrementUV: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds, (64, 64));
        Assert.All(result.Frames, f => Assert.Null(f.ShapesSave));
    }
}
