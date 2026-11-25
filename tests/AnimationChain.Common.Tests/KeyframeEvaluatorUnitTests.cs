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
    public void MillisecondFile_FrameOfNoLengthTakesNoTime()
    {
        KeyframeEvaluator.EvaluateOffset(Slide(0f), 0, 0.05, TimeMeasurementUnit.Millisecond).X.ShouldBe(0f);
    }

    // An unset length is a length of 0, in the game and the editor: both readers leave it at 0, so nothing can tell them apart.
    [Theory]
    [InlineData("a.achx", "<AnimationChainArraySave><TimeMeasurementUnit>Millisecond</TimeMeasurementUnit><AnimationChain><Name>a</Name><Frame><TextureName>a.png</TextureName></Frame><Frame><FrameLength>0</FrameLength></Frame></AnimationChain></AnimationChainArraySave>")]
    [InlineData("a.achj", """{ "timeMeasurementUnit": "Millisecond", "animationChains": [ { "name": "a", "frames": [ { "textureName": "a.png" }, { "frameLength": 0 } ] } ] }""")]
    public void UnsetLength_IsReadAsZero(string file, string content)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + file);
        System.IO.File.WriteAllText(path, content);
        try
        {
            var chain = AnimationChainListSave.FromFile(path).AnimationChains[0];

            foreach (var frame in chain.Frames) frame.GetDuration(TimeMeasurementUnit.Millisecond).ShouldBe(System.TimeSpan.Zero);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
