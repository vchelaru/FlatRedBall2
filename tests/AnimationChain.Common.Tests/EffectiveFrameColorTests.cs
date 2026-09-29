using System.Collections.Generic;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Tests for <see cref="EffectiveFrameColor"/>: a null channel inherits the most recent earlier
/// frame that sets it.
/// </summary>
public class EffectiveFrameColorTests
{
    private static List<AnimationFrameSave> Chain(params AnimationFrameSave[] frames)
        => new(frames);

    [Fact]
    public void Resolve_ChannelSetOnEarlierFrame_CarriesForward()
    {
        // Frame 0 sets Alpha=250; frames 1 and 2 omit it → should stay 250 (sticky), not reset.
        var frames = Chain(
            new AnimationFrameSave { Alpha = 250 },
            new AnimationFrameSave(),
            new AnimationFrameSave());

        EffectiveFrameColor.Resolve(frames, 2).Alpha.ShouldBe(250);
    }

    [Fact]
    public void Resolve_ChannelNeverSet_ReturnsNull()
    {
        var frames = Chain(new AnimationFrameSave(), new AnimationFrameSave());

        EffectiveFrameColor.Resolve(frames, 1).Alpha.ShouldBeNull();
    }

    [Fact]
    public void Resolve_ChannelResetOnLaterFrame_ReturnsMostRecentValue()
    {
        // Alpha changes across frames; resolving at frame 2 returns the closest preceding set (100).
        var frames = Chain(
            new AnimationFrameSave { Alpha = 250 },
            new AnimationFrameSave { Alpha = 100 },
            new AnimationFrameSave());

        EffectiveFrameColor.Resolve(frames, 2).Alpha.ShouldBe(100);
    }

    [Fact]
    public void Resolve_ChannelsSetOnDifferentFrames_ResolveIndependently()
    {
        // Red set on frame 0, Blue set on frame 1; frame 2 inherits both.
        var frames = Chain(
            new AnimationFrameSave { Red = 200 },
            new AnimationFrameSave { Blue = 50 },
            new AnimationFrameSave());

        var resolved = EffectiveFrameColor.Resolve(frames, 2);
        resolved.Red.ShouldBe(200);
        resolved.Blue.ShouldBe(50);
        resolved.Green.ShouldBeNull();
    }

    [Fact]
    public void Resolve_OperationSetOnEarlierFrame_CarriesForward()
    {
        var frames = Chain(
            new AnimationFrameSave { ColorOperation = ColorOperation.Add },
            new AnimationFrameSave());

        EffectiveFrameColor.Resolve(frames, 1).Operation.ShouldBe(ColorOperation.Add);
    }

    // ── ResolveAll (O(n) whole-strip resolution) ──────────────────────────────

    [Fact]
    public void ResolveAll_MatchesPerFrameResolve_ForEveryIndex()
    {
        // ResolveAll's forward pass must agree with the backward Resolve at every index, including
        // channels set on different frames and channels re-set mid-chain.
        var frames = Chain(
            new AnimationFrameSave { Red = 200, ColorOperation = ColorOperation.Multiply },
            new AnimationFrameSave { Blue = 50 },
            new AnimationFrameSave { Red = 10 },
            new AnimationFrameSave());

        var all = EffectiveFrameColor.ResolveAll(frames);

        all.Length.ShouldBe(frames.Count);
        for (int i = 0; i < frames.Count; i++)
            all[i].ShouldBe(EffectiveFrameColor.Resolve(frames, i));
    }

    [Fact]
    public void ResolveAll_LaterFrameReSetsChannel_OverridesRunningValue()
    {
        // Guards against a `??=` regression that would freeze the first set value: frame 2 sets Red=10
        // and every later frame must inherit 10, not the earlier 200.
        var frames = Chain(
            new AnimationFrameSave { Red = 200 },
            new AnimationFrameSave(),
            new AnimationFrameSave { Red = 10 },
            new AnimationFrameSave());

        var all = EffectiveFrameColor.ResolveAll(frames);

        all[0].Red.ShouldBe(200);
        all[1].Red.ShouldBe(200);
        all[2].Red.ShouldBe(10);
        all[3].Red.ShouldBe(10);
    }

    [Fact]
    public void ResolveAll_EditingEarlierFrame_ChangesDownstreamButNotUpstream()
    {
        // The sticky-invalidation contract: changing frame 1's color changes the effective color of
        // frames 1 onward (until a frame re-sets that channel), and leaves frame 0 untouched.
        var frames = Chain(
            new AnimationFrameSave { Alpha = 255 },
            new AnimationFrameSave { Alpha = 128 },
            new AnimationFrameSave(),
            new AnimationFrameSave { Alpha = 64 });

        var before = EffectiveFrameColor.ResolveAll(frames);
        frames[1].Alpha = 100;
        var after = EffectiveFrameColor.ResolveAll(frames);

        after[0].ShouldBe(before[0]);      // upstream unchanged
        after[1].ShouldNotBe(before[1]);   // edited frame
        after[2].ShouldNotBe(before[2]);   // downstream inherits the edit
        after[3].ShouldBe(before[3]);      // frame 3 re-sets alpha, so it's shielded
    }

    [Fact]
    public void ResolveAll_EmptyList_ReturnsEmptyArray()
        => EffectiveFrameColor.ResolveAll(new System.Collections.Generic.List<AnimationFrameSave>()).ShouldBeEmpty();

    [Fact]
    public void ChannelDefault_Add_ReturnsZero()
        => EffectiveFrameColor.ChannelDefault(ColorOperation.Add).ShouldBe(0);

    [Fact]
    public void ChannelDefault_MultiplyOrNone_Returns255()
    {
        EffectiveFrameColor.ChannelDefault(ColorOperation.Multiply).ShouldBe(255);
        EffectiveFrameColor.ChannelDefault(null).ShouldBe(255);
    }
}
