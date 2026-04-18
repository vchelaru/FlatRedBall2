using System;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class PlatformerValuesTests
{
    [Fact]
    public void SetJumpHeights_MinOnly_SetsFixedJump()
    {
        float gravity = 600f;
        float minHeight = 48f;
        float expectedVelocity = MathF.Sqrt(2f * gravity * minHeight);

        var values = new PlatformerValues { Gravity = gravity };
        values.SetJumpHeights(minHeight);

        values.JumpVelocity.ShouldBe(expectedVelocity);
        values.JumpApplyLength.ShouldBe(TimeSpan.Zero);
        values.JumpApplyByButtonHold.ShouldBeFalse();
    }

    [Fact]
    public void SetJumpHeights_MaxEqualToMin_SetsFixedJump()
    {
        float gravity = 600f;
        float height = 48f;

        var values = new PlatformerValues { Gravity = gravity };
        values.SetJumpHeights(height, height);

        values.JumpApplyLength.ShouldBe(TimeSpan.Zero);
        values.JumpApplyByButtonHold.ShouldBeFalse();
    }

    [Fact]
    public void SetJumpHeights_MaxGreaterThanMin_SetsSustainForVariableJump()
    {
        float gravity = 600f;
        float minHeight = 32f;
        float maxHeight = 96f;
        float expectedVelocity = MathF.Sqrt(2f * gravity * minHeight);
        float expectedSustain = (maxHeight - minHeight) / expectedVelocity;

        var values = new PlatformerValues { Gravity = gravity };
        values.SetJumpHeights(minHeight, maxHeight);

        values.JumpVelocity.ShouldBe(expectedVelocity);
        values.JumpApplyLength.TotalSeconds.ShouldBe(expectedSustain, tolerance: 0.0001);
        values.JumpApplyByButtonHold.ShouldBeTrue();
    }

    [Fact]
    public void SetJumpHeights_GravityNotSet_Throws()
    {
        var values = new PlatformerValues { Gravity = 0f };

        Should.Throw<InvalidOperationException>(() => values.SetJumpHeights(48f));
    }

    [Fact]
    public void SetJumpHeights_MaxLessThanMin_Throws()
    {
        var values = new PlatformerValues { Gravity = 600f };

        Should.Throw<ArgumentOutOfRangeException>(() => values.SetJumpHeights(48f, 32f));
    }

    [Fact]
    public void SetJumpHeights_MinNotPositive_Throws()
    {
        var values = new PlatformerValues { Gravity = 600f };

        Should.Throw<ArgumentOutOfRangeException>(() => values.SetJumpHeights(0f));
    }

    [Fact]
    public void SetJumpHeights_ExplicitGravity_UsesGivenGravityNotOwnGravity()
    {
        // When the trajectory gravity differs from this slot's Gravity (ground slot deriving
        // heights against airborne gravity), the overload must use the argument, not this.Gravity.
        var values = new PlatformerValues { Gravity = 4400f };

        values.SetJumpHeights(minHeight: 24f, maxHeight: 84f, jumpGravity: 800f);

        values.JumpVelocity.ShouldBe(MathF.Sqrt(2f * 800f * 24f), tolerance: 0.01f);
        float v = MathF.Sqrt(2f * 800f * 24f);
        values.JumpApplyLength.TotalSeconds.ShouldBe((84f - 24f) / v, tolerance: 0.001);
        values.JumpApplyByButtonHold.ShouldBeTrue();
    }

    [Fact]
    public void DefaultSlopeSnapDistance_IsHalfTile()
    {
        // 8f = half a 16px tile. Aggressive enough to hug downslopes that briefly go airborne
        // across tile seams, small enough not to teleport onto one-tile-lower steps. The
        // historical default was 16f, which matched single-tile-step height exactly and made
        // staircase geometry feel teleporty.
        new PlatformerValues().SlopeSnapDistance.ShouldBe(8f);
    }

    [Fact]
    public void SetJumpHeights_ExplicitGravityNotPositive_Throws()
    {
        var values = new PlatformerValues { Gravity = 1500f };

        Should.Throw<ArgumentOutOfRangeException>(
            () => values.SetJumpHeights(minHeight: 24f, maxHeight: 84f, jumpGravity: 0f));
    }
}
