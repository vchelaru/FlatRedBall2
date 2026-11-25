using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>What <see cref="AnimationChainList{TFrame}.TryReloadFrom(string, Func{string, Stream}, Func{AnimationFrameSave, TFrame})"/> does with an edited file.</summary>
public class HotReloadMergeTests
{
    private sealed class Frame : AnimationFrameBase { }

    private static string Achx(params string[] chains) => $"<AnimationChainArraySave>{string.Concat(chains)}</AnimationChainArraySave>";

    private static string Chain(string name, string flags, params string[] lengths) =>
        $"<AnimationChain><Name>{name}</Name>{flags}"
        + string.Concat(lengths.Select(length => $"<Frame><FrameLength>{length}</FrameLength></Frame>")) + "</AnimationChain>";

    private static Frame Make(AnimationFrameSave save) => new() { FrameLength = TimeSpan.FromSeconds(save.FrameLength) };

    private static bool Reload(AnimationChainList<Frame> list, string text, string path = "a.achx") =>
        list.TryReloadFrom(path, _ => new MemoryStream(Encoding.UTF8.GetBytes(text)), Make);

    private static AnimationChainList<Frame> Load(string text)
    {
        var list = new AnimationChainList<Frame>();
        foreach (var save in AnimationChainListSave.FromString(text).AnimationChains)
        {
            var chain = new AnimationChain<Frame> { Name = save.Name };
            chain.AddRange(save.Frames.Select(Make));
            list.Add(chain);
        }
        return list;
    }

    [Fact]
    public void TryReloadFrom_FlagsChangedInFile_ChainTakesThem()
    {
        var list = Load(Achx(Chain("Walk", "", "1")));

        Reload(list, Achx(Chain("Walk", "<Loop>false</Loop><PingPong>true</PingPong><Reverse>true</Reverse>", "1"))).ShouldBeTrue();

        (list[0].Loop, list[0].PingPong, list[0].Reversed).ShouldBe((false, true, true));
    }

    [Fact]
    public void TryReloadFrom_TwoChainsShareAName_EachKeepsItsOwnFrames()
    {
        string text = Achx(Chain("Walk", "", "1"), Chain("Walk", "", "2", "3"));
        var list = Load(text);

        Reload(list, text).ShouldBeTrue();

        list.Select(c => c.Count).ShouldBe([1, 2]);
    }

    [Fact]
    public void TryReloadFrom_FewerFramesWhilePaused_CurrentFrameDoesNotThrow()
    {
        var list = Load(Achx(Chain("Walk", "", "1", "1", "1")));
        var player = new AnimationPlayer<Frame>(list);
        player.Play("Walk");
        player.Update(TimeSpan.FromSeconds(2.5));
        player.Pause();

        Reload(list, Achx(Chain("Walk", "", "1"))).ShouldBeTrue();

        player.CurrentFrame.ShouldBe(list[0][0]);
        player.CurrentFrameIndex.ShouldBe(0);
        Should.NotThrow(() => (player.CurrentOffset, player.CurrentColor));
    }

    [Fact]
    public void AnimationFinished_NonLoopingChainWithNoLength_IsRaised()
    {
        var list = new AnimationChainList<Frame> { new AnimationChain<Frame> { Name = "Hit", Loop = false } };
        list[0].Add(new Frame());
        var player = new AnimationPlayer<Frame>(list);
        int finished = 0;
        player.AnimationFinished += () => finished++;

        player.Play("Hit");
        player.Update(TimeSpan.FromSeconds(1));

        finished.ShouldBe(1);
    }

    [Fact]
    public void AnimationFinished_NonLoopingChainWithNoFrames_IsRaised()
    {
        var list = new AnimationChainList<Frame> { new AnimationChain<Frame> { Name = "Hit", Loop = false } };
        var player = new AnimationPlayer<Frame>(list);
        int finished = 0;
        player.AnimationFinished += () => finished++;

        player.Play("Hit");
        player.Update(TimeSpan.FromSeconds(1));

        finished.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryReloadFrom_InsideAFrameEventHandler_DoesNotThrow(bool pingPong)
    {
        var list = Load(Achx(Chain("Walk", "", "1", "1", "1")));
        foreach (var frame in list[0]) frame.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "step" });
        var player = new AnimationPlayer<Frame>(list);
        player.Play("Walk");
        player.IsPingPong = pingPong;
        int raised = 0;
        player.FrameEventRaised += _ =>
        {
            raised++;
            player.IsPingPong = false;
            Reload(list, Achx(Chain("Walk", "", "1"))).ShouldBeTrue();
        };

        Should.NotThrow(() => player.Update(TimeSpan.FromSeconds(4.5)));

        raised.ShouldBe(1);
    }
}
