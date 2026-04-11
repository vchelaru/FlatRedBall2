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

    [Fact]
    public void Jump_FixedJump_ReachesMinHeight()
    {
        // Fixed jump (no push-to-hold): pure ballistic arc.
        // v = sqrt(2*g*h), peak after n = v/(g*dt) frames.
        // Discrete peak: Y(n) = n*dt*(v - g*n*dt/2).
        // With g=600, h=48, dt=1/60: v=240, n=24, Y(24)=48.0 exactly.
        float gravity = 600f;
        float minHeight = 48f;
        float dt = 1f / 60f;
        float v = MathF.Sqrt(2f * gravity * minHeight);           // 240
        float peakFrame = v / (gravity * dt);                      // 24
        int n = (int)MathF.Round(peakFrame);
        float expectedPeak = n * dt * (v - gravity * n * dt / 2f); // 48.0

        var values = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f, UsesAcceleration = false };
        values.SetJumpHeights(minHeight); // no maxHeight → fixed jump, no sustain

        float maxY = SimulateJump(values, held: false);

        maxY.ShouldBe(expectedPeak, tolerance: expectedPeak * 0.01);
    }

    [Fact]
    public void Jump_HeldJump_ReachesMaxHeight()
    {
        // Variable jump (push-to-hold): sustain cancels gravity (AccY=0),
        // so each sustain frame adds exactly v*dt of height.
        // sustainTime = (maxHeight - minHeight) / v.
        // Sustain frames with AccY=0: frames 1..N where N*dt = sustainTime (frame 0 also
        // gets AccY=0 via the sustain else-branch, but its PhysicsUpdate saw the entity at
        // rest so it contributes 0 height).
        // After sustain, coast is pure ballistic from v: peak = v²/(2g) = minHeight.
        // Total = sustainFrames * v * dt + ballisticPeak.
        float gravity = 600f;
        float minHeight = 48f;
        float maxHeight = 96f;
        float dt = 1f / 60f;
        float v = MathF.Sqrt(2f * gravity * minHeight);                // 240
        float sustainTime = (maxHeight - minHeight) / v;                // 0.2s
        int sustainFrames = (int)MathF.Round(sustainTime / dt);         // 12
        float sustainHeight = sustainFrames * v * dt;                   // 48.0
        float coastPeakFrame = v / (gravity * dt);                      // 24
        int cn = (int)MathF.Round(coastPeakFrame);
        float coastHeight = cn * dt * (v - gravity * cn * dt / 2f);     // 48.0
        float expectedPeak = sustainHeight + coastHeight;               // 96.0

        var values = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f, UsesAcceleration = false };
        values.SetJumpHeights(minHeight, maxHeight);

        float maxY = SimulateJump(values, held: true);

        maxY.ShouldBe(expectedPeak, tolerance: expectedPeak * 0.01);
    }

    /// <summary>
    /// Simulates a full jump arc matching the real game loop order
    /// (PhysicsUpdate → Collision → behavior.Update) and returns the peak Y.
    /// </summary>
    private static float SimulateJump(PlatformerValues values, bool held)
    {
        float dt = 1f / 60f;
        var behavior = new PlatformerBehavior { AirMovement = values };
        var entity = new Entity();

        var jumpPressed = new MockPressableInput(isDown: true, wasJustPressed: true);
        var jumpHeld = new MockPressableInput(isDown: held, wasJustPressed: false);
        behavior.JumpInput = jumpPressed;

        float maxY = 0f;
        float totalTime = 0f;

        for (int frame = 0; frame < 600; frame++)
        {
            totalTime += dt;

            entity.PhysicsUpdate(MakeFrame(dt));

            // Simulate ground contact on frame 0
            if (frame == 0)
                entity.LastReposition = new System.Numerics.Vector2(0f, 1f);

            behavior.Update(entity, MakeFrame(dt, totalSeconds: totalTime));

            if (frame == 0)
                behavior.JumpInput = jumpHeld;

            if (entity.Y > maxY) maxY = entity.Y;
            if (frame > 2 && entity.Y <= 0f && entity.VelocityY < 0f) break;
        }

        return maxY;
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
