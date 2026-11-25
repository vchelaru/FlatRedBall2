using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

/// <summary>A looping animation with no length, or next to none, never loops forever. Each test has a time limit, so a hang fails.</summary>
public class SpriteNoLengthTests
{
    private const int TimeLimit = 10000;

    private static AnimationChain Looping(bool pingPong, bool reverse, params long[] frameTicks)
    {
        var chain = new AnimationChain { Name = "a", PingPong = pingPong, Reversed = reverse };
        foreach (long ticks in frameTicks)
        {
            var frame = new AnimationFrame { FrameLength = TimeSpan.FromTicks(ticks) };
            frame.Events.Add(new AnimationFrameEvent { Name = "e" });
            chain.Add(frame);
        }
        return chain;
    }

    // Every step raises each frame's event at most once, and returns.
    private static int EventsRaised(AnimationChain chain, TimeSpan step)
    {
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.PlayAnimation("a");
        int raised = 0;
        sprite.FrameEventRaised += _ => raised++;
        sprite.AnimateSelf(step.TotalSeconds);
        return raised;
    }

    [Theory(Timeout = TimeLimit)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 86400)]
    public async Task AnimateSelf_LoopingChainOfZeroLengthFrames_Returns(bool pingPong, bool reverse, int stepSeconds)
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
    public async Task AnimateSelf_LoopingChainOfOneTickAndAStepOfManyLoops_RaisesEachFramesEventsOnce(bool pingPong, bool reverse, int stepSeconds)
    {
        var chain = Looping(pingPong, reverse, 0, 1, 0);

        // A ping-pong shows each of its three frames twice, the end frames back to back (one entry each).
        (await Task.Run(() => EventsRaised(chain, TimeSpan.FromSeconds(stepSeconds)))).ShouldBe(pingPong ? 4 : 3);
    }

    [Fact(Timeout = TimeLimit)]
    public async Task AnimateSelf_HotReloadZeroesTheLengths_Returns()
    {
        var list = new AnimationChainList { Looping(false, false, 10_000_000, 10_000_000) };
        var sprite = new Sprite { AnimationChains = list };
        sprite.PlayAnimation("a");
        sprite.AnimateSelf(1.5);
        const string file = "<AnimationChainArraySave><AnimationChain><Name>a</Name><Frame><FrameLength>0</FrameLength></Frame><Frame><FrameLength>0</FrameLength></Frame></AnimationChain></AnimationChainArraySave>";
        var content = new ContentLoader { StreamProvider = _ => new MemoryStream(Encoding.UTF8.GetBytes(file)) };

        list.TryReloadFrom("a.achx", content).ShouldBeTrue();
        await Task.Run(() => sprite.AnimateSelf(86400));

        sprite.Animate.ShouldBeTrue();
    }
}
