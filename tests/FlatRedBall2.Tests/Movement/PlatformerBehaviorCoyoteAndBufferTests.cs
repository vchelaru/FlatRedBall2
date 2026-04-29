using System;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class PlatformerBehaviorCoyoteAndBufferTests
{
    private static FrameTime Frame(float dt = 1f / 60f, float total = 0f)
        => new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.FromSeconds(total));

    private static (PlatformerBehavior platformer, Entity entity, PressableInput jump) MakeSetup(
        TimeSpan coyoteTime = default, TimeSpan jumpBuffer = default, float groundJumpVelocity = 400f)
    {
        var ground = new PlatformerValues
        {
            MaxSpeedX = 120f,
            Gravity = 700f,
            MaxFallSpeed = 800f,
            JumpVelocity = groundJumpVelocity,
            CoyoteTime = coyoteTime,
            JumpInputBufferDuration = jumpBuffer,
        };
        var air = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = 0f };
        var jump = new PressableInput();
        var platformer = new PlatformerBehavior
        {
            GroundMovement = ground,
            AirMovement = air,
            JumpInput = jump,
        };
        var entity = new Entity();
        var body = new AxisAlignedRectangle { Width = 12f, Height = 20f, Y = 10f };
        entity.Add(body);
        // Start grounded
        entity.LastReposition = new Vector2(0f, 5f);
        return (platformer, entity, jump);
    }

    private static void StepAirborne(PlatformerBehavior platformer, Entity entity, FrameTime time)
    {
        entity.LastReposition = Vector2.Zero;
        platformer.Update(entity, time);
    }

    private static void StepGrounded(PlatformerBehavior platformer, Entity entity, FrameTime time)
    {
        entity.LastReposition = new Vector2(0f, 5f);
        platformer.Update(entity, time);
    }

    // ── Coyote time ──────────────────────────────────────────────────────────

    [Fact]
    public void CoyoteTime_PressJumpWithinWindowAfterWalkingOffLedge_AppliesJumpVelocity()
    {
        // C1: 100ms coyote window. Walk off ledge, press jump 50ms later → jump.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(
            coyoteTime: TimeSpan.FromSeconds(0.1), groundJumpVelocity: jumpVelocity);

        // Frame 1: grounded, no jump
        StepGrounded(platformer, entity, Frame(total: 0f));

        // Frame 2: just walked off (50ms into game), still within coyote
        StepAirborne(platformer, entity, Frame(total: 0.05f));

        // Frame 3: press jump 50ms after leaving ground (at t=0.1s game time means 50ms after the t=0.05s ground-loss frame)
        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 0.10f));

        entity.VelocityY.ShouldBe(jumpVelocity);
    }

    [Fact]
    public void CoyoteTime_PressJumpAfterWindowExpires_DoesNotJump()
    {
        // C2: 100ms coyote window. Press jump 150ms after leaving ground → no jump.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(
            coyoteTime: TimeSpan.FromSeconds(0.1), groundJumpVelocity: jumpVelocity);

        StepGrounded(platformer, entity, Frame(total: 0f));
        StepAirborne(platformer, entity, Frame(total: 0.05f)); // just walked off at ~0.05s

        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 0.20f)); // ~150ms after ground-loss

        entity.VelocityY.ShouldNotBe(jumpVelocity);
    }

    [Fact]
    public void CoyoteTime_DefaultZero_PressJumpOneFrameAfterLeavingGround_DoesNotJump()
    {
        // C3: default behavior (CoyoteTime = Zero) preserved.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(groundJumpVelocity: jumpVelocity);

        StepGrounded(platformer, entity, Frame(total: 0f));
        StepAirborne(platformer, entity, Frame(total: 1f / 60f)); // walked off

        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 2f / 60f));

        entity.VelocityY.ShouldNotBe(jumpVelocity);
    }

    [Fact]
    public void CoyoteTime_DoesNotArmAfterDeliberateJump()
    {
        // C4: jumping off the ground does NOT arm coyote — second press during JumpApplyLength
        // window should be handled by air-jump (which has 0 velocity here), not as a coyote re-jump.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(
            coyoteTime: TimeSpan.FromSeconds(0.1), groundJumpVelocity: jumpVelocity);

        // Press jump while grounded
        jump.Press();
        StepGrounded(platformer, entity, Frame(total: 0f));
        entity.VelocityY.ShouldBe(jumpVelocity); // confirms ground jump fired

        jump.Release();
        // Now airborne, simulate falling
        entity.VelocityY = -10f;
        StepAirborne(platformer, entity, Frame(total: 0.03f));

        // Press jump within coyote window — must NOT trigger coyote re-jump (we already jumped).
        // Air slot has JumpVelocity = 0 so no air jump either → velocity stays falling.
        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 0.05f));

        entity.VelocityY.ShouldNotBe(jumpVelocity);
    }

    // ── Jump input buffer ────────────────────────────────────────────────────

    [Fact]
    public void JumpInputBuffer_PressJumpBeforeLandingWithinWindow_AppliesJumpOnLanding()
    {
        // B1: 100ms buffer. Press jump 50ms before landing → on landing frame, jump fires.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(
            jumpBuffer: TimeSpan.FromSeconds(0.1), groundJumpVelocity: jumpVelocity);

        // Start airborne (override the grounded default)
        entity.LastReposition = Vector2.Zero;
        platformer.Update(entity, Frame(total: 0f));

        // Press jump while airborne at t=0.05s
        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 0.05f));
        // Air JumpVelocity is 0, so no air jump fires
        entity.VelocityY.ShouldNotBe(jumpVelocity);

        jump.Release();

        // Land at t=0.10s (50ms after press, inside buffer window)
        StepGrounded(platformer, entity, Frame(total: 0.10f));

        entity.VelocityY.ShouldBe(jumpVelocity);
    }

    [Fact]
    public void JumpInputBuffer_PressJumpBeforeLandingOutsideWindow_DoesNotJumpOnLanding()
    {
        // B2: 100ms buffer. Press jump 150ms before landing → no jump on landing.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(
            jumpBuffer: TimeSpan.FromSeconds(0.1), groundJumpVelocity: jumpVelocity);

        entity.LastReposition = Vector2.Zero;
        platformer.Update(entity, Frame(total: 0f));

        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 0.05f));
        jump.Release();

        // Land at t=0.20s (150ms after press → outside buffer)
        StepGrounded(platformer, entity, Frame(total: 0.20f));

        entity.VelocityY.ShouldNotBe(jumpVelocity);
    }

    [Fact]
    public void JumpInputBuffer_DefaultZero_PressJumpOneFrameBeforeLanding_DoesNotJump()
    {
        // B3: default behavior (buffer = Zero) preserved — must press jump exactly on landing frame.
        float jumpVelocity = 400f;
        var (platformer, entity, jump) = MakeSetup(groundJumpVelocity: jumpVelocity);

        entity.LastReposition = Vector2.Zero;
        platformer.Update(entity, Frame(total: 0f));

        jump.Press();
        StepAirborne(platformer, entity, Frame(total: 1f / 60f));
        jump.Release();

        StepGrounded(platformer, entity, Frame(total: 2f / 60f));

        entity.VelocityY.ShouldNotBe(jumpVelocity);
    }

    [Fact]
    public void JumpInputBuffer_BufferedPressDoesNotAlsoTriggerAirDoubleJump()
    {
        // B4: a buffered press resolves only on landing — it must not also fire an air jump in
        // between (would burn the double-jump). Setup: air slot has its own JumpVelocity, so an
        // air jump would be visible if it fired.
        float groundJumpVelocity = 400f;
        float airJumpVelocity = 250f;
        var ground = new PlatformerValues
        {
            MaxSpeedX = 120f,
            Gravity = 700f,
            MaxFallSpeed = 800f,
            JumpVelocity = groundJumpVelocity,
            JumpInputBufferDuration = TimeSpan.FromSeconds(0.1),
        };
        var air = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = airJumpVelocity };
        var jump = new PressableInput();
        var platformer = new PlatformerBehavior { GroundMovement = ground, AirMovement = air, JumpInput = jump };
        var entity = new Entity();
        var body = new AxisAlignedRectangle { Width = 12f, Height = 20f, Y = 10f };
        entity.Add(body);

        // Airborne start
        entity.LastReposition = Vector2.Zero;
        platformer.Update(entity, Frame(total: 0f));

        // Press jump while airborne — this DOES fire air jump (existing behavior preserved, B4
        // just verifies buffer doesn't *additionally* mess with that).
        jump.Press();
        platformer.Update(entity, Frame(total: 0.02f));
        entity.VelocityY.ShouldBe(airJumpVelocity); // air jump fired

        jump.Release();
        // Falling again
        entity.VelocityY = -10f;
        entity.LastReposition = Vector2.Zero;
        platformer.Update(entity, Frame(total: 0.05f));

        // Land within buffer window of original press — but original press was already consumed
        // by air-jump, so landing should NOT additionally fire a ground jump.
        entity.LastReposition = new Vector2(0f, 5f);
        platformer.Update(entity, Frame(total: 0.08f));

        entity.VelocityY.ShouldNotBe(groundJumpVelocity);
    }

    /// <summary>
    /// Manual pressable input for tests. Mirrors the pattern in PlatformerBehaviorAirJumpTests.
    /// </summary>
    private sealed class PressableInput : IPressableInput
    {
        public bool IsDown { get; private set; }
        public bool WasJustPressed { get; private set; }
        public bool WasJustReleased { get; private set; }

        public void Press()
        {
            WasJustPressed = true;
            IsDown = true;
            WasJustReleased = false;
        }

        public void Release()
        {
            WasJustReleased = true;
            IsDown = false;
            WasJustPressed = false;
        }
    }
}
