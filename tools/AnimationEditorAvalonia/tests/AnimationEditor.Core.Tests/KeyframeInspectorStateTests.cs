using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class KeyframeInspectorStateTests
{
    private static AnimationChainSave Chain(int frames)
    {
        var chain = new AnimationChainSave { Name = "c" };
        for (int i = 0; i < frames; i++)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        return chain;
    }

    private static KeyframeGroupState State(KeyframeGroup group, AnimationChainSave chain, params int[] indices) =>
        KeyframeInspectorState.For(group, indices.Select(i => chain.Frames[i]).ToList(), _ => chain);

    [Fact]
    public void FirstFrame_IsAlwaysAKeyframe_AndCannotBeToggled_EvenWithTheFlagSet()
    {
        var chain = Chain(2);
        chain.Frames[0].SkipOffsetKeyframe = true;

        var state = State(KeyframeGroup.Offset, chain, 0);

        Assert.True(state.IsKeyframe);
        Assert.False(state.CanToggle);
    }

    [Fact]
    public void LaterFrame_FollowsTheSkipFlag_PerGroup()
    {
        var chain = Chain(3);
        chain.Frames[1].SkipOffsetKeyframe = true;

        Assert.False(State(KeyframeGroup.Offset, chain, 1).IsKeyframe);
        Assert.True(State(KeyframeGroup.Color, chain, 1).IsKeyframe);
        Assert.True(State(KeyframeGroup.Offset, chain, 1).CanToggle);
    }

    [Fact]
    public void KeyframeOff_HidesTheCurves_AndMakesFieldsReadOnly()
    {
        var chain = Chain(2);
        chain.Frames[1].SkipColorKeyframe = true;

        var state = State(KeyframeGroup.Color, chain, 1);

        Assert.False(state.CurvesVisible);
        Assert.False(state.FieldsEditable);
    }

    [Fact]
    public void MixedKeyframeState_IsNull_AndKeepsCurvesAndFieldsAvailable()
    {
        var chain = Chain(3);
        chain.Frames[2].SkipOffsetKeyframe = true;

        var state = State(KeyframeGroup.Offset, chain, 1, 2);

        Assert.Null(state.IsKeyframe);
        Assert.True(state.CurvesVisible);
        Assert.True(state.FieldsEditable);
    }

    [Fact]
    public void Curve_SingleFrame_IsTheMatchingChoice()
    {
        var chain = Chain(2);
        chain.Frames[1].OffsetInterpolation = "Quadratic";
        chain.Frames[1].OffsetEasing = "InOut";

        var state = State(KeyframeGroup.Offset, chain, 1);

        Assert.Equal(KeyframeCurveChoice.EaseInOut, state.Curve);
        Assert.Null(state.CurveNote);
    }

    [Fact]
    public void Curve_OutsideTheEight_ShowsNothingAndNamesTheStoredCurve()
    {
        var chain = Chain(2);
        chain.Frames[1].ColorInterpolation = "Cubic";
        chain.Frames[1].ColorEasing = "In";

        var state = State(KeyframeGroup.Color, chain, 1);

        Assert.Null(state.Curve);
        Assert.Equal("Cubic In", state.CurveNote);
    }

    [Fact]
    public void Curve_DisagreeingFrames_ShowsNothingAndSaysMixed()
    {
        var chain = Chain(3);
        chain.Frames[1].OffsetInterpolation = "Linear";

        var state = State(KeyframeGroup.Offset, chain, 1, 2);

        Assert.Null(state.Curve);
        Assert.Equal("Mixed", state.CurveNote);
    }

    [Fact]
    public void Curve_IgnoresSkippedFrames()
    {
        var chain = Chain(3);
        chain.Frames[1].OffsetInterpolation = "Linear";
        chain.Frames[2].OffsetInterpolation = "Bounce";
        chain.Frames[2].SkipOffsetKeyframe = true;

        var state = State(KeyframeGroup.Offset, chain, 1, 2);

        Assert.Equal(KeyframeCurveChoice.Linear, state.Curve);
    }

    [Fact]
    public void Curve_GroupsAreIndependent()
    {
        var chain = Chain(2);
        chain.Frames[1].OffsetInterpolation = "Linear";

        Assert.Equal(KeyframeCurveChoice.Snap, State(KeyframeGroup.Color, chain, 1).Curve);
    }

    [Fact]
    public void IsEffectivelyKeyframe_FirstFrameAlwaysTrue()
    {
        var chain = Chain(2);
        chain.Frames[0].SkipColorKeyframe = true;
        chain.Frames[1].SkipColorKeyframe = true;

        Assert.True(KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Color, chain.Frames[0], chain));
        Assert.False(KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Color, chain.Frames[1], chain));
    }

    [Fact]
    public void DisplayOffset_KeyframeShowsItsOwnValue_NonKeyframeShowsTheValueInEffect()
    {
        var chain = Chain(3);
        chain.Frames[0].RelativeX = 0;
        chain.Frames[0].OffsetInterpolation = "Linear";
        chain.Frames[1].RelativeX = 999;
        chain.Frames[2].RelativeX = 90;

        Assert.Equal(999f, KeyframeInspectorState.DisplayOffset(chain.Frames[1], chain).X);

        chain.Frames[1].SkipOffsetKeyframe = true;
        Assert.Equal(45f, KeyframeInspectorState.DisplayOffset(chain.Frames[1], chain).X, 3);
    }

    [Fact]
    public void DisplayColor_KeyframeKeepsBlankChannels_NonKeyframeShowsTheGlide()
    {
        var chain = Chain(3);
        chain.Frames[0].Red = 0;
        chain.Frames[0].Alpha = 0;
        chain.Frames[0].ColorInterpolation = "Linear";
        chain.Frames[2].Red = 200;
        chain.Frames[2].Alpha = 100;

        var own = KeyframeInspectorState.DisplayColor(chain.Frames[1], chain);
        Assert.Null(own.Red);

        chain.Frames[1].SkipColorKeyframe = true;
        var effective = KeyframeInspectorState.DisplayColor(chain.Frames[1], chain);
        Assert.Equal(100, effective.Red);
        Assert.Equal(50, effective.Alpha);
        Assert.Null(effective.Green);
    }
}
