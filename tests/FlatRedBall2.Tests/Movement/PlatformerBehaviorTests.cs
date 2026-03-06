using System;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class PlatformerBehaviorTests
{
    private static FrameTime MakeFrame(float deltaSeconds, float totalSeconds = 0f)
        => new FrameTime(TimeSpan.FromSeconds(deltaSeconds), TimeSpan.Zero, TimeSpan.FromSeconds(totalSeconds));

    [Fact]
    public void HorizontalInput_WithNoAcceleration_SetsVelocityDirectly()
    {
        float maxSpeedX = 200f;
        var values = new PlatformerValues { MaxSpeedX = maxSpeedX, UsesAcceleration = false };
        var behavior = new PlatformerBehavior { AirMovement = values, MovementInput = new MockAxisInput(x: 1f) };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(maxSpeedX);
    }

    [Fact]
    public void IsOnGround_WhenLastRepositionYPositive_ReturnsTrue()
    {
        var behavior = new PlatformerBehavior { AirMovement = new PlatformerValues() };
        var entity = new Entity();
        entity.LastReposition = new System.Numerics.Vector2(0f, 5f);

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsOnGround.ShouldBeTrue();
    }

    [Fact]
    public void IsOnGround_WhenLastRepositionYZero_ReturnsFalse()
    {
        var behavior = new PlatformerBehavior { AirMovement = new PlatformerValues() };
        var entity = new Entity();
        entity.LastReposition = new System.Numerics.Vector2(0f, 0f);

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsOnGround.ShouldBeFalse();
    }

    [Fact]
    public void Jump_WhenNotOnGround_DoesNotJump()
    {
        float jumpVelocity = 400f;
        var values = new PlatformerValues { JumpVelocity = jumpVelocity };
        var jumpInput = new MockPressableInput(wasJustPressed: true);
        var behavior = new PlatformerBehavior { AirMovement = values, JumpInput = jumpInput };
        var entity = new Entity();
        // LastReposition.Y = 0 → not on ground

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityY.ShouldBe(0f);
    }

    [Fact]
    public void Jump_WhenOnGround_SetsVelocityY()
    {
        float jumpVelocity = 400f;
        var values = new PlatformerValues { JumpVelocity = jumpVelocity, MaxFallSpeed = 1000f };
        var jumpInput = new MockPressableInput(wasJustPressed: true);
        var behavior = new PlatformerBehavior { AirMovement = values, JumpInput = jumpInput };
        var entity = new Entity();
        entity.LastReposition = new System.Numerics.Vector2(0f, 5f); // pushed up → on ground

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityY.ShouldBe(jumpVelocity);
    }

    [Fact]
    public void Gravity_SetsNegativeAccelerationY()
    {
        float gravity = 600f;
        var values = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f };
        var behavior = new PlatformerBehavior { AirMovement = values };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.AccelerationY.ShouldBe(-gravity);
    }

    [Fact]
    public void MaxFallSpeed_ClampsDownwardVelocity()
    {
        float maxFallSpeed = 300f;
        var values = new PlatformerValues { Gravity = 0f, MaxFallSpeed = maxFallSpeed };
        var behavior = new PlatformerBehavior { AirMovement = values };
        var entity = new Entity { VelocityY = -999f }; // already falling faster than cap

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityY.ShouldBe(-maxFallSpeed);
    }

    [Fact]
    public void Jump_SustainContinuesWhileButtonHeld()
    {
        float jumpVelocity = 400f;
        var values = new PlatformerValues
        {
            JumpVelocity = jumpVelocity,
            JumpApplyLength = TimeSpan.FromSeconds(0.2),
            JumpApplyByButtonHold = true,
            MaxFallSpeed = 1000f,
        };
        // Button held down across both frames
        var jumpInput = new MockPressableInput(isDown: true, wasJustPressed: true);
        var behavior = new PlatformerBehavior { AirMovement = values, JumpInput = jumpInput };
        var entity = new Entity();
        entity.LastReposition = new System.Numerics.Vector2(0f, 5f); // on ground

        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 0f));
        // Second frame: button still held, within JumpApplyLength — sustain should keep VelocityY = jumpVelocity
        var heldInput = new MockPressableInput(isDown: true, wasJustPressed: false);
        behavior.JumpInput = heldInput;
        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        entity.VelocityY.ShouldBe(jumpVelocity);
    }

    [Fact]
    public void Jump_EarlyRelease_WithButtonHold_CancelsSustain()
    {
        float jumpVelocity = 400f;
        var values = new PlatformerValues
        {
            JumpVelocity = jumpVelocity,
            JumpApplyLength = TimeSpan.FromSeconds(0.2),
            JumpApplyByButtonHold = true,
            MaxFallSpeed = 1000f,
            Gravity = 0f, // disable so gravity doesn't interfere with VelocityY assertion
        };
        var jumpInput = new MockPressableInput(isDown: true, wasJustPressed: true);
        var behavior = new PlatformerBehavior { AirMovement = values, JumpInput = jumpInput };
        var entity = new Entity();
        entity.LastReposition = new System.Numerics.Vector2(0f, 5f); // on ground

        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 0f));
        // Second frame: button released early — sustain should cancel, VelocityY no longer forced to jumpVelocity
        var releasedInput = new MockPressableInput(isDown: false, wasJustPressed: false);
        behavior.JumpInput = releasedInput;
        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        behavior.IsApplyingJump.ShouldBeFalse();
    }

    // --- Mock helpers ---

    private sealed class MockPressableInput : IPressableInput
    {
        public bool IsDown { get; }
        public bool WasJustPressed { get; }
        public bool WasJustReleased { get; }

        public MockPressableInput(bool isDown = false, bool wasJustPressed = false, bool wasJustReleased = false)
        {
            IsDown = isDown;
            WasJustPressed = wasJustPressed;
            WasJustReleased = wasJustReleased;
        }
    }

    private sealed class MockAxisInput : I2DInput
    {
        public float X { get; }
        public float Y { get; }

        public MockAxisInput(float x = 0f, float y = 0f)
        {
            X = x;
            Y = y;
        }
    }
}
