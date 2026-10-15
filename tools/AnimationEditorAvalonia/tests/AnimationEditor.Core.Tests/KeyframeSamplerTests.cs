using AnimationEditor.Core.Keyframes;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class KeyframeSamplerTests
{
    private static AnimationChainSave Slide()
    {
        var chain = new AnimationChainSave { Name = "Slide" };
        chain.Frames.Add(new AnimationFrameSave
        {
            FrameLength = 0.1f, RelativeX = 0, Red = 0, Green = 0, Blue = 0, Alpha = 0,
            ColorOperation = ColorOperation.Multiply,
        });
        chain.Frames.Add(new AnimationFrameSave
        {
            FrameLength = 0.1f, RelativeX = 100, Red = 200, Green = 100, Blue = 50, Alpha = 255,
        });
        return chain;
    }

    [Fact]
    public void Snap_HoldsTheFramesOwnValues()
    {
        var chain = Slide();

        var sample = KeyframeSampler.Sample(chain, 0, 0.09);

        Assert.Equal(0f, sample.OffsetX);
        Assert.Equal(0, sample.Color.Red);
        Assert.Equal(ColorOperation.Multiply, sample.Color.Operation);
    }

    [Fact]
    public void StoredLinearCurves_GlideOffsetAndColorAtTheSameTime()
    {
        var chain = Slide();
        chain.Frames[0].OffsetInterpolation = "Linear";
        chain.Frames[0].ColorInterpolation = "Linear";

        var sample = KeyframeSampler.Sample(chain, 0, 0.05);

        Assert.Equal(50f, sample.OffsetX, 3);
        Assert.Equal(100, sample.Color.Red);
        Assert.Equal(50, sample.Color.Green);
        Assert.Equal(25, sample.Color.Blue);
        Assert.Equal(128, sample.Color.Alpha);
        Assert.Equal(ColorOperation.Multiply, sample.Color.Operation);
    }

    [Fact]
    public void StoredCurve_IsUsedNotForcedLinear()
    {
        var chain = Slide();
        chain.Frames[0].OffsetInterpolation = "Quadratic";
        chain.Frames[0].OffsetEasing = "In";

        var sample = KeyframeSampler.Sample(chain, 0, 0.05);

        Assert.Equal(25f, sample.OffsetX, 3); // quadratic ease-in at t = 0.5
    }

    [Fact]
    public void KeyframeOff_AtFrameStart_ShowsTheGlideValue()
    {
        var chain = new AnimationChainSave { Name = "c" };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, RelativeX = i == 0 ? 0 : i == 1 ? 999 : 90 });
        chain.Frames[0].OffsetInterpolation = "Linear";
        chain.Frames[1].SkipOffsetKeyframe = true;

        var sample = KeyframeSampler.Sample(chain, 1, 0);

        Assert.Equal(45f, sample.OffsetX, 3);
    }

    [Fact]
    public void ColorUnsetEverywhere_StaysNullChannels()
    {
        var chain = new AnimationChainSave { Name = "c" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });

        var sample = KeyframeSampler.Sample(chain, 0, 0);

        Assert.Null(sample.Color.Red);
        Assert.Null(sample.Color.Alpha);
    }

    [Fact]
    public void EmptyOrMissingChain_IsZeroAndUnset()
    {
        Assert.Equal(0f, KeyframeSampler.Sample(null, 0, 0).OffsetX);
        Assert.Equal(0f, KeyframeSampler.Sample(new AnimationChainSave(), 3, 1).OffsetY);
    }

    [Fact]
    public void IsAnimated_OnlyWhenSomeFrameHasAGlidingCurve()
    {
        var chain = Slide();
        Assert.False(KeyframeSampler.IsAnimated(chain));

        chain.Frames[0].ColorInterpolation = "Bounce";
        Assert.True(KeyframeSampler.IsAnimated(chain));

        chain.Frames[0].ColorInterpolation = "Instant";
        Assert.False(KeyframeSampler.IsAnimated(chain));
    }
}
