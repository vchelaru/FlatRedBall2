using System;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class PlatformerBehaviorAirJumpTests
{
    private static FrameTime Frame(float dt = 1f / 60f, float total = 0f)
        => new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.FromSeconds(total));

    private static PlatformerBehavior MakePlatformer(PressableInput jump, float airJumpVelocity = 300f, PlatformerValues? afterDoubleJump = null)
    {
        var air = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = airJumpVelocity };
        var behavior = new PlatformerBehavior
        {
            AirMovement = air,
            JumpInput = jump,
            AfterDoubleJump = afterDoubleJump,
        };
        return behavior;
    }

    private static (Entity entity, AxisAlignedRectangle body) MakeAirborneEntity()
    {
        var entity = new Entity { X = 0f, Y = 100f };
        var body = new AxisAlignedRectangle { Width = 12f, Height = 20f, Y = 10f };
        entity.Add(body);
        // LastReposition default is Vector2.Zero → not grounded
        return (entity, body);
    }

    private static (Entity entity, AxisAlignedRectangle body) MakeGroundedEntity()
    {
        var entity = new Entity { X = 0f, Y = 0f };
        var body = new AxisAlignedRectangle { Width = 12f, Height = 20f, Y = 10f };
        entity.Add(body);
        entity.LastReposition = new Vector2(0f, 5f); // pushed up → grounded
        return (entity, body);
    }

    // ── Air jump basics ──────────────────────────────────────────────────────

    [Fact]
    public void AirJump_WhenAirMovementHasJumpVelocity_NoAfterDoubleJump_AppliesJumpVelocity()
    {
        float airJumpVelocity = 300f;
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: airJumpVelocity);
        var (entity, _) = MakeAirborneEntity();

        // First frame: not grounded, no jump
        platformer.Update(entity, Frame());

        // Second frame: press jump while airborne
        jump.Press();
        platformer.Update(entity, Frame());

        entity.VelocityY.ShouldBe(airJumpVelocity);
    }

    [Fact]
    public void AirJump_WhenAirMovementHasJumpVelocity_NoAfterDoubleJump_CanJumpAgainAfterRelease()
    {
        // No AfterDoubleJump → flutter: can air-jump again after releasing and re-pressing.
        float airJumpVelocity = 300f;
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: airJumpVelocity);
        var (entity, _) = MakeAirborneEntity();

        platformer.Update(entity, Frame()); // airborne, no jump

        jump.Press();
        platformer.Update(entity, Frame()); // first air jump

        jump.Release();
        entity.VelocityY = 0f; // simulate falling after jump (IsApplyingJump becomes false)
        platformer.Update(entity, Frame()); // released, no jump

        jump.Press();
        platformer.Update(entity, Frame()); // second air jump

        entity.VelocityY.ShouldBe(airJumpVelocity);
    }

    // ── AfterDoubleJump slot ─────────────────────────────────────────────────

    [Fact]
    public void AirJump_WhenAirMovementHasJumpVelocityAndAfterDoubleJump_FirstPress_AppliesVelocity()
    {
        float airJumpVelocity = 300f;
        var afterDoubleJump = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = 0f };
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: airJumpVelocity, afterDoubleJump: afterDoubleJump);
        var (entity, _) = MakeAirborneEntity();

        platformer.Update(entity, Frame()); // airborne, no jump

        jump.Press();
        platformer.Update(entity, Frame()); // first air jump

        entity.VelocityY.ShouldBe(airJumpVelocity);
    }

    [Fact]
    public void AirJump_WhenAirMovementHasJumpVelocityAndAfterDoubleJump_SecondPress_AfterDoubleJumpHasZeroVelocity_DoesNotJump()
    {
        float airJumpVelocity = 300f;
        var afterDoubleJump = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = 0f };
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: airJumpVelocity, afterDoubleJump: afterDoubleJump);
        var (entity, _) = MakeAirborneEntity();

        platformer.Update(entity, Frame()); // airborne, no jump

        jump.Press();
        platformer.Update(entity, Frame()); // first air jump applies

        jump.Release();
        entity.VelocityY = 0f; // simulate falling
        platformer.Update(entity, Frame());

        // Second press — AfterDoubleJump.JumpVelocity == 0 → locked out
        jump.Press();
        platformer.Update(entity, Frame());

        entity.VelocityY.ShouldBe(0f);
    }

    [Fact]
    public void AirJump_WhenAirMovementHasZeroJumpVelocity_DoesNotJump()
    {
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: 0f);
        var (entity, _) = MakeAirborneEntity();

        platformer.Update(entity, Frame());

        jump.Press();
        platformer.Update(entity, Frame());

        entity.VelocityY.ShouldBe(0f, tolerance: 0.001f);
    }

    // ── Guards ───────────────────────────────────────────────────────────────

    [Fact]
    public void AirJump_WhileSustainActive_DoesNotFireAirJump()
    {
        // IsApplyingJump == true → air jump must not fire (button hold sustain is active).
        float groundJumpVelocity = 400f;
        var ground = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = groundJumpVelocity, JumpApplyLength = TimeSpan.FromSeconds(0.3f) };
        var air = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = 999f };
        var jump = new PressableInput();
        var platformer = new PlatformerBehavior { GroundMovement = ground, AirMovement = air, JumpInput = jump };
        var entity = new Entity();
        entity.LastReposition = new Vector2(0f, 5f); // grounded

        // Press jump to trigger ground jump (with sustain)
        jump.Press();
        platformer.Update(entity, Frame(total: 0f));
        jump.Release();

        // Now airborne, sustain still active (within 0.3s window)
        entity.LastReposition = Vector2.Zero;
        jump.Press(); // re-press while sustain active
        platformer.Update(entity, Frame(total: 0.05f)); // within JumpApplyLength

        // VelocityY is still driven by sustain (groundJumpVelocity), not overwritten by air jump (999)
        entity.VelocityY.ShouldBe(groundJumpVelocity);
    }

    // ── Landing resets state ─────────────────────────────────────────────────

    [Fact]
    public void AirJump_AfterLanding_ResetsToAirMovement_CanDoubleJumpAgain()
    {
        // After using the double jump and landing, _activeAirValues resets → can air-jump again.
        float airJumpVelocity = 300f;
        var afterDoubleJump = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = 0f };
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: airJumpVelocity, afterDoubleJump: afterDoubleJump);
        var (entity, _) = MakeAirborneEntity();

        // Use the double jump — now _activeAirValues = afterDoubleJump
        platformer.Update(entity, Frame()); // airborne, no jump

        jump.Press();
        platformer.Update(entity, Frame()); // first air jump applied, _activeAirValues = afterDoubleJump

        // Land
        jump.Release();
        entity.VelocityY = 0f;
        entity.LastReposition = new Vector2(0f, 5f);
        platformer.Update(entity, Frame()); // lands → _activeAirValues reset to null

        // Go airborne again and air-jump: should work again because reset on landing
        entity.LastReposition = Vector2.Zero;
        entity.VelocityY = -10f; // falling
        platformer.Update(entity, Frame());

        jump.Press();
        platformer.Update(entity, Frame()); // air jump again — should succeed

        entity.VelocityY.ShouldBe(airJumpVelocity);
    }

    // ── Fell off ground ──────────────────────────────────────────────────────

    [Fact]
    public void AirJump_WhenFallingOffGround_CanAirJump()
    {
        // Player walks off a ledge (never pressed jump, just falling) → presses jump while airborne.
        float airJumpVelocity = 300f;
        var afterDoubleJump = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f, JumpVelocity = 0f };
        var jump = new PressableInput();
        var platformer = MakePlatformer(jump, airJumpVelocity: airJumpVelocity, afterDoubleJump: afterDoubleJump);
        var (entity, _) = MakeAirborneEntity();
        entity.VelocityY = -50f; // already falling

        platformer.Update(entity, Frame()); // airborne, falling, no jump pressed

        jump.Press();
        platformer.Update(entity, Frame()); // press jump while airborne → air jump

        entity.VelocityY.ShouldBe(airJumpVelocity);
    }

    // ── Mock input ────────────────────────────────────────────────────────────

    /// <summary>
    /// Manual pressable input for tests. Call Press() before an Update to simulate a button
    /// press that frame, then Release() to clear it. WasJustPressed and IsDown are not
    /// auto-reset — tests control them explicitly.
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
