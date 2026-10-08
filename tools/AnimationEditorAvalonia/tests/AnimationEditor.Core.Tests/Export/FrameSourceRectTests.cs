using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Export;

public class FrameSourceRectTests
{
    [Fact]
    public void TryResolve_PixelCoordinates_UsedDirectlyWithoutResolver()
    {
        var frame = new AnimationFrameSave { LeftCoordinate = 8f, TopCoordinate = 8f, RightCoordinate = 32f, BottomCoordinate = 32f };

        // A resolver returning null proves pixel input never consults it.
        bool ok = FrameSourceRect.TryResolve(frame, TextureCoordinateType.Pixel, _ => null, out var region);

        ok.ShouldBeTrue();
        region.ShouldBe(new FrameSourceRect(8, 8, 24, 24));
    }

    [Fact]
    public void TryResolve_UvCoordinates_ScaledByTextureSize()
    {
        var frame = new AnimationFrameSave { TextureName = "hero.png", LeftCoordinate = 0.5f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 0.25f };

        bool ok = FrameSourceRect.TryResolve(frame, TextureCoordinateType.UV, _ => (64, 128), out var region);

        ok.ShouldBeTrue();
        region.ShouldBe(new FrameSourceRect(32, 0, 32, 32));
    }

    [Fact]
    public void TryResolve_UvCoordinatesWithUnreadableTexture_ReturnsFalse()
    {
        var frame = new AnimationFrameSave { TextureName = "missing.png" };

        FrameSourceRect.TryResolve(frame, TextureCoordinateType.UV, _ => null, out _).ShouldBeFalse();
    }
}
