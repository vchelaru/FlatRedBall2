using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class PixelFrameUvConverterTests
{
    // Issue #953: the PNG usage-overlay scan needs matched frames in pixel coordinates regardless
    // of the source .achx's on-disk CoordinateType. The project-tree thumbnail (#839) uses it too.
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
