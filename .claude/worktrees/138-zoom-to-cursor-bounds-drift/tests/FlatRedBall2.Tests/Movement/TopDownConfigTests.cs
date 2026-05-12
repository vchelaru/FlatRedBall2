using System;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class TopDownConfigTests
{
    [Fact]
    public void ApplyTo_PopulatesAllFields()
    {
        var json = """
        {
          "movement": {
            "maxSpeed": 200,
            "accelerationTime": 0.4,
            "decelerationTime": 0.2,
            "updateDirectionFromInput": false,
            "updateDirectionFromVelocity": true,
            "isUsingCustomDeceleration": true,
            "customDecelerationValue": 350
          }
        }
        """;
        var config = TopDownConfig.FromJsonString(json);
        var behavior = new TopDownBehavior();

        config.ApplyTo(behavior);

        var v = behavior.MovementValues!;
        v.MaxSpeed.ShouldBe(200f);
        v.AccelerationTime.ShouldBe(TimeSpan.FromSeconds(0.4));
        v.DecelerationTime.ShouldBe(TimeSpan.FromSeconds(0.2));
        v.UpdateDirectionFromInput.ShouldBeFalse();
        v.UpdateDirectionFromVelocity.ShouldBeTrue();
        v.IsUsingCustomDeceleration.ShouldBeTrue();
        v.CustomDecelerationValue.ShouldBe(350f);
    }

    [Fact]
    public void ApplyTo_CalledTwice_MutatesExistingTopDownValuesInstance()
    {
        // Per-frame context swapping relies on ApplyTo reusing the existing TopDownValues
        // instance, so it allocates nothing on the hot path.
        var firstJson = """{ "movement": { "maxSpeed": 200, "accelerationTime": 0.4 } }""";
        var secondJson = """{ "movement": { "maxSpeed": 50,  "accelerationTime": 0.1 } }""";
        var behavior = new TopDownBehavior();
        TopDownConfig.FromJsonString(firstJson).ApplyTo(behavior);
        var original = behavior.MovementValues;

        TopDownConfig.FromJsonString(secondJson).ApplyTo(behavior);

        behavior.MovementValues.ShouldBeSameAs(original);
        behavior.MovementValues!.MaxSpeed.ShouldBe(50f);
    }

    [Fact]
    public void ApplyTo_OmittedFields_ResetsToDefaults()
    {
        // Replace semantic: fields absent from the JSON revert to TopDownValues defaults, not
        // retained from the prior state.
        var behavior = new TopDownBehavior
        {
            MovementValues = new TopDownValues
            {
                MaxSpeed = 100,
                AccelerationTime = TimeSpan.FromSeconds(0.5),
                DecelerationTime = TimeSpan.FromSeconds(0.5),
            },
        };
        var json = """{ "movement": { "maxSpeed": 250 } }""";
        var config = TopDownConfig.FromJsonString(json);

        config.ApplyTo(behavior);

        behavior.MovementValues!.MaxSpeed.ShouldBe(250f);
        behavior.MovementValues.AccelerationTime.ShouldBe(TimeSpan.Zero);
        behavior.MovementValues.DecelerationTime.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ApplyTo_NullMovementValues_CreatesNewInstance()
    {
        var behavior = new TopDownBehavior();
        var json = """{ "movement": { "maxSpeed": 175 } }""";
        var config = TopDownConfig.FromJsonString(json);

        config.ApplyTo(behavior);

        behavior.MovementValues.ShouldNotBeNull();
        behavior.MovementValues!.MaxSpeed.ShouldBe(175f);
    }

    [Fact]
    public void FromJsonString_AllowsCommentsAndTrailingCommas()
    {
        var json = """
        {
          // top comment
          "movement": {
            "maxSpeed": 120, // trailing-line
          }
        }
        """;
        var config = TopDownConfig.FromJsonString(json);
        var behavior = new TopDownBehavior();

        Should.NotThrow(() => config.ApplyTo(behavior));
        behavior.MovementValues!.MaxSpeed.ShouldBe(120f);
    }

    [Fact]
    public void FromJsonString_PropertyNamesAreCaseInsensitive()
    {
        var json = """{ "MOVEMENT": { "maxspeed": 99 } }""";
        var config = TopDownConfig.FromJsonString(json);
        var behavior = new TopDownBehavior();

        config.ApplyTo(behavior);

        behavior.MovementValues!.MaxSpeed.ShouldBe(99f);
    }

    [Fact]
    public void ApplyTo_NullMovement_DoesNothing()
    {
        var behavior = new TopDownBehavior();
        var config = TopDownConfig.FromJsonString("{}");

        config.ApplyTo(behavior);

        behavior.MovementValues.ShouldBeNull();
    }
}
