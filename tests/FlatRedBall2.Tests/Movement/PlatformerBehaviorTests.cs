using System;
using System.Numerics;
using FlatRedBall2.Collision;
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
        var values = new PlatformerValues { MaxSpeedX = maxSpeedX };
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
    public void DropThrough_AirborneDown_RequiresCanFallThroughTrue_ReturnsFalse()
    {
        var values = new PlatformerValues { CanFallThroughOneWayCollision = false };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            MovementInput = new MockAxisInput(y: -1f),
        };
        var entity = new Entity();
        entity.LastReposition = Vector2.Zero; // airborne

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsSuppressingOneWayCollision.ShouldBeFalse();
    }

    [Fact]
    public void DropThrough_AirborneDownHeld_SetsSuppressionFlag()
    {
        var values = new PlatformerValues();
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            MovementInput = new MockAxisInput(y: -1f),
        };
        var entity = new Entity();
        entity.LastReposition = Vector2.Zero; // airborne

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsSuppressingOneWayCollision.ShouldBeTrue();
    }

    [Fact]
    public void DropThrough_AirborneNoDown_SuppressionIsFalse()
    {
        var behavior = new PlatformerBehavior { AirMovement = new PlatformerValues() };
        var entity = new Entity();
        entity.LastReposition = Vector2.Zero; // airborne, no input

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsSuppressingOneWayCollision.ShouldBeFalse();
    }

    [Fact]
    public void DropThrough_DisabledByCanFallThroughFalse_JumpsNormally()
    {
        float jumpVelocity = 300f;
        var values = new PlatformerValues
        {
            CanFallThroughOneWayCollision = false,
            JumpVelocity = jumpVelocity,
            MaxFallSpeed = 1000f,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            JumpInput = new MockPressableInput(isDown: true, wasJustPressed: true),
            MovementInput = new MockAxisInput(y: -1f),
        };
        var entity = new Entity();
        entity.LastReposition = new Vector2(0f, 1f); // grounded

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsSuppressingOneWayCollision.ShouldBeFalse();
        entity.VelocityY.ShouldBe(jumpVelocity);
    }

    [Fact]
    public void DropThrough_GroundedDownAndJump_DoesNotApplyJumpVelocity()
    {
        var values = new PlatformerValues
        {
            JumpVelocity = 300f,
            MaxFallSpeed = 1000f,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            JumpInput = new MockPressableInput(isDown: true, wasJustPressed: true),
            MovementInput = new MockAxisInput(y: -1f),
        };
        var entity = new Entity();
        entity.LastReposition = new Vector2(0f, 1f); // grounded

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityY.ShouldBe(0f);
        behavior.IsApplyingJump.ShouldBeFalse();
    }

    [Fact]
    public void DropThrough_GroundedDownAndJump_SetsSuppressionFlag()
    {
        var values = new PlatformerValues
        {
            JumpVelocity = 300f,
            MaxFallSpeed = 1000f,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            JumpInput = new MockPressableInput(isDown: true, wasJustPressed: true),
            MovementInput = new MockAxisInput(y: -1f),
        };
        var entity = new Entity { Y = 100f };
        entity.LastReposition = new Vector2(0f, 1f); // grounded

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsSuppressingOneWayCollision.ShouldBeTrue();
    }

    [Fact]
    public void DropThrough_SuppressionClearsNextFrame_WhenDownReleased()
    {
        var values = new PlatformerValues
        {
            JumpVelocity = 300f,
            MaxFallSpeed = 1000f,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            JumpInput = new MockPressableInput(isDown: true, wasJustPressed: true),
            MovementInput = new MockAxisInput(y: -1f),
        };
        var entity = new Entity { Y = 100f };
        entity.LastReposition = new Vector2(0f, 1f); // grounded — triggers drop-through

        behavior.Update(entity, MakeFrame(1f / 60f));
        behavior.IsSuppressingOneWayCollision.ShouldBeTrue();

        // Next frame: release all input. Suppression should clear (one-frame flag expired,
        // and airborne-Down path is inactive).
        entity.LastReposition = Vector2.Zero; // airborne
        behavior.JumpInput = new MockPressableInput();
        behavior.MovementInput = new MockAxisInput();
        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        behavior.IsSuppressingOneWayCollision.ShouldBeFalse();
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

        var values = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f };
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

        var values = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f };
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

    // --- Ground snap (slope adherence) ---
    //
    // Ground snap is driven implicitly by any CollisionRelationship with
    // SlopeMode = PlatformerFloor — the relationship calls ConsiderSnappingTo on the
    // IPlatformerEntity side during collision dispatch. Tests below build a minimal
    // PlayerEntity implementing IPlatformerEntity and drive the relationship's
    // RunCollisions() to emulate the engine's collision pass, then call Update() to
    // close out the frame (updating _wasOnGroundLastFrame).

    private sealed class PlayerEntity : Entity, IPlatformerEntity
    {
        public PlatformerBehavior Behavior { get; } = new();
        public PlatformerBehavior Platformer => Behavior;
    }

    private static PlatformerValues MakeSnapValues(float snapDistance = 16f, float maxAngleDegrees = 60f)
        => new()
        {
            Gravity = 0f,
            MaxFallSpeed = 1000f,
            SlopeSnapDistance = snapDistance,
            SlopeSnapMaxAngleDegrees = maxAngleDegrees,
        };

    private static TileShapeCollection MakeTiles(int gridSize = 16)
        => new() { GridSize = gridSize };

    // Creates a collision shape attached to the entity such that the shape's bottom edge
    // (feet) is at entity.Y. Shape height is 2 with a +1 Y offset, so AbsoluteY = entity.Y + 1,
    // and feet = AbsoluteY - Height/2 = entity.Y.
    private static AxisAlignedRectangle AttachFeetShape(Entity entity)
    {
        var shape = new AxisAlignedRectangle { Width = 2f, Height = 2f, Y = 1f };
        entity.Add(shape);
        return shape;
    }

    // Builds a relationship with SlopeMode = PlatformerFloor so RunCollisions invokes
    // ConsiderSnappingTo on player.Platformer.
    private static CollisionRelationship<PlayerEntity, TileShapeCollection> MakePlatformerRelationship(
        PlayerEntity player, TileShapeCollection tiles)
    {
        var rel = new CollisionRelationship<PlayerEntity, TileShapeCollection>(
            new[] { player }, new[] { tiles })
        {
            SlopeMode = SlopeCollisionMode.PlatformerFloor,
        };
        return rel;
    }

    [Fact]
    public void GroundSnap_CollisionShapeNullWithDistance_Throws()
    {
        // Active values have SlopeSnapDistance > 0 but the user forgot CollisionShape.
        // This is a wiring bug we surface loudly rather than silently skip.
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate to reach the null check.
        // Deliberately no CollisionShape set.
        var rel = MakePlatformerRelationship(player, tiles);

        // Frame 1: pretend grounded last frame so the snap gate opens on frame 2.
        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;

        var ex = Should.Throw<InvalidOperationException>(() => rel.RunCollisions());
        ex.Message.ShouldContain("CollisionShape is null");
    }

    [Fact]
    public void GroundSnap_DisabledByZeroDistance_DoesNotSnap()
    {
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 0f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeFalse();
    }

    [Fact]
    public void GroundSnap_DownslopeToFlat_StaysGrounded()
    {
        // "Ran off a downslope onto lower flat ground" — the right cell (1,0) is a polygon
        // filling the bottom half (flat top at y=8). Player just crossed the seam at x=16
        // with feet at y=16 — an 8 unit gap to the flat ground below.
        // CurrentSlope is primed non-zero to simulate the slope probe having fired while the
        // entity was on the preceding downslope. Without this, the "was on a slope last frame"
        // gate rejects the snap (see GroundSnap_FlatToFlatWithGap_DoesNotSnap).
        var tiles = MakeTiles();
        var lowerFlat = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  0f),
            new Vector2(-8f,  0f),
        });
        tiles.AddPolygonTileAtCell(1, 0, lowerFlat);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 20f, Y = 16f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // simulate "last frame was on a 30° downslope"
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeTrue();
        player.Y.ShouldBe(8f);
    }

    [Fact]
    public void GroundSnap_FlatToFlatWithGap_DoesNotSnap()
    {
        // Staircase case: walking off the top of a flat tile onto a lower flat tile within
        // SlopeSnapDistance. Intent is a ballistic cliff drop — classic platformer feel. The
        // snap must be gated on "was on a slope last frame"; CurrentSlope = 0 from flat-ground
        // walking blocks it.
        var tiles = MakeTiles();
        var lowerFlat = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  0f),
            new Vector2(-8f,  0f),
        });
        tiles.AddPolygonTileAtCell(1, 0, lowerFlat);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 20f, Y = 16f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        // CurrentSlope not primed — defaults to 0, representing "was walking on flat ground."
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeFalse();
        player.Y.ShouldBe(16f); // unchanged — no snap, entity falls ballistically
    }

    [Fact]
    public void GroundSnap_FlatToFlatWithGap_EmitsPriorSurfaceFlatDiagnostic()
    {
        // The new slope gate must emit a diagnostic matching the gate that fired, so logs make
        // the no-snap reason unambiguous — same convention as the other skip reasons.
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        // CurrentSlope not primed — the gate should skip and emit the flat-surface reason.
        var messages = new System.Collections.Generic.List<string>();
        player.Behavior.OnSnapDiagnostic = messages.Add;
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        messages.Clear();
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();

        messages.Count.ShouldBe(1);
        messages[0].ShouldContain("skip: prior surface flat");
    }

    [Fact]
    public void GroundSnap_SmallNonZeroSlope_StillSnaps()
    {
        // Gate is strict non-zero — a gentle 5° downslope last frame still produces the
        // hug-across-seam behavior. No angle threshold on the snap gate itself (steepness
        // of the candidate surface is a separate check via SlopeSnapMaxAngleDegrees).
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 5f; // very gentle downslope
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeTrue();
        player.Y.ShouldBe(16f);
    }

    [Fact]
    public void GroundSnap_GapExceedsDistance_DoesNotSnap()
    {
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 100f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeFalse();
    }

    [Fact]
    public void GroundSnap_MultipleRelationships_SnapsOnceToSurfaceInRange()
    {
        // Two PlatformerFloor relationships. Only _tilesB has a surface within range.
        // Snap must fire exactly once (no double-snap) and land the player on _tilesB's surface.
        var tilesA = MakeTiles();
        // tilesA has no surface within range — tile at col 0 is offscreen from player at x=100.
        var tilesB = MakeTiles();
        tilesB.AddTileAtCell(6, 0); // cell (6,0): spans x∈[96,112], top y=16
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 104f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing multi-rel dispatch, not the slope gate.
        var messages = new System.Collections.Generic.List<string>();
        player.Behavior.OnSnapDiagnostic = messages.Add;

        var relA = MakePlatformerRelationship(player, tilesA);
        var relB = MakePlatformerRelationship(player, tilesB);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        messages.Clear();
        player.LastReposition = Vector2.Zero;
        relA.RunCollisions();
        relB.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeTrue();
        player.Y.ShouldBe(16f);
        // Exactly one "snap:" message — the second relationship must no-op after the first success.
        int snapCount = 0;
        foreach (var m in messages)
            if (m.Contains("snap:") && !m.Contains("skip:")) snapCount++;
        snapCount.ShouldBe(1);
    }

    [Fact]
    public void GroundSnap_MultipleRelationshipsAfterFirstSnap_EmitsAlreadySnappedDiagnostic()
    {
        // Two PlatformerFloor relationships, both with a valid surface within snap range.
        // First RunCollisions snaps. Second RunCollisions (no Update between) must hit the
        // _snappedThisFrame guard and emit "skip: already snapped this frame" so logs are
        // unambiguous about double-snap situations.
        var tilesA = MakeTiles();
        tilesA.AddTileAtCell(0, 0);
        var tilesB = MakeTiles();
        tilesB.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing already-snapped diagnostic.
        var messages = new System.Collections.Generic.List<string>();
        player.Behavior.OnSnapDiagnostic = messages.Add;
        var relA = MakePlatformerRelationship(player, tilesA);
        var relB = MakePlatformerRelationship(player, tilesB);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        messages.Clear();
        player.LastReposition = Vector2.Zero;
        relA.RunCollisions();
        relB.RunCollisions();

        messages.Count.ShouldBe(2);
        messages[0].ShouldContain("snap: entityY=");
        messages[1].ShouldContain("skip: already snapped this frame");
    }

    [Fact]
    public void GroundSnap_NonPlatformerEntity_DoesNotCrashOrSnap()
    {
        // Plain Entity (not IPlatformerEntity) on a PlatformerFloor relationship.
        // The relationship must silently skip the snap offer.
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var entity = new Entity { X = 8f, Y = 20f };
        AttachFeetShape(entity); // so collision has something to compare
        var rel = new CollisionRelationship<Entity, TileShapeCollection>(
            new[] { entity }, new[] { tiles })
        {
            SlopeMode = SlopeCollisionMode.PlatformerFloor,
        };

        Should.NotThrow(() => rel.RunCollisions());
    }

    [Fact]
    public void GroundSnap_NotGroundedLastFrame_DoesNotSnap()
    {
        // Simulates a jump: player is airborne and rising, with a surface below within range.
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f, VelocityY = 100f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        var rel = MakePlatformerRelationship(player, tiles);

        // Never grounded — rel.RunCollisions should skip snap.
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f));

        player.Behavior.IsOnGround.ShouldBeFalse();
        player.Y.ShouldBe(20f);
    }

    [Fact]
    public void GroundSnap_OnSnapDiagnostic_OnRaycastMiss_InvokedOnceWithSkipReason()
    {
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 100f }; // far above — ray misses
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing raycast-miss diagnostic.
        var messages = new System.Collections.Generic.List<string>();
        player.Behavior.OnSnapDiagnostic = messages.Add;
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        messages.Clear();
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();

        messages.Count.ShouldBe(1);
        messages[0].ShouldContain("skip: raycast missed");
    }

    [Fact]
    public void GroundSnap_OnSnapDiagnostic_OnSuccessfulSnap_InvokedOnceWithSnapMessage()
    {
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing successful-snap diagnostic.
        var messages = new System.Collections.Generic.List<string>();
        player.Behavior.OnSnapDiagnostic = messages.Add;
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        messages.Clear();
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();

        messages.Count.ShouldBe(1);
        messages[0].ShouldContain("snap: entityY=");
        messages[0].ShouldContain("shape=AxisAlignedRectangle");
        messages[0].ShouldContain("cell=(col=0,row=0)");
        messages[0].ShouldContain("probe=(");
    }

    [Fact]
    public void GroundSnap_OnSnapDiagnostic_PolygonHit_ReportsShapePolygon()
    {
        var tiles = MakeTiles();
        var lowerFlat = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  0f),
            new Vector2(-8f,  0f),
        });
        tiles.AddPolygonTileAtCell(0, 0, lowerFlat);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 4f, Y = 16f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing polygon shape-classification diagnostic.
        var messages = new System.Collections.Generic.List<string>();
        player.Behavior.OnSnapDiagnostic = messages.Add;
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        messages.Clear();
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();

        messages.Count.ShouldBe(1);
        messages[0].ShouldContain("shape=Polygon");
    }

    [Fact]
    public void GroundSnap_SurfaceTooSteep_DoesNotSnap()
    {
        var tiles = MakeTiles();
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        tiles.AddPolygonTileAtCell(0, 0, slope);
        var values = MakeSnapValues(snapDistance: 16f, maxAngleDegrees: 30f);
        var player = new PlayerEntity { X = 4f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing steepness gate specifically.
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeFalse();
        player.Y.ShouldBe(20f);
    }

    [Fact]
    public void GroundSnap_SurfaceWithinDistance_SnapsAndGrounds()
    {
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0);
        var values = MakeSnapValues(snapDistance: 16f);
        var player = new PlayerEntity { X = 8f, Y = 20f };
        player.Behavior.AirMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);
        player.Behavior.CurrentSlope = 30f; // past the flat-surface gate — testing successful snap.
        var rel = MakePlatformerRelationship(player, tiles);

        player.LastReposition = new Vector2(0f, 1f);
        player.Behavior.Update(player, MakeFrame(1f / 60f));
        player.LastReposition = Vector2.Zero;
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        player.Behavior.IsOnGround.ShouldBeTrue();
        player.Y.ShouldBe(16f);
        player.VelocityY.ShouldBe(0f);
    }

    [Fact]
    public void DropThrough_FullCycle_PlayerPassesBelowTileWithoutBeingPoppedBack()
    {
        // End-to-end regression for the drop-through cycle. Catches a subtle bug class: if
        // drop-through suppression lasts one frame. After that frame the entity's
        // LastPosition is below the surface, so the one-way gate's positional check
        // naturally prevents re-landing. This test releases all input after the Down+Jump
        // to verify the one-frame flag is sufficient — no held-Down path involved.
        var tiles = MakeTiles();
        tiles.AddTileAtCell(0, 0); // tile spans Y in [0, 16], top at Y=16

        var values = new PlatformerValues
        {
            Gravity = 600f,
            MaxFallSpeed = 1000f,
            JumpVelocity = 400f,
            MaxSpeedX = 0f,
            SlopeSnapDistance = 0f,
        };

        // Player at Y=16 so feet shape (bottom at entity.Y - 1, top at entity.Y + 1) overlaps
        // the tile's top edge by 1 unit — enough for the one-way-up relationship to separate
        // upward while the player is "standing on" the tile.
        var player = new PlayerEntity { X = 8f, Y = 16f };
        player.Behavior.AirMovement = values;
        player.Behavior.GroundMovement = values;
        player.Behavior.CollisionShape = AttachFeetShape(player);

        var rel = new CollisionRelationship<PlayerEntity, TileShapeCollection>(
            new[] { player }, new[] { tiles })
        {
            OneWayDirection = OneWayDirection.Up,
            AllowDropThrough = true,
        };
        rel.MoveFirstOnCollision();

        float dt = 1f / 60f;
        float totalTime = 0f;

        var noInput = new MockPressableInput();
        var noAxis = new MockAxisInput();
        var downJumpPress = new MockPressableInput(isDown: true, wasJustPressed: true);
        var downHeldAxis = new MockAxisInput(y: -1f);

        player.Behavior.JumpInput = noInput;
        player.Behavior.MovementInput = noAxis;

        // Step 1: run a few grounded frames to establish IsOnGround = true.
        for (int i = 0; i < 4; i++)
        {
            totalTime += dt;
            player.PhysicsUpdate(MakeFrame(dt));
            rel.RunCollisions();
            player.Behavior.Update(player, MakeFrame(dt, totalSeconds: totalTime));
        }

        player.Behavior.IsOnGround.ShouldBeTrue("player should be grounded on the tile before drop-through");
        float groundedY = player.Y;

        // Step 2: press Down + Jump to trigger drop-through.
        player.Behavior.JumpInput = downJumpPress;
        player.Behavior.MovementInput = downHeldAxis;

        totalTime += dt;
        player.PhysicsUpdate(MakeFrame(dt));
        rel.RunCollisions();
        player.Behavior.Update(player, MakeFrame(dt, totalSeconds: totalTime));

        player.Behavior.IsSuppressingOneWayCollision.ShouldBeTrue(
            "drop-through should activate suppression on the Down+Jump frame");

        // Step 3: release all input and let gravity carry the player down. Track the maximum
        // Y observed during descent — it must never exceed the initial grounded Y (which would
        // indicate a pop-back to the tile top).
        player.Behavior.JumpInput = noInput;
        player.Behavior.MovementInput = noAxis;

        float maxYDuringDescent = player.Y;
        for (int i = 0; i < 60; i++)
        {
            totalTime += dt;
            player.PhysicsUpdate(MakeFrame(dt));
            rel.RunCollisions();
            player.Behavior.Update(player, MakeFrame(dt, totalSeconds: totalTime));

            if (player.Y > maxYDuringDescent) maxYDuringDescent = player.Y;
            if (player.Y < -16f) break; // cleared the tile
        }

        player.Y.ShouldBeLessThan(-16f, "player should have fallen well below the tile");
        maxYDuringDescent.ShouldBeLessThanOrEqualTo(groundedY + 0.001f,
            "player must never be popped back above the tile's top during descent");
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

    // ── Acceleration / Deceleration ───────────────────────────────────────────

    private static PlatformerBehavior MakeAccelBehavior(float accelTime, float decelTime, float inputX, float startVelX, out Entity entity)
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f,
            MaxFallSpeed = 1000f,
            AccelerationTimeX = TimeSpan.FromSeconds(accelTime),
            DecelerationTimeX = TimeSpan.FromSeconds(decelTime),
            // Neutralize slope defaults — these tests target the accel/decel math only
            UphillFullSpeedSlope = 0f, UphillStopSpeedSlope = 0f,
            DownhillMaxSpeedMultiplier = 1f,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            MovementInput = new MockAxisInput(x: inputX),
        };
        entity = new Entity { VelocityX = startVelX };
        entity.LastReposition = new Vector2(0f, 1f); // grounded
        return behavior;
    }

    [Fact]
    public void Accel_SettingAccelerationTimeX_IsEnoughToEnableRamp()
    {
        // Regression: previously a `UsesAcceleration` bool gated the accel path, so setting
        // AccelerationTimeX without also flipping the bool silently did nothing and velocity
        // jumped instantly. Setting AccelerationTimeX alone must produce a ramp.
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            AccelerationTimeX = TimeSpan.FromSeconds(1f),
            DecelerationTimeX = TimeSpan.FromSeconds(1f),
            UphillFullSpeedSlope = 0f, UphillStopSpeedSlope = 0f,
            DownhillMaxSpeedMultiplier = 1f,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values, GroundMovement = values,
            MovementInput = new MockAxisInput(x: 1f),
        };
        var entity = new Entity();
        entity.LastReposition = new Vector2(0f, 1f);

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(0f); // velocity unchanged this frame
        entity.AccelerationX.ShouldBe(200f, tolerance: 0.5f); // accel queued for next PhysicsUpdate
    }

    [Fact]
    public void Accel_FromRest_AppliesAccelerationTimeXRate()
    {
        // AccelTime=0.5s, MaxSpeed=200 → accel magnitude = 400/s.
        // dt=1/60 → per-frame delta = 6.667. AccelerationX = 6.667/dt = 400.
        var behavior = MakeAccelBehavior(accelTime: 0.5f, decelTime: 0.5f, inputX: 1f, startVelX: 0f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.AccelerationX.ShouldBe(400f, tolerance: 0.5f);
    }

    [Fact]
    public void Accel_WhenAtMaxSpeed_NoAccelerationApplied()
    {
        var behavior = MakeAccelBehavior(accelTime: 0.5f, decelTime: 0.5f, inputX: 1f, startVelX: 200f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.AccelerationX.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void Accel_WhenReleasingInput_UsesDecelerationTimeX()
    {
        // VelX=200, input=0 → target=0, diff=-200. Should use DecelerationTimeX.
        // DecelTime=0.25s, MaxSpeed=200 → decel magnitude = 800/s.
        var behavior = MakeAccelBehavior(accelTime: 0.5f, decelTime: 0.25f, inputX: 0f, startVelX: 200f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        // AccelerationX should be negative (braking) at decel rate
        entity.AccelerationX.ShouldBe(-800f, tolerance: 0.5f);
    }

    [Fact]
    public void Accel_WhenOvershooting_ClampsToTarget()
    {
        // VelX=5, input=1, target=200, diff=195. Very short dt, very fast accel → would overshoot.
        // But with small diff and big accelMagnitude, the clamp should limit delta to exactly diff.
        // Using tiny accel time (0.001s) so accelMagnitude=200000, dt=1/60 → maxDelta=3333, diff=195 → clamp to 195.
        var behavior = MakeAccelBehavior(accelTime: 0.001f, decelTime: 0.5f, inputX: 1f, startVelX: 5f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        // clampedDiff = 195, AccelerationX = 195 / (1/60) = 11700
        entity.AccelerationX.ShouldBe(11700f, tolerance: 1f);
    }

    [Fact]
    public void Accel_WhenReversingDirection_UsesDecelerationTimeX()
    {
        // VelX=+200 (moving right at full speed), input=-1 (target=-200).
        // This is a REVERSAL — the entity is moving opposite to target.
        // Correct behavior (matches FRB1): brake at DecelerationTimeX rate, not AccelerationTimeX.
        // Otherwise, a long accel time (e.g. slippery ice) would make reversing feel instant
        // instead of requiring a slow skid-to-a-stop.
        var behavior = MakeAccelBehavior(accelTime: 1.0f, decelTime: 0.25f, inputX: -1f, startVelX: 200f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        // Expected: decel magnitude = 200/0.25 = 800/s. AccelerationX should be -800.
        // Buggy: accel magnitude = 200/1.0 = 200/s. AccelerationX would be -200.
        entity.AccelerationX.ShouldBe(-800f, tolerance: 0.5f);
    }

    [Fact]
    public void Accel_WhenOppositeVelocity_UsesDecelerationUntilZero()
    {
        // VelX=-50 (drifting left), input=+1 (target=+200).
        // Still a reversal — must brake (decel) before accelerating in the new direction.
        var behavior = MakeAccelBehavior(accelTime: 1.0f, decelTime: 0.25f, inputX: 1f, startVelX: -50f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        // Expected: decel magnitude 800/s → AccelerationX = +800.
        entity.AccelerationX.ShouldBe(800f, tolerance: 0.5f);
    }

    [Fact]
    public void Accel_SameDirectionBelowMax_UsesAccelerationTimeX()
    {
        // VelX=+100 (moving right at half speed), input=+1 (target=+200). Proper "speeding up".
        var behavior = MakeAccelBehavior(accelTime: 0.5f, decelTime: 0.25f, inputX: 1f, startVelX: 100f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        // Accel magnitude = 200/0.5 = 400/s.
        entity.AccelerationX.ShouldBe(400f, tolerance: 0.5f);
    }

    [Fact]
    public void Accel_SameDirectionAboveMax_UsesDeceleration()
    {
        // VelX=+300 (moving right FASTER than max, e.g. just landed after a downhill boost).
        // input=+1 (target=+200). Should brake back down to cap at decel rate.
        var behavior = MakeAccelBehavior(accelTime: 1.0f, decelTime: 0.25f, inputX: 1f, startVelX: 300f, out var entity);

        behavior.Update(entity, MakeFrame(1f / 60f));

        // Expected decel: AccelerationX = -800.
        entity.AccelerationX.ShouldBe(-800f, tolerance: 0.5f);
    }

    // ── Slope speed adjustment ────────────────────────────────────────────────

    private static PlatformerBehavior MakeSlopeBehavior(PlatformerValues values, float inputX, float currentSlope)
    {
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            MovementInput = new MockAxisInput(x: inputX),
        };
        behavior.CurrentSlope = currentSlope;
        return behavior;
    }

    private static Entity MakeGroundedEntity()
    {
        var e = new Entity();
        e.LastReposition = new Vector2(0f, 1f);
        return e;
    }

    [Fact]
    public void SlopeSpeed_WalkingUphill_AtStopSlope_VelocityIsZero()
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 45f,
        };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 45f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(0f);
    }

    [Fact]
    public void SlopeSpeed_WalkingUphill_BelowFullSpeedSlope_UsesFullSpeed()
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 45f,
        };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 5f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(200f);
    }

    [Fact]
    public void SlopeSpeed_WalkingUphill_Midway_LinearlyInterpolates()
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 50f,
        };
        // slope=30 halfway between 10 and 50 → multiplier 0.5
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 30f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(100f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_WalkingDownhill_OnUphillConfig_IsUnaffected()
    {
        // slope is positive (rises to +X), input is -X → walking downhill. Uphill config must not apply.
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 45f,
            DownhillMaxSpeedMultiplier = 1f, // disable downhill for this assertion
        };
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: 30f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(-200f);
    }

    [Fact]
    public void SlopeSpeed_WalkingDownhill_AtMaxSlope_AppliesFullMultiplier()
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            DownhillFullSpeedSlope = 10f, DownhillMaxSpeedSlope = 45f,
            DownhillMaxSpeedMultiplier = 1.5f,
        };
        // slope positive, input negative → downhill
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: 45f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(-300f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_WalkingDownhill_Midway_LinearlyInterpolatesMultiplier()
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            DownhillFullSpeedSlope = 10f, DownhillMaxSpeedSlope = 50f,
            DownhillMaxSpeedMultiplier = 2f,
        };
        // slope=30 halfway → multiplier 1.5
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: 30f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(-300f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_WhenAirborne_SlopeIgnored()
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 45f,
        };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 45f);
        var entity = new Entity(); // not grounded

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(200f);
        behavior.CurrentSlope.ShouldBe(0f); // reset when airborne
    }

    [Fact]
    public void SlopeSpeed_NegativeSlope_MovingLeft_IsUphill()
    {
        // slope negative (rises to -X), input -X → walking uphill
        var values = new PlatformerValues
        {
            MaxSpeedX = 200f, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 45f,
        };
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: -45f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(0f);
    }

    [Fact]
    public void ContributeGroundVelocity_NoInput_AddsPlatformVelocityToEntityVelocity()
    {
        // Standing on a platform moving right at 50 — entity should ride along even with no input.
        var values = new PlatformerValues { MaxSpeedX = 200f, MaxFallSpeed = 1000f };
        var behavior = new PlatformerBehavior { AirMovement = values, GroundMovement = values };
        var entity = MakeGroundedEntity();

        // Simulate the collision pass calling ContributeGroundVelocity, then the behavior runs.
        behavior.ContributeGroundVelocity(50f);
        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(50f);
    }

    [Fact]
    public void ContributeGroundVelocity_WithInput_AddsToInputDrivenSpeed()
    {
        // Holding right on a right-moving platform stacks: input gives MaxSpeedX, platform adds 50.
        float maxSpeedX = 200f;
        var values = new PlatformerValues { MaxSpeedX = maxSpeedX, MaxFallSpeed = 1000f };
        var behavior = new PlatformerBehavior
        {
            AirMovement = values,
            GroundMovement = values,
            MovementInput = new MockAxisInput(x: 1f),
        };
        var entity = MakeGroundedEntity();

        behavior.ContributeGroundVelocity(50f);
        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(maxSpeedX + 50f);
    }

    [Fact]
    public void ContributeGroundVelocity_NotCalledNextFrame_OffsetClears()
    {
        // After leaving the platform, no more contribution → the additive offset must be 0.
        var values = new PlatformerValues { MaxSpeedX = 200f, MaxFallSpeed = 1000f };
        var behavior = new PlatformerBehavior { AirMovement = values, GroundMovement = values };
        var entity = MakeGroundedEntity();

        behavior.ContributeGroundVelocity(50f);
        behavior.Update(entity, MakeFrame(1f / 60f));
        entity.VelocityX.ShouldBe(50f);

        // Next frame: no ContributeGroundVelocity call. Offset should reset to 0.
        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        entity.VelocityX.ShouldBe(0f);
    }

    [Fact]
    public void ContributeSlopeProbe_On45DegSlope_PopulatesCurrentSlope()
    {
        var tiles = new FlatRedBall2.Collision.TileShapeCollection { GridSize = 16f };
        var slope = FlatRedBall2.Collision.Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        tiles.AddPolygonTileAtCell(0, 0, slope);

        // Cell (0,0) spans world X [0..16], Y [0..16]. Surface at X=12 is Y=12.
        var collisionShape = new FlatRedBall2.Collision.AxisAlignedRectangle
        { Width = 8f, Height = 8f, X = 12f, Y = 12f + 4f };
        var behavior = new PlatformerBehavior
        {
            AirMovement = new PlatformerValues(),
            CollisionShape = collisionShape,
        };
        var entity = new Entity { X = 12f };
        entity.LastReposition = new Vector2(0f, 1f); // grounded gate

        // Invoke probe via the same entry point the relationship uses
        typeof(PlatformerBehavior)
            .GetMethod("ContributeSlopeProbe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(behavior, new object[] { entity, tiles });

        behavior.CurrentSlope.ShouldBe(45f, tolerance: 1f);
    }

    [Fact]
    public void SlopeSpeed_DefaultValues_Uphill30Deg_HalvesSpeed()
    {
        // Defaults: UphillFullSpeedSlope=0, UphillStopSpeedSlope=60 → at 30° multiplier=0.5.
        var values = new PlatformerValues { MaxSpeedX = 200f, MaxFallSpeed = 1000f };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 30f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(100f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_DefaultValues_Downhill60Deg_AppliesFullBoost()
    {
        // Defaults: DownhillFullSpeedSlope=0, DownhillMaxSpeedSlope=60, Multiplier=1.5.
        var values = new PlatformerValues { MaxSpeedX = 200f, MaxFallSpeed = 1000f };
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: 60f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(-300f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_DefaultValues_FlatGround_NoEffect()
    {
        var values = new PlatformerValues { MaxSpeedX = 200f, MaxFallSpeed = 1000f };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 0f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(200f);
    }

    // ── Climbing ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsClimbing_JumpInput_ExitsClimbingAndAppliesClimbingJumpVelocity()
    {
        float climbJumpVelocity = 250f;
        var climbing = new PlatformerValues
        {
            MaxSpeedX = 80f,
            ClimbingSpeed = 100f,
            JumpVelocity = climbJumpVelocity,
        };
        var behavior = new PlatformerBehavior
        {
            AirMovement = new PlatformerValues { JumpVelocity = 400f, MaxFallSpeed = 1000f },
            ClimbingMovement = climbing,
            IsClimbing = true,
            JumpInput = new MockPressableInput(isDown: true, wasJustPressed: true),
        };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        behavior.IsClimbing.ShouldBeFalse();
        entity.VelocityY.ShouldBe(climbJumpVelocity);
    }

    [Fact]
    public void IsClimbing_SuppressesGravity()
    {
        float gravity = 600f;
        var climbing = new PlatformerValues { MaxSpeedX = 80f, ClimbingSpeed = 100f };
        var behavior = new PlatformerBehavior
        {
            AirMovement = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f },
            ClimbingMovement = climbing,
            IsClimbing = true,
        };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.AccelerationY.ShouldBe(0f);
    }

    [Fact]
    public void IsClimbing_TogglingOff_RestoresGravity()
    {
        float gravity = 600f;
        var climbing = new PlatformerValues { MaxSpeedX = 80f, ClimbingSpeed = 100f };
        var air = new PlatformerValues { Gravity = gravity, MaxFallSpeed = 1000f };
        var behavior = new PlatformerBehavior
        {
            AirMovement = air,
            ClimbingMovement = climbing,
            IsClimbing = true,
        };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));
        behavior.IsClimbing = false;
        behavior.Update(entity, MakeFrame(1f / 60f, totalSeconds: 1f / 60f));

        entity.AccelerationY.ShouldBe(-gravity);
    }

    [Fact]
    public void IsClimbing_TopOfLadderY_ClampsYAndZeroesUpwardVelocity()
    {
        float topY = 100f;
        var climbing = new PlatformerValues { MaxSpeedX = 80f, ClimbingSpeed = 100f };
        var behavior = new PlatformerBehavior
        {
            AirMovement = new PlatformerValues(),
            ClimbingMovement = climbing,
            IsClimbing = true,
            TopOfLadderY = topY,
            MovementInput = new MockAxisInput(y: 1f), // pushing up into the top
        };
        var entity = new Entity { Y = topY + 5f }; // already past the top

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.Y.ShouldBe(topY);
        entity.VelocityY.ShouldBe(0f);
    }

    [Fact]
    public void IsClimbing_VelocityY_TracksInputTimesClimbingSpeed()
    {
        float climbingSpeed = 120f;
        var climbing = new PlatformerValues { MaxSpeedX = 80f, ClimbingSpeed = climbingSpeed };
        var behavior = new PlatformerBehavior
        {
            AirMovement = new PlatformerValues(),
            ClimbingMovement = climbing,
            IsClimbing = true,
            MovementInput = new MockAxisInput(y: 1f),
        };
        var entity = new Entity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityY.ShouldBe(climbingSpeed);
    }

    [Fact]
    public void IsClimbing_WithNullClimbingMovement_Throws()
    {
        var behavior = new PlatformerBehavior
        {
            AirMovement = new PlatformerValues(),
            IsClimbing = true,
        };
        var entity = new Entity();

        var ex = Should.Throw<InvalidOperationException>(() => behavior.Update(entity, MakeFrame(1f / 60f)));
        ex.Message.ShouldContain("ClimbingMovement is null");
    }
}
