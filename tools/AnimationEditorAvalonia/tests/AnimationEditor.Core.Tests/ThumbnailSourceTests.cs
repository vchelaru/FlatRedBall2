using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ThumbnailSourceTests
{
    // ── FromChain ─────────────────────────────────────────────────────────────

    [Fact]
    public void FromChain_ChainWithNoFrames_ReturnsNull()
    {
        var chain = new AnimationChainSave { Name = "Empty" };

        Assert.Null(ThumbnailSource.FromChain(chain));
    }

    [Fact]
    public void FromChain_CapturesFirstFrameTextureAndRegion()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName     = "sheet.png",
            LeftCoordinate  = 0f,   TopCoordinate    = 0f,
            RightCoordinate = 0.5f, BottomCoordinate = 1f,
        });
        // Second frame must not influence the signature.
        chain.Frames.Add(new AnimationFrameSave { TextureName = "other.png" });

        var source = ThumbnailSource.FromChain(chain);

        Assert.Equal(new ThumbnailSource("sheet.png", 0f, 0.5f, 0f, 1f, false, false), source);
    }

    [Fact]
    public void FromChain_FirstFrameFlipHorizontal_ProducesUnequalSignature()
    {
        var unflipped = new AnimationChainSave { Name = "A" };
        unflipped.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png" });
        var flipped = new AnimationChainSave { Name = "B" };
        flipped.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FlipHorizontal = true });

        Assert.NotEqual(ThumbnailSource.FromChain(unflipped), ThumbnailSource.FromChain(flipped));
    }

    [Fact]
    public void FromChain_FirstFrameFlipVertical_ProducesUnequalSignature()
    {
        var unflipped = new AnimationChainSave { Name = "A" };
        unflipped.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png" });
        var flipped = new AnimationChainSave { Name = "B" };
        flipped.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FlipVertical = true });

        Assert.NotEqual(ThumbnailSource.FromChain(unflipped), ThumbnailSource.FromChain(flipped));
    }

    [Fact]
    public void FromChain_DifferentFirstFrameTexture_ProducesUnequalSignature()
    {
        var red = new AnimationChainSave { Name = "Red" };
        red.Frames.Add(new AnimationFrameSave { TextureName = "red.png" });
        var blue = new AnimationChainSave { Name = "Blue" };
        blue.Frames.Add(new AnimationFrameSave { TextureName = "blue.png" });

        Assert.NotEqual(ThumbnailSource.FromChain(red), ThumbnailSource.FromChain(blue));
    }

    [Fact]
    public void FromChain_SameTextureDifferentRegion_ProducesUnequalSignature()
    {
        // Two frames on the same sheet but different UV regions — a region edit on the
        // first frame must be detected as a change.
        var leftHalf = new AnimationChainSave { Name = "Left" };
        leftHalf.Frames.Add(new AnimationFrameSave
        {
            TextureName = "sheet.png", RightCoordinate = 0.5f, BottomCoordinate = 1f,
        });
        var rightHalf = new AnimationChainSave { Name = "Right" };
        rightHalf.Frames.Add(new AnimationFrameSave
        {
            TextureName = "sheet.png", LeftCoordinate = 0.5f, RightCoordinate = 1f, BottomCoordinate = 1f,
        });

        Assert.NotEqual(ThumbnailSource.FromChain(leftHalf), ThumbnailSource.FromChain(rightHalf));
    }
}
