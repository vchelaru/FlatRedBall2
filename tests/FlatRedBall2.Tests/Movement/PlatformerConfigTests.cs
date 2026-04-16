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

        behavior.GroundMovement!.SlopeSnapDistance.ShouldBe(16f);
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

    [Fact]
    public void FromJsonString_UnknownTopLevelKeys_LoadsWithoutError()
    {
        // leftSuffix/rightSuffix/animations belong to phase 2+ animation layer.
        // Phase 1 files and phase 2 files share a single loader, so unknown keys must be tolerated.
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
