using System.Reflection;
using System.Runtime.CompilerServices;
using FlatRedBall.AnimationChain;
using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

/// <summary>A frame's region must land on the pixels the AnimationEditor shows, which rounds its edges.</summary>
public class SourceRectangleRoundingTests
{
    // A Texture2D with a size and no device: the converter only reads Width and Height.
    private static Texture2D Texture(int size)
    {
        var texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        GC.SuppressFinalize(texture);
        foreach (var field in new[] { "width", "height" })
            typeof(Texture2D).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(texture, size);
        return texture;
    }

    private static (int? X, int? Width) Region(TextureCoordinateType type, int textureSize, float left, float right)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", LeftCoordinate = left, RightCoordinate = right, BottomCoordinate = type == TextureCoordinateType.UV ? 1 : textureSize });
        var save = new AnimationChainListSave { CoordinateType = type, AnimationChains = { chain } };
        var region = save.ToAnimationChainList(_ => Texture(textureSize))[0][0].SourceRectangle;
        return (region?.X, region?.Width);
    }

    [Theory]
    [InlineData(48, 32)]
    [InlineData(96, 64)]
    [InlineData(160, 48)]
    [InlineData(192, 144)]
    [InlineData(384, 256)]
    public void ToAnimationChainList_UvRegionOfWholePixels_KeepsItsWidth(int textureSize, int x) =>
        Assert.Equal((x, 16), Region(TextureCoordinateType.UV, textureSize, x / (float)textureSize, (x + 16) / (float)textureSize));

    [Fact]
    public void ToAnimationChainList_PixelRegionAHairOffAWholePixel_IsTheWholePixel() =>
        Assert.Equal((32, 16), Region(TextureCoordinateType.Pixel, 96, 31.999998f, 48));
}
