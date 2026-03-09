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
        chain.Add(new AnimationFrame { FrameLength = 0.1f });
        chain.Add(new AnimationFrame { FrameLength = 0.2f });
        chain.Add(new AnimationFrame { FrameLength = 0.3f });

        chain.TotalLength.ShouldBe(0.6f, tolerance: 0.0001f);
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
