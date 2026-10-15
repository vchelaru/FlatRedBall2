using System;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// #1478 phase 1: Sprite applies per-frame keyframe interpolation (offset + color) from the .achx.
// CPU-side only (AnimateSelf, ComputeDrawColor), so it runs without a GraphicsDevice.
public class SpriteKeyframeInterpolationTests
{
    private static Sprite Play(AnimationChainListSave save, string chain = "c")
    {
        var sprite = new Sprite { AnimationChains = save.ToAnimationChainList(null!) };
        sprite.PlayAnimation(chain);
        return sprite;
    }

    private static AnimationChainListSave Save(bool loop, params AnimationFrameSave[] frames)
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        chain.Frames.AddRange(frames);
        save.AnimationChains.Add(chain);
        return save;
    }

    private static AnimationFrameSave Slide(float x, float y = 0, float length = 1f, string? type = null,
        string? easing = null, bool skip = false)
        => new()
        {
            FrameLength = length, RelativeX = x, RelativeY = y,
            OffsetInterpolation = type, OffsetEasing = easing, SkipOffsetKeyframe = skip,
        };

    [Fact]
    public void AnimateSelf_LinearOffset_SpriteGlidesBetweenFrames()
    {
        var sprite = Play(Save(true, Slide(0, 10, type: "Linear"), Slide(100, 30)));

        sprite.AnimateSelf(0.5);

        sprite.X.ShouldBe(50f, 0.001f);
        sprite.Y.ShouldBe(20f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_DefaultSnap_OffsetStaysAtFrameValueMidFrame()
    {
        var sprite = Play(Save(true, Slide(10, 20), Slide(100, 30)));

        sprite.AnimateSelf(0.7);

        sprite.X.ShouldBe(10f);
        sprite.Y.ShouldBe(20f);
    }

    [Fact]
    public void AnimateSelf_EaseOutOffset_FollowsCurve()
    {
        var sprite = Play(Save(true, Slide(0, type: "Quadratic", easing: "Out"), Slide(100)));

        sprite.AnimateSelf(0.5);

        sprite.X.ShouldBe(75f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_SkippedFrame_GlideSpansThroughIt()
    {
        var sprite = Play(Save(true, Slide(0, type: "Linear"), Slide(999, skip: true), Slide(100)));

        sprite.AnimateSelf(1.5); // 1.5s into a 2s glide

        sprite.X.ShouldBe(75f, 0.001f);
        sprite.CurrentFrame.ShouldBeSameAs(sprite.CurrentAnimation![1]);
    }

    [Fact]
    public void AnimateSelf_NonLoopingChain_HoldsLastFrameOffset()
    {
        var sprite = Play(Save(false, Slide(0, type: "Linear"), Slide(100, type: "Linear")));

        sprite.AnimateSelf(5.0);

        sprite.X.ShouldBe(100f);
    }

    [Fact]
    public void AnimateSelf_LoopingChain_LastKeyframeGlidesBackToFirst()
    {
        var sprite = Play(Save(true, Slide(-10), Slide(10, type: "Linear")));

        sprite.AnimateSelf(1.5);

        sprite.X.ShouldBe(0f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_OffsetGroupNotFrameControlled_SpritePositionUntouched()
    {
        var sprite = new Sprite
        {
            AnimationChains = Save(true, Slide(0, type: "Linear"), Slide(100)).ToAnimationChainList(null!),
            FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Offset,
        };
        sprite.X = 7;
        sprite.PlayAnimation("c");

        sprite.AnimateSelf(0.5);

        sprite.X.ShouldBe(7f);
    }

    [Fact]
    public void FrameControlledProperties_OffsetReEnabledMidGlide_AppliesInterpolatedValue()
    {
        var sprite = new Sprite
        {
            AnimationChains = Save(true, Slide(0, type: "Linear"), Slide(100)).ToAnimationChainList(null!),
            FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Offset,
        };
        sprite.PlayAnimation("c");
        sprite.AnimateSelf(0.5);

        sprite.FrameControlledProperties = SpriteFrameProperties.All;

        sprite.X.ShouldBe(50f, 0.001f);
    }

    // ---- Color ----

    private static AnimationFrameSave Tint(int? alpha = null, int? red = null, string? type = null,
        bool skip = false, FlatRedBall2.Animation.ColorOperation? op = null, string? easing = null)
        => new()
        {
            FrameLength = 1f, Alpha = alpha, Red = red, ColorOperation = op,
            ColorInterpolation = type, ColorEasing = easing, SkipColorKeyframe = skip,
        };

    [Fact]
    public void AnimateSelf_LinearAlpha_DrawColorAlphaGlides()
    {
        var sprite = Play(Save(true, Tint(alpha: 0, type: "Linear"), Tint(alpha: 255)));

        sprite.AnimateSelf(0.5);

        sprite.ComputeDrawColor().A.ShouldBe((byte)128);
    }

    [Fact]
    public void AnimateSelf_LinearMultiplyRed_DrawColorGlides()
    {
        var sprite = Play(Save(true,
            Tint(red: 0, type: "Linear", op: ColorOperation.Multiply),
            Tint(red: 200, op: ColorOperation.Multiply)));

        sprite.AnimateSelf(0.5);

        sprite.ComputeDrawColor().R.ShouldBe((byte)100);
    }

    [Fact]
    public void AnimateSelf_LinearAddRed_AddColorGlides()
    {
        var sprite = Play(Save(true,
            Tint(red: 0, type: "Linear", op: ColorOperation.Add),
            Tint(red: 255, op: ColorOperation.Add)));

        sprite.AnimateSelf(0.5);

        sprite.AddColor.X.ShouldBe(128 / 255f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_DefaultSnapColor_DrawColorIsFrameValueMidFrame()
    {
        var sprite = Play(Save(true, Tint(alpha: 100), Tint(alpha: 255)));

        sprite.AnimateSelf(0.7);

        sprite.ComputeDrawColor().A.ShouldBe((byte)100);
    }

    [Fact]
    public void AnimateSelf_ColorOperationSnapsAtFrameChangeWhileChannelsGlide()
    {
        // Frame 0 is Multiply, frame 1 Add. Mid-glide on frame 0 the operation is still Multiply.
        var sprite = Play(Save(true,
            Tint(red: 0, type: "Linear", op: ColorOperation.Multiply),
            Tint(red: 200, op: ColorOperation.Add)));

        sprite.AnimateSelf(0.5);

        sprite.AddColor.X.ShouldBe(0f);
        sprite.ComputeDrawColor().R.ShouldBe((byte)100);
    }

    [Fact]
    public void AnimateSelf_ColorGroupNotFrameControlled_DrawColorIgnoresInterpolation()
    {
        var sprite = new Sprite
        {
            AnimationChains = Save(true, Tint(alpha: 0, type: "Linear"), Tint(alpha: 255)).ToAnimationChainList(null!),
            FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Color,
        };
        sprite.PlayAnimation("c");

        sprite.AnimateSelf(0.5);

        sprite.ComputeDrawColor().A.ShouldBe((byte)255);
    }

    [Fact]
    public void PlayAnimation_AfterGlidingChain_NewChainStartsFromItsOwnFirstFrame()
    {
        var save = Save(true, Tint(alpha: 0, type: "Linear"), Tint(alpha: 255));
        var other = new AnimationChainSave { Name = "other" };
        other.Frames.Add(Tint(alpha: 200));
        save.AnimationChains.Add(other);
        var sprite = Play(save);
        sprite.AnimateSelf(0.5);

        sprite.PlayAnimation("other");

        sprite.ComputeDrawColor().A.ShouldBe((byte)200);
    }

    // ---- Loading ----

    [Fact]
    public void ToAnimationChainList_CopiesKeyframeFieldsOntoRuntimeFrames()
    {
        var save = Save(true,
            new AnimationFrameSave
            {
                FrameLength = 1f,
                OffsetInterpolation = "Cubic", OffsetEasing = "In",
                ColorInterpolation = "Linear",
                SkipOffsetKeyframe = true, SkipColorKeyframe = true,
            });

        var frame = save.ToAnimationChainList(null!)[0][0];

        frame.SkipOffsetKeyframe.ShouldBeTrue();
        frame.SkipColorKeyframe.ShouldBeTrue();
        frame.OffsetCurve.Type.ShouldBe(FlatRedBall.Glue.StateInterpolation.InterpolationType.Cubic);
        frame.OffsetCurve.Easing.ShouldBe(FlatRedBall.Glue.StateInterpolation.Easing.In);
        frame.ColorCurve.Type.ShouldBe(FlatRedBall.Glue.StateInterpolation.InterpolationType.Linear);
    }

    [Fact]
    public void ToAnimationChainList_OldFrame_HasSnapCurves()
    {
        var save = Save(true, new AnimationFrameSave { FrameLength = 1f });

        var frame = save.ToAnimationChainList(null!)[0][0];

        frame.OffsetCurve.IsSnap.ShouldBeTrue();
        frame.ColorCurve.IsSnap.ShouldBeTrue();
        frame.SkipOffsetKeyframe.ShouldBeFalse();
        frame.SkipColorKeyframe.ShouldBeFalse();
    }

    [Fact]
    public void ToAnimationChainList_SkippedFrameChannels_AreNotInheritedByNextKeyframe()
    {
        var save = Save(true,
            Tint(red: 20, op: ColorOperation.Multiply),
            Tint(red: 255, skip: true),
            Tint());

        var chain = save.ToAnimationChainList(null!)[0];

        chain[2].Red.ShouldBe(20);
    }

    // ---- Back curves (Anticipate/Overshoot): the sprite follows the swing; draw color clamps ----

    [Fact]
    public void AnimateSelf_OvershootOffset_SpriteGoesPastEndValue()
    {
        var sprite = Play(Save(true, Slide(0, type: "Back", easing: "Out"), Slide(100)));

        sprite.AnimateSelf(0.5);

        sprite.X.ShouldBeGreaterThan(100f);
    }

    [Fact]
    public void AnimateSelf_AnticipateOffset_SpriteDipsBeforeStartValue()
    {
        var sprite = Play(Save(true, Slide(0, type: "Back", easing: "In"), Slide(100)));

        sprite.AnimateSelf(0.25);

        sprite.X.ShouldBeLessThan(0f);
    }

    [Fact]
    public void AnimateSelf_OvershootAlpha_DrawColorClampsWithoutWrapping()
    {
        var sprite = Play(Save(true, Tint(alpha: 0, type: "Back"), Tint(alpha: 255)));
        // Back with the default Out easing overshoots past 255 at the midpoint.
        sprite.AnimateSelf(0.5);

        sprite.ComputeDrawColor().A.ShouldBe((byte)255);
    }

    [Fact]
    public void AnimateSelf_AnticipateMultiplyRed_DrawColorClampsAtZeroWithoutWrapping()
    {
        var sprite = Play(Save(true,
            Tint(red: 0, type: "Back", easing: "In", op: ColorOperation.Multiply),
            Tint(red: 100, op: ColorOperation.Multiply)));

        sprite.AnimateSelf(0.25);

        sprite.ComputeDrawColor().R.ShouldBe((byte)0);
    }
}
