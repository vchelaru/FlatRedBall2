using System;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// Issue #1412. Sprite.FrameControlledProperties picks which property groups animation frames write
// onto the sprite. All of this is CPU-side (ApplyCurrentFrame, ComputeDrawColor, Batch), so it runs
// on CI without a GraphicsDevice.
public class SpriteFrameControlTests
{
    // Two 1-second frames: frame 0 is flipped, offset, tinted and half-alpha; frame 1 is plain.
    private static Sprite MakePlayingSprite()
    {
        var chain = new AnimationChain { Name = "Chain" };
        chain.Add(new AnimationFrame
        {
            FrameLength = TimeSpan.FromSeconds(1),
            FlipHorizontal = true,
            FlipVertical = true,
            FlipDiagonal = true,
            RelativeX = 10,
            RelativeY = 20,
            ColorOperation = ColorOperation.Multiply,
            Red = 128,
            Green = 255,
            Blue = 255,
            Alpha = 128,
        });
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(1) });
        return new Sprite { AnimationChains = new AnimationChainList { chain } };
    }

    [Fact]
    public void FrameControlledProperties_Default_IsAll()
    {
        new Sprite().FrameControlledProperties.ShouldBe(SpriteFrameProperties.All);
    }

    [Fact]
    public void PlayAnimation_DefaultFlags_FrameWritesFlipAndOffset()
    {
        var sprite = MakePlayingSprite();

        sprite.PlayAnimation("Chain");

        sprite.FlipHorizontal.ShouldBeTrue();
        sprite.FlipVertical.ShouldBeTrue();
        sprite.FlipDiagonal.ShouldBeTrue();
        sprite.X.ShouldBe(10f);
        sprite.Y.ShouldBe(20f);
    }

    [Fact]
    public void AnimateSelf_FlipGroupOff_ManualFlipSurvivesFrameChange()
    {
        var sprite = MakePlayingSprite();
        sprite.FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Flip;
        sprite.PlayAnimation("Chain"); // frame 0 has every flip set; none may land
        sprite.FlipHorizontal.ShouldBeFalse();
        sprite.FlipHorizontal = true;

        sprite.AnimateSelf(1.5); // advances to frame 1, whose flips are all false

        sprite.FlipHorizontal.ShouldBeTrue();
        sprite.FlipVertical.ShouldBeFalse();
        sprite.FlipDiagonal.ShouldBeFalse();
    }

    [Fact]
    public void AnimateSelf_FlipGroupOn_FrameOverwritesManualFlip()
    {
        var sprite = MakePlayingSprite();
        sprite.PlayAnimation("Chain");

        sprite.AnimateSelf(1.5);

        sprite.FlipHorizontal.ShouldBeFalse();
    }

    [Fact]
    public void AnimateSelf_OffsetGroupOff_ManualPositionSurvivesFrameChange()
    {
        var sprite = MakePlayingSprite();
        sprite.FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Offset;
        sprite.X = 3;
        sprite.Y = 4;
        sprite.PlayAnimation("Chain");

        sprite.AnimateSelf(1.5);

        sprite.X.ShouldBe(3f);
        sprite.Y.ShouldBe(4f);
    }

    [Fact]
    public void PlayAnimation_AllGroupsOff_SourceRectangleStillFollowsFrames()
    {
        var chain = new AnimationChain { Name = "Chain" };
        chain.Add(new AnimationFrame { SourceRectangle = new Rectangle(0, 0, 8, 6), FlipHorizontal = true });
        var sprite = new Sprite
        {
            AnimationChains = new AnimationChainList { chain },
            FrameControlledProperties = SpriteFrameProperties.None,
        };

        sprite.PlayAnimation("Chain");

        sprite.SourceRectangle.ShouldBe(new Rectangle(0, 0, 8, 6));
        sprite.FlipHorizontal.ShouldBeFalse();
    }

    [Fact]
    public void ComputeDrawColor_ColorGroupOn_AppliesFrameMultiplyAndAlpha()
    {
        var sprite = MakePlayingSprite();
        sprite.PlayAnimation("Chain");

        var color = sprite.ComputeDrawColor();

        color.R.ShouldBe((byte)128); // White * Red 128
        color.A.ShouldBe((byte)128); // opaque * Alpha 128
    }

    [Fact]
    public void ComputeDrawColor_ColorGroupOff_IgnoresFrameColorAndAlpha()
    {
        var sprite = MakePlayingSprite();
        sprite.FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Color;
        sprite.Color = new Color(200, 100, 50);
        sprite.Alpha = 0.5f;
        sprite.PlayAnimation("Chain");

        var color = sprite.ComputeDrawColor();

        color.ShouldBe(new Color(200, 100, 50) * 0.5f);
    }

    [Fact]
    public void FrameControlledProperties_EnablingGroupMidAnimation_AppliesCurrentFrameImmediately()
    {
        var sprite = MakePlayingSprite();
        sprite.FrameControlledProperties = SpriteFrameProperties.None;
        sprite.PlayAnimation("Chain");
        sprite.X.ShouldBe(0f);

        sprite.FrameControlledProperties = SpriteFrameProperties.Offset;

        sprite.X.ShouldBe(10f);
        sprite.Y.ShouldBe(20f);
        sprite.FlipHorizontal.ShouldBeFalse(); // Flip stayed off
    }

    [Fact]
    public void Batch_AddFrameWithColorGroupOff_ReturnsAssignedBatch()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { ColorOperation = ColorOperation.Add, Red = 200 });
        var sprite = new Sprite
        {
            AnimationChains = new AnimationChainList { chain },
            FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Color,
        };

        sprite.PlayAnimation("Flash");

        sprite.Batch.ShouldBeSameAs(WorldSpaceBatch.Instance);
    }
}
