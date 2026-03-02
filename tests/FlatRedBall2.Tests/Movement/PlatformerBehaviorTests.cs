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
