using System;
using FlatRedBall2.Animation;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class AnimationChainTests
{
    [Fact]
    public void TotalLength_MultipleFrames_ReturnsSumOfFrameLengths()
    {
        var chain = new AnimationChain();
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1) });
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.2) });
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.3) });

        chain.TotalLength.TotalSeconds.ShouldBe(0.6, tolerance: 0.0001);
    }

    [Fact]
    public void AnimationChainList_StringIndexer_ReturnsChainByName()
    {
        var list = new AnimationChainList();
        list.Add(new AnimationChain { Name = "Walk" });
        list.Add(new AnimationChain { Name = "Run" });

        list["Walk"].ShouldNotBeNull();
        list["Walk"]!.Name.ShouldBe("Walk");
        list["Missing"].ShouldBeNull();
    }
}
