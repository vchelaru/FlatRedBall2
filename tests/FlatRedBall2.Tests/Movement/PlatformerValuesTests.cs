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
}
