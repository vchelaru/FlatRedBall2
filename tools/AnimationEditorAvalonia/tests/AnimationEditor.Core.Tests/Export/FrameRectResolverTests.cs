using AnimationEditor.Core.Export;
using FlatRedBall2.Animation.Content;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests.Export;

public class FrameRectResolverTests
{
    private static AnimationFrameSave Frame(
        string textureName, float left, float top, float right, float bottom) =>
        new()
        {
            TextureName = textureName,
            LeftCoordinate = left,
            TopCoordinate = top,
            RightCoordinate = right,
            BottomCoordinate = bottom,
        };

    [Fact]
    public void TryBuildRect_PixelCoordinates_UsesCoordsDirectlyWithoutResolver()
    {
        var frame = Frame("hero.png", 8f, 8f, 32f, 32f);

        bool ok = FrameRectResolver.TryBuildRect(
            frame, TextureCoordinateType.Pixel, _ => null, out var rect);

        Assert.True(ok);
        Assert.Equal((8, 8, 24, 24), (rect.X, rect.Y, rect.W, rect.H));
    }

    [Fact]
    public void TryBuildRect_UvCoordinates_ScalesByTextureSize()
    {
        var frame = Frame("hero.png", 0f, 0f, 0.5f, 0.5f);

        bool ok = FrameRectResolver.TryBuildRect(
            frame, TextureCoordinateType.UV, _ => (64, 64), out var rect);

        Assert.True(ok);
        Assert.Equal((0, 0, 32, 32), (rect.X, rect.Y, rect.W, rect.H));
    }

    [Fact]
    public void TryBuildRect_UvCoordinates_WhenTextureUnresolvable_ReturnsFalse()
    {
        var frame = Frame("missing.png", 0f, 0f, 1f, 1f);

        bool ok = FrameRectResolver.TryBuildRect(
            frame, TextureCoordinateType.UV, _ => null, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryBuildRect_UvCoordinates_RoundsEdgesAwayFromZero()
    {
        // 0.5 * 5 = 2.5 → AwayFromZero → 3 on right/bottom edges.
        var frame = Frame("hero.png", 0f, 0f, 0.5f, 0.5f);

        bool ok = FrameRectResolver.TryBuildRect(
            frame, TextureCoordinateType.UV, _ => (5, 5), out var rect);

        Assert.True(ok);
        Assert.Equal((0, 0, 3, 3), (rect.X, rect.Y, rect.W, rect.H));
    }
}
