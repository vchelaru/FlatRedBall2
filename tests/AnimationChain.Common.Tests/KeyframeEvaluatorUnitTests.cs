using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>#1829: the evaluator takes <c>elapsedInFrame</c> in seconds whatever unit the file's frame lengths are in.</summary>
public class KeyframeEvaluatorUnitTests
{
    private static AnimationChainSave Slide(float length)
    {
        var chain = new AnimationChainSave { Name = "slide", Loop = false };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = length, RelativeX = 0, OffsetInterpolation = "Linear" });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = length, RelativeX = 100 });
        return chain;
    }

    [Fact]
    public void MillisecondFile_GlidesByTheSecondsElapsed()
    {
        KeyframeEvaluator.EvaluateOffset(Slide(100f), 0, 0.05, TimeMeasurementUnit.Millisecond).X.ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void SecondsAndUndefinedFiles_GlideByTheSecondsElapsed()
    {
        KeyframeEvaluator.EvaluateOffset(Slide(0.1f), 0, 0.05, TimeMeasurementUnit.Second).X.ShouldBe(50f, 0.001f);
        KeyframeEvaluator.EvaluateOffset(Slide(0.1f), 0, 0.05, TimeMeasurementUnit.Undefined).X.ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void MillisecondFile_UnsetLengthCountsAsAHundredMilliseconds()
    {
        KeyframeEvaluator.EvaluateOffset(Slide(0f), 0, 0.05, TimeMeasurementUnit.Millisecond).X.ShouldBe(50f, 0.001f);
    }
}
