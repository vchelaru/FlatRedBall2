using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #839: .achx files saved with CoordinateType=Pixel store raw pixel rectangles, but
// ThumbnailService.RenderFrameThumbnail expects normalized (0-1) UV -- the project-tree thumbnail
// generator converts using the already-decoded texture's pixel size before cropping.
public class PixelFrameUvConverterTests
{
    [Fact]
    public void ToUv_PixelCoordinates_DividesByTextureSize()
    {
        var frame = new AnimationFrameSave
        {
            LeftCoordinate = 16f, RightCoordinate = 48f,
            TopCoordinate = 0f, BottomCoordinate = 32f,
        };

        var uv = PixelFrameUvConverter.ToUv(frame, textureWidth: 64, textureHeight: 64);

        Assert.Equal(0.25f, uv.LeftCoordinate, tolerance: 0.001f);
        Assert.Equal(0.75f, uv.RightCoordinate, tolerance: 0.001f);
        Assert.Equal(0f, uv.TopCoordinate, tolerance: 0.001f);
        Assert.Equal(0.5f, uv.BottomCoordinate, tolerance: 0.001f);
    }

    [Fact]
    public void ToUv_PreservesFlipFlags()
    {
        var frame = new AnimationFrameSave { FlipHorizontal = true, FlipVertical = true };

        var uv = PixelFrameUvConverter.ToUv(frame, textureWidth: 32, textureHeight: 32);

        Assert.True(uv.FlipHorizontal);
        Assert.True(uv.FlipVertical);
    }
}
