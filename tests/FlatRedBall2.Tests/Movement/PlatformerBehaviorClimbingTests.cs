using System;
using System.Numerics;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Movement;

public class PlatformerBehaviorClimbingTests
{
    private static FrameTime Frame(float dt = 1f / 60f, float total = 0f)
        => new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.FromSeconds(total));

    private static PlatformerBehavior MakePlatformer(AxisAlignedRectangle body, I2DInput? input = null)
    {
        var climb = new PlatformerValues { MaxSpeedX = 80f, ClimbingSpeed = 100f, JumpVelocity = 200f };
        var air = new PlatformerValues { MaxSpeedX = 120f, Gravity = 700f, MaxFallSpeed = 800f };
        return new PlatformerBehavior
        {
            AirMovement = air,
            ClimbingMovement = climb,
            CollisionShape = body,
            MovementInput = input ?? new AxisInput(),
        };
    }

    private static (Entity entity, AxisAlignedRectangle body) MakeEntity(float x, float y)
    {
        var entity = new Entity { X = x, Y = y };
        var body = new AxisAlignedRectangle { Width = 12f, Height = 20f, Y = 10f };
        entity.Add(body);
        return (entity, body);
    }

    private static TileShapeCollection LadderColumn(float cellCenterX, float bottomY, int heightCells)
    {
        // GridSize=16, column centered on cellCenterX, stack of tiles starting at bottomY.
        var tsc = new TileShapeCollection { GridSize = 16f, X = cellCenterX - 8f, Y = bottomY };
        for (int r = 0; r < heightCells; r++) tsc.AddTileAtCell(0, r);
        return tsc;
    }

    // ── Ladder enter ──────────────────────────────────────────────────────────

    [Fact]
    public void EnterLadder_PressUpOverlapping_SetsIsClimbingAndSnapsX()
    {
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 97f, y: 8f); // body overlaps ladder column, X off-center
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        platformer.IsOnLadder.ShouldBeTrue();
        entity.X.ShouldBe(100f); // snapped to ladder column center
    }

    [Fact]
    public void EnterLadder_PressUp_OffByOneColumn_FindsLadderViaBodyRange()
    {
        // Entity X in col 5 (center 88), body 12 wide overlaps col 6 (center 104) by 2 units.
        // Naive GetCellAt(entity.X) returns col 5 — no ladder. Body-range scan must find col 6.
        var ladder = LadderColumn(cellCenterX: 104f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 92f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        entity.X.ShouldBe(104f);
    }

    [Fact]
    public void EnterLadder_NoInput_DoesNotEnter()
    {
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput());
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeFalse();
    }

    [Fact]
    public void EnterLadder_FromGroundPressingUp_DoesNotSelfCancelViaIsOnGround()
    {
        // Reproduces the "grounded entry is cancelled the next frame because IsOnGround is
        // still true from prior collision" bug. Holding Up while on the ground under a ladder
        // must sustain IsClimbing for at least the entry frame.
        var ladder = LadderColumn(100f, 16f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 4f);
        entity.LastReposition = new Vector2(0f, 5f); // pushed up by ground → IsOnGround=true
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
    }

    // ── Ladder X lock ─────────────────────────────────────────────────────────

    [Fact]
    public void WhileClimbingLadder_HorizontalInput_DoesNotMoveX()
    {
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(x: 1f, y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter
        float xAfterEnter = entity.X;
        platformer.Update(entity, Frame()); // continue climbing

        entity.X.ShouldBe(xAfterEnter);
        entity.VelocityX.ShouldBe(0f);
    }

    // ── Top-of-ladder clamp ───────────────────────────────────────────────────

    [Fact]
    public void TopOfLadderY_SetAutomatically_FromDetectedColumn()
    {
        // Ladder rows 0..4; GridSize 16; TSC.Y = 0 → row 4 center at y = 4*16 + 8 = 72; top edge = 80.
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter
        platformer.Update(entity, Frame()); // climbing frame — TopOfLadderY re-set

        platformer.TopOfLadderY.ShouldBe(80f);
    }

    [Fact]
    public void TopOfLadderY_ComputedEvenWhenFeetBelowLadder()
    {
        // Player feet below the lowest ladder cell but body reaches into it — common for ladders
        // that don't extend to the floor. Feet's row has no ladder tile; the scan must still
        // resolve the top.
        var ladder = LadderColumn(100f, 32f, 3); // ladder occupies y=32..80, top edge=80
        var (entity, body) = MakeEntity(x: 100f, y: 20f); // feet at 20 (body top=40, in ladder)
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.TopOfLadderY.ShouldBe(80f);
    }

    [Fact]
    public void HoldingUp_AtTopOfLadder_StaysClimbing_NoBounce()
    {
        // Regression: previously the clamp left entity.Y == TopOfLadderY, which put the body
        // bottom exactly on the column's top edge. Next frame's overlap scan returned null →
        // lostOverlap exit → re-grab → bounce. The clamp now insets so body stays inside the
        // topmost cell and IsClimbing persists across many frames of held Up.
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter
        platformer.IsClimbing.ShouldBeTrue();

        // Teleport to the clamp threshold (TopOfLadderY=80, body bottom at entity.Y → maxEntityY=79.5).
        // Body bottom at 79.5 stays inside the topmost ladder cell (row 4 spans 64..80).
        entity.Y = 79.5f;
        for (int i = 0; i < 30; i++)
            platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        entity.VelocityY.ShouldBe(0f);
        entity.Y.ShouldBe(79.5f); // clamped, no drift
    }

    [Fact]
    public void HoldingUp_AtTopOfLadder_WithSeparateClimbingShape_StaysClimbing()
    {
        // Same regression, but with a smaller ClimbingShape offset above entity.Y. The clamp
        // must use the climb shape's bottom offset, not assume the body bottom is at entity.Y.
        var ladder = LadderColumn(100f, 0f, 5);
        var entity = new Entity { X = 100f, Y = 8f };
        var body = new AxisAlignedRectangle { Width = 12f, Height = 20f, Y = 10f };
        var climbProbe = new AxisAlignedRectangle { Width = 4f, Height = 10f, Y = 10f };
        entity.Add(body);
        entity.Add(climbProbe);

        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.ClimbingShape = climbProbe;
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter
        platformer.IsClimbing.ShouldBeTrue();

        // Teleport to the clamp threshold. Probe Y=10, Height=10 → bottom = entity.Y + 5.
        // Want probe bottom = 79.5 (inside cell 4) → entity.Y = 74.5 (= maxEntityY).
        entity.Y = 74.5f;
        for (int i = 0; i < 30; i++)
            platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        entity.VelocityY.ShouldBe(0f);
        entity.Y.ShouldBe(74.5f);            // clamped, no drift
        (entity.Y + 5f).ShouldBe(79.5f);     // probe bottom inside topmost cell
    }

    [Fact]
    public void ClimbingUp_PhysicsOvershootsTop_ClampPullsBack_StaysClimbing()
    {
        // Real-game scenario: the previous frame velocity carried the entity past the clamp
        // threshold (physics ran with vel>0). Pre-Update overlap scan sees the overshot Y →
        // preLadderCol = null. The clamp pulls Y back inside the ladder, but without the
        // post-clamp re-scan in the exit gate, lostOverlap fires (using stale preLadderCol),
        // climb exits, gravity, re-grab → bounce. The post-clamp re-scan prevents the exit.
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter
        platformer.IsClimbing.ShouldBeTrue();

        // Simulate physics overshoot: place entity past TopOfLadderY=80 (body bottom in row 5).
        entity.Y = 80.9f;
        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();         // clamp + re-scan kept us climbing
        entity.Y.ShouldBe(79.5f);                     // pulled back inside topmost cell
        entity.VelocityY.ShouldBe(0f);                // no upward residual to overshoot again
    }

    // ── Top-of-ladder horizontal-input exit ──────────────────────────────────

    [Fact]
    public void ClimbToTop_PressLeft_ExitsClimbingAndSnapsToLadderTop()
    {
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(x: -1f, y: 1f)); // left + up
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter climbing
        entity.Y = 79.5f;
        platformer.Update(entity, Frame()); // clamp fires, inputX < 0 → exit

        platformer.IsClimbing.ShouldBeFalse();
        entity.VelocityY.ShouldBe(0f);
        entity.Y.ShouldBe(80f, tolerance: 0.1f); // feet snapped to TopOfLadderY
    }

    [Fact]
    public void ClimbToTop_NoHorizontalInput_RemainsClimbing()
    {
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f)); // up only, no horizontal
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter
        entity.Y = 79.5f;
        for (int i = 0; i < 5; i++) platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        entity.Y.ShouldBe(79.5f);
    }

    [Fact]
    public void ClimbToTop_PressRight_ExitsClimbingAndSnapsToLadderTop()
    {
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(x: 1f, y: 1f)); // right + up
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter climbing
        platformer.IsClimbing.ShouldBeTrue();

        entity.Y = 79.5f; // at clamp threshold (TopOfLadderY=80, maxEntityY=79.5)
        platformer.Update(entity, Frame()); // clamp fires, inputX > 0 → exit

        platformer.IsClimbing.ShouldBeFalse();
        entity.VelocityY.ShouldBe(0f);
        entity.Y.ShouldBe(80f, tolerance: 0.1f); // feet snapped to TopOfLadderY
    }

    // ── Climb-down from standing (no body overlap) ────────────────────────────

    [Fact]
    public void ClimbDownFromStanding_GroundedAboveLadder_PressDown_EntersCimbing()
    {
        // Ladder top edge at y=80. Entity feet at y=80 → body bottom exactly at top edge,
        // no tile overlap. Pressing Down while grounded must enter climbing.
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 80f);
        entity.LastReposition = new Vector2(0f, 5f); // grounded
        var platformer = MakePlatformer(body, new AxisInput(y: -1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        entity.X.ShouldBe(100f); // snapped to column center
    }

    [Fact]
    public void ClimbDownFromStanding_EntryFrame_IsNotCancelledByLostOverlap()
    {
        // On entry the body still has no overlap (we just entered). The lostOverlap guard
        // must not fire on the entry frame — _enteredClimbThisFrame prevents it.
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 80f);
        entity.LastReposition = new Vector2(0f, 5f);
        var platformer = MakePlatformer(body, new AxisInput(y: -1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        entity.VelocityY.ShouldBe(-100f); // ClimbingSpeed=100, inputY=-1
    }

    [Fact]
    public void ClimbDownFromStanding_NoLadderBelow_DoesNotEnter()
    {
        var ladder = LadderColumn(cellCenterX: 200f, bottomY: 0f, heightCells: 5); // far away
        var (entity, body) = MakeEntity(x: 100f, y: 80f);
        entity.LastReposition = new Vector2(0f, 5f);
        var platformer = MakePlatformer(body, new AxisInput(y: -1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeFalse();
    }

    [Fact]
    public void ClimbDownFromStanding_Airborne_DoesNotEnter()
    {
        // Not grounded → climb-down-from-standing must not trigger.
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 80f);
        // LastReposition default is zero → IsOnGround = false
        var platformer = MakePlatformer(body, new AxisInput(y: -1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeFalse();
    }

    [Fact]
    public void ClimbDownFromStanding_PressDown_SecondFrameStillClimbing()
    {
        // After entry the player descends; second frame must still be climbing even though
        // the body only slightly overlaps the top ladder cell.
        var ladder = LadderColumn(cellCenterX: 100f, bottomY: 0f, heightCells: 5);
        var (entity, body) = MakeEntity(x: 100f, y: 80f);
        entity.LastReposition = new Vector2(0f, 5f);
        var platformer = MakePlatformer(body, new AxisInput(y: -1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // entry
        entity.LastReposition = Vector2.Zero; // no longer pushed up by ground
        entity.Y -= 2f;                       // simulate one step of descent into ladder

        platformer.Update(entity, Frame()); // second frame

        platformer.IsClimbing.ShouldBeTrue();
    }

    [Fact]
    public void WhileClimbing_PressDown_SuppressesOneWayCollision()
    {
        // Pressing down while climbing must suppress one-way collision so the player
        // can descend through jump-through (cloud) platforms on the ladder.
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter climbing
        platformer.MovementInput = new AxisInput(y: -1f);
        platformer.Update(entity, Frame()); // descend

        platformer.IsSuppressingOneWayCollision.ShouldBeTrue();
    }

    [Fact]
    public void WhileClimbing_PressUp_DoesNotSuppressOneWayCollision()
    {
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame());

        platformer.IsSuppressingOneWayCollision.ShouldBeFalse();
    }

    // ── Exits ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Exit_WhenLandingWhileDescending()
    {
        var ladder = LadderColumn(100f, 0f, 5);
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Ladders = ladder;

        platformer.Update(entity, Frame()); // enter going up
        platformer.IsClimbing.ShouldBeTrue();

        // Next frame: descending, land on ground.
        platformer.MovementInput = new AxisInput(y: -1f);
        entity.LastReposition = new Vector2(0f, 5f);
        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeFalse();
    }

    [Fact]
    public void Exit_WhenBodyLosesOverlap_HorizontalWalkOff()
    {
        // Realistic exit: on a fence (X free), the player walks horizontally off the edge of
        // the fence panel. Body no longer overlaps any fence cell → exit. Vertical overshoots
        // at the top do NOT exit (handled by the top-of-column clamp), so this scenario uses
        // a fence + sideways teleport rather than the old "teleport above ladder" trick.
        var fence = LadderColumn(100f, 0f, 5); // single-column fence; X is free on fences
        var (entity, body) = MakeEntity(x: 100f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(y: 1f));
        platformer.Fences = fence;

        platformer.Update(entity, Frame());
        platformer.IsClimbing.ShouldBeTrue();

        entity.X = 300f; // teleport sideways off the fence
        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeFalse();
    }

    // ── Fence 2D ──────────────────────────────────────────────────────────────

    [Fact]
    public void EnterFence_PreservesX_AndHorizontalInputMovesX()
    {
        var fence = LadderColumn(100f, 0f, 5); // same tile shape, used as a fence here
        var (entity, body) = MakeEntity(x: 97f, y: 8f);
        var platformer = MakePlatformer(body, new AxisInput(x: 1f, y: 1f));
        platformer.Fences = fence;

        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeTrue();
        platformer.IsOnFence.ShouldBeTrue();
        entity.X.ShouldBe(97f); // NOT snapped
        entity.VelocityX.ShouldBeGreaterThan(0f); // horizontal motion active
    }

    [Fact]
    public void DescendingPastFenceBottom_LosesOverlap_AndExits()
    {
        // Regression: with a too-generous body-row scan, descending past the fence bottom kept
        // the player in climbing state inside the gap. Body-tight scan must exit cleanly.
        var fence = LadderColumn(100f, 32f, 3); // fence rows y=32..80
        var (entity, body) = MakeEntity(x: 100f, y: 40f);
        var platformer = MakePlatformer(body, new AxisInput(y: -1f));
        platformer.Fences = fence;

        platformer.Update(entity, Frame()); // enter going down
        platformer.IsClimbing.ShouldBeTrue();

        entity.Y = 10f; // far below fence (no overlap anywhere in body span)
        platformer.Update(entity, Frame());

        platformer.IsClimbing.ShouldBeFalse();
    }

    // ── Safety ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithSurfaceButNoCollisionShape_Throws()
    {
        var ladder = LadderColumn(100f, 0f, 3);
        var platformer = new PlatformerBehavior { AirMovement = new PlatformerValues(), Ladders = ladder };
        var entity = new Entity();

        var ex = Should.Throw<InvalidOperationException>(() => platformer.Update(entity, Frame()));
        ex.Message.ShouldContain("CollisionShape");
    }

    // ── Mock input ────────────────────────────────────────────────────────────

    private sealed class AxisInput : I2DInput
    {
        public float X { get; }
        public float Y { get; }
        public AxisInput(float x = 0f, float y = 0f) { X = x; Y = y; }
    }
}
