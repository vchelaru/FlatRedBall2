using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FrameLengthUnitTests
{
    [Theory]
    [InlineData(TimeMeasurementUnit.Second, false)]
    [InlineData(TimeMeasurementUnit.Undefined, false)]
    [InlineData(TimeMeasurementUnit.Millisecond, true)]
    public void Of_ReadsTheFilesUnit_UndefinedIsSeconds(TimeMeasurementUnit unit, bool ms)
    {
        var list = new AnimationChainListSave { TimeMeasurementUnit = unit };
        Assert.Equal(ms, FrameLengthUnit.Of(list).IsMilliseconds);
    }

    [Fact]
    public void Of_NoList_IsSeconds() => Assert.False(FrameLengthUnit.Of(null).IsMilliseconds);

    [Fact]
    public void DefaultFrameLength_IsATenthOfASecondInTheFilesUnit()
    {
        Assert.Equal(0.1f, FrameLengthUnit.Seconds.DefaultFrameLength);
        Assert.Equal(0.1f, new FrameLengthUnit(TimeMeasurementUnit.Undefined).DefaultFrameLength);
        Assert.Equal(100f, FrameLengthUnit.Milliseconds.DefaultFrameLength);
    }

    [Fact]
    public void ToSeconds_And_FromSeconds_AreInverse()
    {
        Assert.Equal(0.25, FrameLengthUnit.Milliseconds.ToSeconds(250), 6);
        Assert.Equal(250f, FrameLengthUnit.Milliseconds.FromSeconds(0.25));
        Assert.Equal(0.25, FrameLengthUnit.Seconds.ToSeconds(0.25), 6);
    }

    [Fact]
    public void Convert_BetweenUnits_ScalesAndSameUnitIsIdentity()
    {
        Assert.Equal(100f, FrameLengthUnit.Milliseconds.Convert(0.1f, FrameLengthUnit.Seconds));
        Assert.Equal(0.1f, FrameLengthUnit.Seconds.Convert(100f, FrameLengthUnit.Milliseconds), 5);
        Assert.Equal(0.1f, FrameLengthUnit.Seconds.Convert(0.1f, new FrameLengthUnit(TimeMeasurementUnit.Undefined)));
    }

    [Fact]
    public void PlaybackSeconds_NonPositiveFallsBackToTheDefault()
    {
        Assert.Equal(0.1, FrameLengthUnit.Milliseconds.PlaybackSeconds(0), 6);
        Assert.Equal(0.2, FrameLengthUnit.Milliseconds.PlaybackSeconds(200), 6);
        Assert.Equal(0.2, FrameLengthUnit.Seconds.PlaybackSeconds(0.2f), 6);
    }

    [Fact]
    public void PlaybackSeconds_IsTheDecimalTheFileHolds()
    {
        FrameLengthUnit.Seconds.PlaybackSeconds(0.1f).ShouldBe(0.1);
        FrameLengthUnit.Milliseconds.PlaybackSeconds(16.667f).ShouldBe(0.016667);
    }

    // These need 8 or 9 digits, which a 7 digit decimal would round away from the file's value.
    [Theory]
    [InlineData(1 / 60f)]
    [InlineData(1 / 24f)]
    [InlineData(1 / 12f)]
    public void PlaybackSeconds_OfALengthWithMoreDigits_IsTheFloat(float length)
    {
        FrameLengthUnit.Seconds.PlaybackSeconds(length).ShouldBe((double)length);
    }

    [Fact]
    public void Format_UsesTheUnitSuffix()
    {
        Assert.Equal("0.10s", FrameLengthUnit.Seconds.Format(0.1f));
        Assert.Equal("100ms", FrameLengthUnit.Milliseconds.Format(100f));
    }
}

public class FrameLengthUnitTiledPinTests
{
    [Theory]
    [InlineData(TimeMeasurementUnit.Millisecond, 100f, 100)]
    [InlineData(TimeMeasurementUnit.Second, 0.1f, 100)]
    [InlineData(TimeMeasurementUnit.Undefined, 0.25f, 250)]
    public void TiledDuration_IsWholeMillisecondsInEitherUnit(TimeMeasurementUnit unit, float length, int expectedMs)
    {
        Assert.Equal(expectedMs, AnimationEditor.Core.Tiled.AchjToTiledAnimationMapper.FrameDurationMs(length, unit));
        Assert.Equal(expectedMs, new FrameLengthUnit(unit).ToMilliseconds(length));
    }
}
