using System.Collections.Generic;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="EffectiveFrameColor"/> — the sticky "unset = keep the last value"
/// resolution the editor uses so its preview and inspector match runtime behavior.
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

        Assert.Equal(250, EffectiveFrameColor.Resolve(frames, 2).Alpha);
    }

    [Fact]
    public void Resolve_ChannelNeverSet_ReturnsNull()
    {
        var frames = Chain(new AnimationFrameSave(), new AnimationFrameSave());

        Assert.Null(EffectiveFrameColor.Resolve(frames, 1).Alpha);
    }

    [Fact]
    public void Resolve_ChannelResetOnLaterFrame_ReturnsMostRecentValue()
    {
        // Alpha changes across frames; resolving at frame 2 returns the closest preceding set (100).
        var frames = Chain(
            new AnimationFrameSave { Alpha = 250 },
            new AnimationFrameSave { Alpha = 100 },
            new AnimationFrameSave());

        Assert.Equal(100, EffectiveFrameColor.Resolve(frames, 2).Alpha);
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
        Assert.Equal(200, resolved.Red);
        Assert.Equal(50, resolved.Blue);
        Assert.Null(resolved.Green);
    }

    [Fact]
    public void Resolve_OperationSetOnEarlierFrame_CarriesForward()
    {
        var frames = Chain(
            new AnimationFrameSave { ColorOperation = ColorOperation.Add },
            new AnimationFrameSave());

        Assert.Equal(ColorOperation.Add, EffectiveFrameColor.Resolve(frames, 1).Operation);
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

        Assert.Equal(frames.Count, all.Length);
        for (int i = 0; i < frames.Count; i++)
            Assert.Equal(EffectiveFrameColor.Resolve(frames, i), all[i]);
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

        Assert.Equal(200, all[0].Red);
        Assert.Equal(200, all[1].Red);
        Assert.Equal(10, all[2].Red);
        Assert.Equal(10, all[3].Red);
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

        Assert.Equal(before[0], after[0]);      // upstream unchanged
        Assert.NotEqual(before[1], after[1]);   // edited frame
        Assert.NotEqual(before[2], after[2]);   // downstream inherits the edit
        Assert.Equal(before[3], after[3]);      // frame 3 re-sets alpha, so it's shielded
    }

    [Fact]
    public void ResolveAll_EmptyList_ReturnsEmptyArray()
        => Assert.Empty(EffectiveFrameColor.ResolveAll(new System.Collections.Generic.List<AnimationFrameSave>()));

    [Fact]
    public void ChannelDefault_Add_ReturnsZero()
        => Assert.Equal(0, EffectiveFrameColor.ChannelDefault(ColorOperation.Add));

    [Fact]
    public void ChannelDefault_MultiplyOrNone_Returns255()
    {
        Assert.Equal(255, EffectiveFrameColor.ChannelDefault(ColorOperation.Multiply));
        Assert.Equal(255, EffectiveFrameColor.ChannelDefault(null));
    }
}
