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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
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
            MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f,
            UphillFullSpeedSlope = 10f, UphillStopSpeedSlope = 45f,
        };
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: -45f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

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
        var values = new PlatformerValues { MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 30f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(100f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_DefaultValues_Downhill60Deg_AppliesFullBoost()
    {
        // Defaults: DownhillFullSpeedSlope=0, DownhillMaxSpeedSlope=60, Multiplier=1.5.
        var values = new PlatformerValues { MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f };
        var behavior = MakeSlopeBehavior(values, inputX: -1f, currentSlope: 60f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(-300f, tolerance: 0.01f);
    }

    [Fact]
    public void SlopeSpeed_DefaultValues_FlatGround_NoEffect()
    {
        var values = new PlatformerValues { MaxSpeedX = 200f, UsesAcceleration = false, MaxFallSpeed = 1000f };
        var behavior = MakeSlopeBehavior(values, inputX: 1f, currentSlope: 0f);
        var entity = MakeGroundedEntity();

        behavior.Update(entity, MakeFrame(1f / 60f));

        entity.VelocityX.ShouldBe(200f);
    }
}
