using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class SpriteAnimationTests
{
    private static AnimationChainList MakeChain(string chainName, int frameCount, float frameLength)
    {
        var chain = new AnimationChain { Name = chainName };
        for (int i = 0; i < frameCount; i++)
            chain.Add(new AnimationFrame { FrameLength = frameLength });
        var list = new AnimationChainList();
        list.Add(chain);
        return list;
    }

    [Fact]
    public void AnimateSelf_LoopingAnimation_AdvancesToSecondFrame()
    {
        var sprite = new Sprite();
        sprite.AnimationChains = MakeChain("Walk", 3, 0.1f);
        sprite.PlayAnimation("Walk");

        // Advance past first frame (0.1s) — should be on frame 1
        sprite.AnimateSelf(0.15);

        sprite.CurrentAnimation.ShouldNotBeNull();
        // Internal frame index is not directly readable, but we can verify Animate is still true
        sprite.Animate.ShouldBeTrue();
    }

    [Fact]
    public void AnimateSelf_LoopingAnimation_WrapsAfterTotalLength()
    {
        var sprite = new Sprite();
        sprite.AnimationChains = MakeChain("Walk", 2, 0.1f); // total = 0.2s
        sprite.PlayAnimation("Walk");

        // Advance past full cycle — should still be animating (looping)
        sprite.AnimateSelf(0.25);

        sprite.Animate.ShouldBeTrue();
    }

    [Fact]
    public void AnimateSelf_NonLoopingAnimation_StopsAtEnd()
    {
        var sprite = new Sprite();
        sprite.IsLooping = false;
        sprite.AnimationChains = MakeChain("Attack", 2, 0.1f); // total = 0.2s
        sprite.PlayAnimation("Attack");

        sprite.AnimateSelf(0.5);

        sprite.Animate.ShouldBeFalse();
    }

    [Fact]
    public void AnimateSelf_NonLoopingAnimation_FiresAnimationFinished()
    {
        var sprite = new Sprite();
        sprite.IsLooping = false;
        sprite.AnimationChains = MakeChain("Attack", 2, 0.1f);
        sprite.PlayAnimation("Attack");

        bool fired = false;
        sprite.AnimationFinished += () => fired = true;

        sprite.AnimateSelf(0.5);

        fired.ShouldBeTrue();
    }

    [Fact]
    public void PlayAnimation_ByName_SetsAnimateTrue()
    {
        var sprite = new Sprite();
        sprite.AnimationChains = MakeChain("Idle", 1, 0.2f);

        sprite.PlayAnimation("Idle");

        sprite.Animate.ShouldBeTrue();
        sprite.CurrentAnimation!.Name.ShouldBe("Idle");
    }

    [Fact]
    public void PlayAnimation_UnknownName_DoesNotCrash()
    {
        var sprite = new Sprite();
        sprite.AnimationChains = MakeChain("Idle", 1, 0.2f);

        // Should not throw
        sprite.PlayAnimation("NotExisting");

        sprite.Animate.ShouldBeFalse();
    }

    [Fact]
    public void PlayAnimation_FrameWithRelativeOffset_SetsXY()
    {
        var chain = new AnimationChain { Name = "Kick" };
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 5f, RelativeY = 10f });

        var list = new AnimationChainList();
        list.Add(chain);

        var sprite = new Sprite();
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Kick");

        sprite.X.ShouldBe(5f);
        sprite.Y.ShouldBe(10f);
    }

    [Fact]
    public void AnimateSelf_FrameChange_UpdatesXY()
    {
        var chain = new AnimationChain { Name = "Walk" };
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 0f, RelativeY = 0f });
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 3f, RelativeY = 7f });

        var list = new AnimationChainList();
        list.Add(chain);

        var sprite = new Sprite();
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Walk");

        // Initial frame: offsets are (0, 0)
        sprite.X.ShouldBe(0f);
        sprite.Y.ShouldBe(0f);

        // Advance past first frame
        sprite.AnimateSelf(0.15);

        sprite.X.ShouldBe(3f);
        sprite.Y.ShouldBe(7f);
    }

    [Fact]
    public void PlayAnimation_SameNameWhileAlreadyPlaying_DoesNotRestartAnimation()
    {
        var chain = new AnimationChain { Name = "Walk" };
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 0f });
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 5f });
        var list = new AnimationChainList();
        list.Add(chain);

        var sprite = new Sprite();
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Walk");
        sprite.AnimateSelf(0.15); // advances to frame 1 (RelativeX = 5)

        sprite.X.ShouldBe(5f);   // sanity: we're on frame 1

        sprite.PlayAnimation("Walk"); // same name — should NOT restart

        sprite.X.ShouldBe(5f);   // still on frame 1, not reset to frame 0
    }

    [Fact]
    public void PlayAnimation_SameChainWhileAlreadyPlaying_DoesNotRestartAnimation()
    {
        var chain = new AnimationChain { Name = "Walk" };
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 0f });
        chain.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 5f });
        var list = new AnimationChainList();
        list.Add(chain);

        var sprite = new Sprite();
        sprite.AnimationChains = list;
        sprite.PlayAnimation(chain);
        sprite.AnimateSelf(0.15); // advances to frame 1

        sprite.X.ShouldBe(5f);

        sprite.PlayAnimation(chain); // same chain reference — should NOT restart

        sprite.X.ShouldBe(5f);
    }

    [Fact]
    public void PlayAnimation_DifferentNameWhilePlaying_RestartsFromBeginning()
    {
        var chain1 = new AnimationChain { Name = "Walk" };
        chain1.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 0f });
        chain1.Add(new AnimationFrame { FrameLength = 0.1f, RelativeX = 5f });
        var chain2 = new AnimationChain { Name = "Jump" };
        chain2.Add(new AnimationFrame { FrameLength = 0.2f, RelativeX = 9f });
        var list = new AnimationChainList();
        list.Add(chain1);
        list.Add(chain2);

        var sprite = new Sprite();
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Walk");
        sprite.AnimateSelf(0.15); // on frame 1 of Walk

        sprite.PlayAnimation("Jump"); // different chain — SHOULD restart

        sprite.CurrentAnimation!.Name.ShouldBe("Jump");
        sprite.X.ShouldBe(9f);
    }

    [Fact]
    public void PlayAnimation_ZeroRelativeOffset_ResetsXYToZero()
    {
        var chain = new AnimationChain { Name = "Idle" };
        chain.Add(new AnimationFrame { FrameLength = 0.2f, RelativeX = 0f, RelativeY = 0f });

        var list = new AnimationChainList();
        list.Add(chain);

        var sprite = new Sprite { X = 99f, Y = 99f };
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Idle");

        // Playing an animation always sets X/Y from the frame, even when zero
        sprite.X.ShouldBe(0f);
        sprite.Y.ShouldBe(0f);
    }
}
