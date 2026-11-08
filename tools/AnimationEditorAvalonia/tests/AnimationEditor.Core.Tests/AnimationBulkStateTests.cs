using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AnimationBulkStateTests
{
    private static AnimationChainSave Chain(bool locked = false, bool loop = true, bool pingPong = false, bool reverse = false) =>
        new() { Name = "c", IsLocked = locked, Loop = loop, PingPong = pingPong, Reverse = reverse };

    [Fact]
    public void For_OneAnimation_IsItsOwnValues_AndNotMultiple()
    {
        var state = AnimationBulkState.For(new[] { Chain(locked: true, loop: false, pingPong: true) });

        Assert.Equal(new AnimationBulkState(true, false, true, false, false), state);
    }

    [Fact]
    public void For_AnimationsThatAgree_ShowThatValue_AndAreMultiple()
    {
        var state = AnimationBulkState.For(new[] { Chain(loop: true), Chain(loop: true) });

        Assert.Equal(new AnimationBulkState(false, true, false, false, true), state);
    }

    [Fact]
    public void For_AnimationsThatDisagree_ShowNullOnlyForTheDifferingFlags()
    {
        var state = AnimationBulkState.For(new[] { Chain(loop: true, reverse: true), Chain(loop: false, reverse: true) });

        Assert.Null(state.Loop);
        Assert.True(state.Reverse);
        Assert.False(state.Locked);
    }

    [Fact]
    public void For_NoAnimations_IsAllNull_AndNotMultiple()
    {
        Assert.Equal(new AnimationBulkState(null, null, null, null, false), AnimationBulkState.For(System.Array.Empty<AnimationChainSave>()));
    }

    [Fact]
    public void Common_ReturnsTheSharedValue_OrNullWhenTheyDifferOrThereAreNone()
    {
        Assert.Equal(3, MixedState.Common(new[] { 3, 3 }));
        Assert.Null(MixedState.Common(new[] { 3, 4 }));
        Assert.Null(MixedState.Common(System.Array.Empty<int>()));
        Assert.Equal("a", MixedState.Common(new[] { "a", "a" }));
        Assert.Null(MixedState.Common(new[] { "a", "b" }));
    }
}
