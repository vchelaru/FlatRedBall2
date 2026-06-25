using AnimationEditor.App;
using FlatRedBall2.Animation;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

// FrameColorFilter is the pure mapping from a frame's ColorOperation + RGB to a SkiaSharp blend filter.
// It is the testable core of the preview's color rendering; DrawFrameCore is the thin wiring on top.
public class FrameColorFilterTests
{
    [Fact]
    public void Resolve_NullOperation_ReturnsNull()
    {
        Assert.Null(FrameColorFilter.Resolve(null, 255, 0, 0));
    }

    [Fact]
    public void Resolve_Multiply_UsesModulateAndDefaultsUnsetChannelsTo255()
    {
        // 255 is the multiply identity, so an unset channel leaves that channel untouched.
        var result = FrameColorFilter.Resolve(ColorOperation.Multiply, 128, null, 64);

        Assert.NotNull(result);
        Assert.Equal(SKBlendMode.Modulate, result.Value.Mode);
        Assert.Equal(new SKColor(128, 255, 64, 255), result.Value.Color);
    }

    [Fact]
    public void Resolve_Add_UsesPlusAndDefaultsUnsetChannelsTo0WithZeroAlpha()
    {
        // 0 is the add identity; alpha 0 so an additive flash never forces the sprite opaque.
        var result = FrameColorFilter.Resolve(ColorOperation.Add, 200, null, null);

        Assert.NotNull(result);
        Assert.Equal(SKBlendMode.Plus, result.Value.Mode);
        Assert.Equal(new SKColor(200, 0, 0, 0), result.Value.Color);
    }
}
