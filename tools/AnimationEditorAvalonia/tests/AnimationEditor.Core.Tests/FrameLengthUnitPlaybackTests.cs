using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #1829: playback does not depend on the unit a file is written in. Every test builds the same animation in
/// seconds and in milliseconds and expects the same result; a Millisecond file used to play 1000 times too slowly.
/// </summary>
public class FrameLengthUnitPlaybackTests
{
    private static readonly FrameLengthUnit Ms = FrameLengthUnit.Milliseconds;
    private static readonly FrameLengthUnit Sec = FrameLengthUnit.Seconds;

    /// <summary>Frames of 0.1 s, 0.2 s and 0.3 s, offsets 0, 10, 20, glided linearly, written in <paramref name="unit"/>.</summary>
    private static AnimationChainSave Walk(FrameLengthUnit unit, bool loop = true)
    {
        var chain = new AnimationChainSave { Name = "Walk", Loop = loop };
        float[] seconds = { 0.1f, 0.2f, 0.3f };
        for (int i = 0; i < seconds.Length; i++)
            chain.Frames.Add(new AnimationFrameSave
            {
                FrameLength = unit.FromSeconds(seconds[i]),
                RelativeX = i * 10,
                OffsetInterpolation = "Linear",
            });
        return chain;
    }

    [Fact]
    public void Controller_AdvancesThroughTheFramesAtTheSameSeconds_InEitherUnit()
    {
        var ms = new PlaybackController();
        var sec = new PlaybackController();
        ms.SetChain(Walk(Ms), Ms);
        sec.SetChain(Walk(Sec), Sec);

        // 65 ms steps never land on a frame boundary, where the float 0.1f and the double 0.1 differ by a hair.
        for (int step = 0; step < 12; step++)
        {
            ms.Advance(0.065);
            sec.Advance(0.065);
            Assert.True(sec.CurrentFrameIndex == ms.CurrentFrameIndex, $"step {step}: seconds {sec.CurrentFrameIndex} at {sec.AnimTime}, ms {ms.CurrentFrameIndex} at {ms.AnimTime}");
            Assert.Equal(sec.FrameElapsed, ms.FrameElapsed, precision: 4);
            Assert.Equal(sec.AnimTime, ms.AnimTime, precision: 4);
        }
    }

    [Fact]
    public void Controller_MillisecondFile_StepsToTheSecondFrameAfterOneHundredMilliseconds()
    {
        var controller = new PlaybackController();
        controller.SetChain(Walk(Ms), Ms);

        controller.Advance(0.15);

        Assert.Equal(1, controller.CurrentFrameIndex);
        Assert.Equal(0.05, controller.FrameElapsed, precision: 4);
    }

    [Fact]
    public void Controller_NonLoopingMillisecondFile_StopsAfterItsTotalDuration()
    {
        var controller = new PlaybackController();
        controller.SetChain(Walk(Ms, loop: false), Ms);

        controller.Advance(0.7);

        Assert.Equal(2, controller.CurrentFrameIndex);
        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void Controller_SeekToFrame_UsesSecondsInEitherUnit()
    {
        var ms = new PlaybackController();
        var sec = new PlaybackController();
        ms.SetChain(Walk(Ms), Ms);
        sec.SetChain(Walk(Sec), Sec);

        ms.SeekToFrame(1, 0.5);
        sec.SeekToFrame(1, 0.5);

        Assert.Equal(sec.AnimTime, ms.AnimTime, precision: 4);
        Assert.Equal(0.1, ms.FrameElapsed, precision: 4);
    }

    [Theory]
    [InlineData(TimeMeasurementUnit.Millisecond)]
    [InlineData(TimeMeasurementUnit.Undefined)]
    public void Controller_ZeroLengthFrames_ShowTheLastFrame(TimeMeasurementUnit unit)
    {
        var chain = new AnimationChainSave { Name = "Z" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f });
        var controller = new PlaybackController();
        controller.SetChain(chain, new FrameLengthUnit(unit));

        controller.Advance(0.15);

        Assert.Equal(1, controller.CurrentFrameIndex);
    }

    [Fact]
    public void ChainClock_TotalAndLocate_AreInSecondsInEitherUnit()
    {
        Assert.Equal(0.6, ChainClock.TotalLength(Walk(Ms), Ms), precision: 4);
        Assert.Equal(ChainClock.TotalLength(Walk(Sec), Sec), ChainClock.TotalLength(Walk(Ms), Ms), precision: 4);

        var (indexMs, elapsedMs) = ChainClock.Locate(Walk(Ms), 0.45, Ms);
        var (indexSec, elapsedSec) = ChainClock.Locate(Walk(Sec), 0.45, Sec);

        Assert.Equal(indexSec, indexMs);
        Assert.Equal(2, indexMs);
        Assert.Equal(elapsedSec, elapsedMs, precision: 4);
    }

    [Fact]
    public void KeyframeSampler_GlidesAtTheSameSecondsInEitherUnit()
    {
        var sampleMs = KeyframeSampler.Sample(Walk(Ms), Ms, 1, 0.1);
        var sampleSec = KeyframeSampler.Sample(Walk(Sec), Sec, 1, 0.1);

        Assert.Equal(sampleSec.OffsetX, sampleMs.OffsetX, precision: 3);
        Assert.Equal(15f, sampleMs.OffsetX, precision: 3);
    }

    [Fact]
    public void ExportPlan_IsTheSameInEitherUnit()
    {
        var planMs = ExportTimelinePlanner.Plan(new[] { new ExportSourceChain(Walk(Ms), Ms) }, 50, 100, 2);
        var planSec = ExportTimelinePlanner.Plan(new[] { new ExportSourceChain(Walk(Sec), Sec) }, 50, 100, 2);

        Assert.Equal(planSec.Select(f => f.DelayTicks), planMs.Select(f => f.DelayTicks));
        Assert.Equal(
            planSec.Select(f => MathF.Round(f.Layers[0].OffsetX, 2)),
            planMs.Select(f => MathF.Round(f.Layers[0].OffsetX, 2)));
        Assert.Equal(60, planMs.Sum(f => f.DelayTicks));
    }
}
