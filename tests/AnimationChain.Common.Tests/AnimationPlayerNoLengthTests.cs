using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A looping animation with no length, or next to none, never loops forever. Each test has a time limit, so a hang fails.</summary>
public class AnimationPlayerNoLengthTests
{
    private const int TimeLimit = 10000;

    private sealed class Frame : AnimationFrameBase { }

    private static AnimationChain<Frame> Looping(bool pingPong, bool reverse, params long[] frameTicks)
    {
        var chain = new AnimationChain<Frame> { Name = "a", PingPong = pingPong, Reversed = reverse };
        foreach (long ticks in frameTicks)
        {
            var frame = new Frame { FrameLength = TimeSpan.FromTicks(ticks) };
            frame.Events.Add(new AnimationFrameEvent { Name = "e" });
            chain.Add(frame);
        }
        return chain;
    }

    // Every step raises each frame's event at most once, and returns.
    private static int EventsRaised(AnimationChain<Frame> chain, TimeSpan step)
    {
        var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
        player.Play("a");
        int raised = 0;
        player.FrameEventRaised += _ => raised++;
        player.Update(step);
        return raised;
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 86400)]
    public async Task Update_LoopingChainOfZeroLengthFrames_Returns(bool pingPong, bool reverse, int stepSeconds)
    {
        var chain = Looping(pingPong, reverse, 0, 0, 0);

        (await Task.Run(() => EventsRaised(chain, TimeSpan.FromSeconds(stepSeconds)))).ShouldBe(0);
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 86400)]
    [InlineData(true, true, 86400)]
    public async Task Update_LoopingChainOfOneTickAndAStepOfManyLoops_RaisesEachFramesEventsOnce(bool pingPong, bool reverse, int stepSeconds)
    {
        var chain = Looping(pingPong, reverse, 0, 1, 0);

        // A ping-pong shows each of its three frames twice, the end frames back to back (one entry each).
        (await Task.Run(() => EventsRaised(chain, TimeSpan.FromSeconds(stepSeconds)))).ShouldBe(pingPong ? 4 : 3);
    }

    [Fact(Timeout = TimeLimit)]
    public async Task Update_HotReloadZeroesTheLengths_Returns()
    {
        var list = new AnimationChainList<Frame> { Looping(false, false, 10_000_000, 10_000_000) };
        var player = new AnimationPlayer<Frame>(list);
        player.Play("a");
        player.Update(TimeSpan.FromSeconds(1.5));
        const string file = "<AnimationChainArraySave><AnimationChain><Name>a</Name><Frame><FrameLength>0</FrameLength></Frame><Frame><FrameLength>0</FrameLength></Frame></AnimationChain></AnimationChainArraySave>";

        list.TryReloadFrom("a.achx", _ => new MemoryStream(Encoding.UTF8.GetBytes(file)), _ => new Frame()).ShouldBeTrue();
        await Task.Run(() => player.Update(TimeSpan.FromDays(1)));

        player.CurrentFrame.ShouldBe(list[0][1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Update_LoopingChainOfNoLengthIsGivenFrames_StartsAtZero(int zeroLengthFrames)
    {
        var chain = Looping(false, false, new long[zeroLengthFrames]);
        var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
        player.Play("a");
        for (int i = 0; i < 60; i++) player.Update(TimeSpan.FromSeconds(1 / 60.0));
        int raised = 0;
        player.FrameEventRaised += _ => raised++;

        chain.Clear();
        chain.AddRange(Looping(false, false, 5_000_000, 5_000_000, 5_000_000));
        player.Update(TimeSpan.Zero);

        player.CurrentFrame.ShouldBe(chain[0]);
        raised.ShouldBe(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Update_ZeroStepOnAChainOfTenTicksOrLess_DoesNotLoop(long ticks)
    {
        var chain = Looping(false, false, ticks);

        EventsRaised(chain, TimeSpan.Zero).ShouldBe(0);
    }
}
