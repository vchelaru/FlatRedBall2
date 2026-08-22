using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
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

    // Issue #953: the PNG usage-overlay scan needs matched frames in pixel coordinates regardless
    // of the source .achx's on-disk CoordinateType, so a UV-format frame needs the inverse of ToUv.
    [Fact]
    public void ToPixel_UvCoordinates_MultipliesByTextureSize()
    {
        var frame = new AnimationFrameSave
        {
            LeftCoordinate = 0.25f, RightCoordinate = 0.75f,
            TopCoordinate = 0f, BottomCoordinate = 0.5f,
        };

        var pixel = PixelFrameUvConverter.ToPixel(frame, textureWidth: 64, textureHeight: 64);

        Assert.Equal(16f, pixel.LeftCoordinate, tolerance: 0.001f);
        Assert.Equal(48f, pixel.RightCoordinate, tolerance: 0.001f);
        Assert.Equal(0f, pixel.TopCoordinate, tolerance: 0.001f);
        Assert.Equal(32f, pixel.BottomCoordinate, tolerance: 0.001f);
    }
}
