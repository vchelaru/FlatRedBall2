using System;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class PlatformerConfigTests
{
    [Fact]
    public void ApplyTo_AfterDoubleJumpSlot_DoesNotThrow()
    {
        var json = """
        {
          "movement": {
            "afterDoubleJump": { "MaxSpeedX": 120, "Gravity": 1500, "minJumpHeight": 32 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        Should.NotThrow(() => config.ApplyTo(behavior));
    }

    [Fact]
    public void ApplyTo_GroundAndAirSlots_PopulatesBehaviorSlots()
    {
        var json = """
        {
          "movement": {
            "ground": { "MaxSpeedX": 160, "Gravity": 1500, "minJumpHeight": 48 },
            "air":    { "MaxSpeedX": 100, "Gravity": 1500 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement.ShouldNotBeNull();
        behavior.GroundMovement!.MaxSpeedX.ShouldBe(160f);
        behavior.GroundMovement.Gravity.ShouldBe(1500f);
        behavior.AirMovement.MaxSpeedX.ShouldBe(100f);
        behavior.AirMovement.Gravity.ShouldBe(1500f);
    }

    [Fact]
    public void FromJsonString_AccelerationTimeXInSeconds_ConvertsToTimeSpan()
    {
        var json = """
        { "movement": { "ground": { "Gravity": 1500, "AccelerationTimeX": 0.25, "DecelerationTimeX": 0.5 } } }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.AccelerationTimeX.ShouldBe(TimeSpan.FromSeconds(0.25));
        behavior.GroundMovement.DecelerationTimeX.ShouldBe(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public void FromJsonString_BothDerivedAndRawJumpFields_ThrowsWithSlotName()
    {
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 1500, "minJumpHeight": 48, "JumpVelocity": 300 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        var ex = Should.Throw<InvalidOperationException>(() => config.ApplyTo(behavior));
        ex.Message.ShouldContain("ground");
    }

    [Fact]
    public void FromJsonString_DerivedJumpWithMinAndMax_EnablesButtonHold()
    {
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 1500, "minJumpHeight": 48, "maxJumpHeight": 96 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.JumpApplyByButtonHold.ShouldBeTrue();
        behavior.GroundMovement.JumpApplyLength.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void FromJsonString_DerivedJumpWithMinOnly_IsFixedHeight()
    {
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 1500, "minJumpHeight": 48 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.JumpApplyByButtonHold.ShouldBeFalse();
        behavior.GroundMovement.JumpVelocity.ShouldBe(MathF.Sqrt(2f * 1500f * 48f));
    }

    [Fact]
    public void FromJsonString_OmittedFields_FallBackToPlatformerValuesDefaults()
    {
        var json = """{ "movement": { "ground": { "MaxSpeedX": 160 } } }""";
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.SlopeSnapDistance.ShouldBe(8f);
        behavior.GroundMovement.SlopeSnapMaxAngleDegrees.ShouldBe(60f);
        behavior.GroundMovement.DownhillMaxSpeedMultiplier.ShouldBe(1.5f);
        behavior.GroundMovement.CanFallThroughOneWayCollision.ShouldBeTrue();
    }

    [Fact]
    public void FromJsonString_RawJumpFields_AreAssignedDirectly()
    {
        var json = """
        {
          "movement": {
            "ground": {
              "Gravity": 1500,
              "JumpVelocity": 420,
              "JumpApplyLength": 0.15,
              "JumpApplyByButtonHold": true
            }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.JumpVelocity.ShouldBe(420f);
        behavior.GroundMovement.JumpApplyLength.ShouldBe(TimeSpan.FromSeconds(0.15));
        behavior.GroundMovement.JumpApplyByButtonHold.ShouldBeTrue();
    }

    // ── Derived-mode jump trajectory gravity ──
    // The jump arc is governed by airborne gravity, not the grounded slot's gravity
    // (collision cancels ground gravity, so ground.Gravity never acts on the trajectory).
    // Derived-mode SetJumpHeights on the ground slot must use the paired air slot's
    // gravity so authored min/max heights match what the player actually reaches.

    [Fact]
    public void ApplyTo_DerivedGroundJump_UsesAirGravityForJumpVelocity()
    {
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 4400, "minJumpHeight": 24, "maxJumpHeight": 84 },
            "air":    { "Gravity": 800 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        // v = sqrt(2 * air.Gravity * minJumpHeight) = sqrt(2*800*24) ≈ 196
        behavior.GroundMovement!.JumpVelocity.ShouldBe(MathF.Sqrt(2f * 800f * 24f), tolerance: 0.01f);
    }

    [Fact]
    public void ApplyTo_DerivedGroundJump_UsesAirGravityForSustainLength()
    {
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 4400, "minJumpHeight": 24, "maxJumpHeight": 84 },
            "air":    { "Gravity": 800 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        // sustain = (max-min) / v where v uses air gravity
        float v = MathF.Sqrt(2f * 800f * 24f);
        double expected = (84f - 24f) / v;
        behavior.GroundMovement!.JumpApplyLength.TotalSeconds.ShouldBe(expected, tolerance: 0.001);
    }

    [Fact]
    public void ApplyTo_DerivedAirJump_UsesOwnGravity()
    {
        // An airborne-jump slot (e.g. future double-jump) derives heights from its own gravity —
        // the entity is already in this environment when the jump fires.
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 1500 },
            "air":    { "Gravity": 900, "minJumpHeight": 20, "maxJumpHeight": 60 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.AirMovement.JumpVelocity.ShouldBe(MathF.Sqrt(2f * 900f * 20f), tolerance: 0.01f);
    }

    [Fact]
    public void ApplyTo_DerivedGroundJump_NoAirSlot_FallsBackToOwnGravity()
    {
        // No air slot authored — preserve existing behavior so ground-only configs don't regress.
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 1500, "minJumpHeight": 48, "maxJumpHeight": 96 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.JumpVelocity.ShouldBe(MathF.Sqrt(2f * 1500f * 48f), tolerance: 0.01f);
    }

    [Fact]
    public void ApplyTo_RawGroundJump_JumpVelocityUnaffectedByAirGravity()
    {
        var json = """
        {
          "movement": {
            "ground": { "Gravity": 1500, "JumpVelocity": 420, "JumpApplyLength": 0.15 },
            "air":    { "Gravity": 800 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.JumpVelocity.ShouldBe(420f);
        behavior.GroundMovement.JumpApplyLength.ShouldBe(TimeSpan.FromSeconds(0.15));
    }

    [Fact]
    public void ApplyTo_ClimbingSlot_PopulatesClimbingMovement()
    {
        var json = """
        {
          "movement": {
            "ground":   { "MaxSpeedX": 160, "Gravity": 1500, "minJumpHeight": 48 },
            "air":      { "MaxSpeedX": 100, "Gravity": 1500 },
            "climbing": { "MaxSpeedX": 80, "ClimbingSpeed": 120 }
          }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.ClimbingMovement.ShouldNotBeNull();
        behavior.ClimbingMovement!.MaxSpeedX.ShouldBe(80f);
        behavior.ClimbingMovement.ClimbingSpeed.ShouldBe(120f);
    }

    [Fact]
    public void FromJsonString_UnknownTopLevelKeys_LoadsWithoutError()
    {
        // leftSuffix/rightSuffix/animations are not consumed by the loader but may appear in
        // user-authored files (leftover from earlier designs, or user extensions). Unknown keys must be tolerated.
        var json = """
        {
          "leftSuffix": "Left",
          "rightSuffix": "Right",
          "animations": { "states": { "Idle": "Idle" } },
          "movement": { "ground": { "MaxSpeedX": 160, "Gravity": 1500 } }
        }
        """;
        var config = PlatformerConfig.FromJsonString(json);
        var behavior = new PlatformerBehavior();

        config.ApplyTo(behavior);

        behavior.GroundMovement!.MaxSpeedX.ShouldBe(160f);
    }
}
