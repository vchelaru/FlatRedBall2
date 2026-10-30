using FlatRedBall.AnimationChain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

public class SpriteBatchExtensionsTests
{
    [Fact]
    public void ResolveBaseColor_NoColorGiven_DefaultsToWhite()
    {
        var result = SpriteBatchExtensions.ResolveBaseColor(null);

        Assert.Equal(Color.White, result);
    }

    [Fact]
    public void ResolveBaseColor_ColorGiven_ReturnsThatColorUnchanged()
    {
        var result = SpriteBatchExtensions.ResolveBaseColor(Color.Red);

        Assert.Equal(Color.Red, result);
    }

    [Fact]
    public void ResolveOrigin_NoOriginGiven_DefaultsToCenter()
    {
        var result = SpriteBatchExtensions.ResolveOrigin(null, width: 32, height: 16);

        Assert.Equal(new Vector2(16f, 8f), result);
    }

    [Fact]
    public void ResolveOrigin_OriginGiven_ReturnsThatOriginUnchanged()
    {
        var result = SpriteBatchExtensions.ResolveOrigin(Vector2.Zero, width: 32, height: 16);

        Assert.Equal(Vector2.Zero, result);
    }
}

public class SpriteBatchExtensionsFlipTests
{
    static AnimationFrame Frame(bool h = false, bool v = false, bool d = false) =>
        new() { FlipHorizontal = h, FlipVertical = v, FlipDiagonal = d };

    [Theory]
    [InlineData(false, false, SpriteEffects.None)]
    [InlineData(true, false, SpriteEffects.FlipHorizontally)]
    [InlineData(false, true, SpriteEffects.FlipVertically)]
    [InlineData(true, true, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically)]
    public void ResolveFlipTransform_NoDiagonal_MapsFlagsToEffects(bool h, bool v, SpriteEffects expected)
    {
        var (rotation, effects) = SpriteBatchExtensions.ResolveFlipTransform(Frame(h, v));

        Assert.Equal(0f, rotation);
        Assert.Equal(expected, effects);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResolveFlipTransform_Diagonal_RotatesNinetyDegrees(bool h, bool v)
    {
        var (rotation, _) = SpriteBatchExtensions.ResolveFlipTransform(Frame(h, v, d: true));

        Assert.Equal(MathF.PI / 2f, MathF.Abs(rotation), 5);
    }

    [Fact]
    public void ResolveFlipTransform_DiagonalOnly_IsPositiveQuarterTurnWithVerticalMirror()
    {
        // Plain transpose (x,y)->(y,x) in Y-down screen space: rotate +90 and mirror vertically.
        var (rotation, effects) = SpriteBatchExtensions.ResolveFlipTransform(Frame(d: true));

        Assert.Equal(MathF.PI / 2f, rotation, 5);
        Assert.Equal(SpriteEffects.FlipVertically, effects);
    }
}
