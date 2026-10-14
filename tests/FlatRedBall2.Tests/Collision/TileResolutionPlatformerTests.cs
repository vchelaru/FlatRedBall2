using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// A real PlatformerBehavior driven frame by frame against tile layouts, as a game would run it:
// PhysicsUpdate -> collision relationship (bounce, elasticity 0) -> PlatformerBehavior.Update.
// The player is 12 wide by 28 tall with its origin at its feet (the usual platformer setup), moves at
// 2 units per frame, and can jump about 40 units (2.5 tiles).
public class TileResolutionPlatformerTests
{
    private const float Tol = 1e-3f;
    private const float Dt = 1f / 60f;
    private const float HalfWidth = 6f;
    private const float Height = 28f;
    private const float SpeedPerFrame = 2f;

    // ── Test rig ─────────────────────────────────────────────────────────────

    private sealed class Axis : I2DInput
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    private sealed class Button : IPressableInput
    {
        public bool IsDown { get; set; }
        public bool WasJustPressed { get; set; }
        public bool WasJustReleased { get; set; }
    }

    private sealed class Player : Entity, IPlatformerEntity
    {
        public PlatformerBehavior Behavior { get; } = new();
        public PlatformerBehavior Platformer => Behavior;
    }

    private sealed class Rig
    {
        public Player Player { get; } = new();
        public Axis Move { get; } = new();
        public Button Jump { get; } = new();
        private readonly CollisionRelationship<Player, TileShapes> _rel;
        private float _time;
        public int Frames { get; private set; }
        public float PeakY { get; private set; } = float.MinValue;
        public float LowestY { get; private set; } = float.MaxValue;

        public float BodyHalfWidth { get; }

        public Rig(TileShapes tiles, float feetX, float feetY, float bodyHalfWidth = HalfWidth, bool platformerFloorSlopes = false)
        {
            BodyHalfWidth = bodyHalfWidth;
            var body = new AARect { Width = bodyHalfWidth * 2f, Height = Height, Y = Height / 2f };
            Player.Add(body);
            Player.X = feetX;
            Player.Y = feetY;

            var ground = new PlatformerValues
            {
                MaxSpeedX = SpeedPerFrame / Dt,
                AccelerationTimeX = TimeSpan.Zero,
                DecelerationTimeX = TimeSpan.Zero,
                Gravity = 900f,
                MaxFallSpeed = 600f,
                SlopeSnapDistance = platformerFloorSlopes ? 8f : 0f,
                // Constant speed on slopes so per-frame travel can be asserted.
                UphillStopSpeedSlope = 0f,
                DownhillMaxSpeedMultiplier = 1f,
            };
            ground.SetJumpHeights(40f);
            Player.Behavior.AirMovement = ground;
            Player.Behavior.GroundMovement = ground;
            Player.Behavior.MovementInput = Move;
            Player.Behavior.JumpInput = Jump;
            Player.Behavior.CollisionShape = body;

            _rel = new CollisionRelationship<Player, TileShapes>(new[] { Player }, new[] { tiles });
            if (platformerFloorSlopes) _rel.SlopeMode = SlopeCollisionMode.PlatformerFloor;
            _rel.BounceFirstOnCollision(elasticity: 0f);
        }

        public void Step()
        {
            _time += Dt;
            Frames++;
            var frame = new FrameTime(TimeSpan.FromSeconds(Dt), TimeSpan.FromSeconds(Dt), TimeSpan.FromSeconds(_time), TimeSpan.FromSeconds(_time));
            Player.PhysicsUpdate(frame);
            _rel.RunCollisions();
            Player.Behavior.Update(Player, frame);
            Jump.WasJustPressed = false;
            Jump.WasJustReleased = false;
            PeakY = MathF.Max(PeakY, Player.Y);
            LowestY = MathF.Min(LowestY, Player.Y);
        }

        // Starts walking: the first frame after changing the input still integrates last frame's velocity, so
        // run one frame before measuring per-frame travel.
        public void Walk(float direction)
        {
            Move.X = direction;
            Step();
        }

        public void Step(int frames)
        {
            for (int i = 0; i < frames; i++) Step();
        }

        public void PressJump(int holdFrames = 2)
        {
            Jump.IsDown = true;
            Jump.WasJustPressed = true;
            Step();
            for (int i = 1; i < holdFrames; i++) Step();
            Jump.IsDown = false;
            Jump.WasJustReleased = true;
            Step();
        }

        public bool OnGround => Player.Behavior.IsOnGround;
        public float X => Player.X;
        public float Y => Player.Y;
    }

    // True when the player's 12x28 body overlaps a tile by more than the harness tolerance.
    private static bool Overlaps(Rig rig, TileShapes tiles) =>
        TileWorld.Penetrates(ActorSpec.Rect(rig.BodyHalfWidth, Height / 2f), rig.X, rig.Y + Height / 2f, tiles);

    private static Rig Settled(TileShapes tiles, float feetX, float feetY)
    {
        var rig = new Rig(tiles, feetX, feetY);
        rig.Step(5);
        return rig;
    }

    private static string Row(int width, char c) => new string(c, width);

    // Appends `extra` columns to every row so a map has room to walk: '.' for air rows, '#' for the last (floor) row.
    private static string[] Extend(string[] rows, int extra) =>
        rows.Select((r, i) => r + Row(extra, i == rows.Length - 1 ? '#' : '.')).ToArray();

    // A floor with a ledge `tiles` high starting at column 4, three rows of headroom above it, 40 columns wide.
    private static string[] LedgeMap(int tiles)
    {
        var rows = new List<string> { Row(40, '.'), Row(40, '.'), Row(40, '.') };
        for (int i = 0; i < tiles; i++) rows.Add(Row(4, '.') + Row(36, '#'));
        rows.Add(Row(40, '#'));
        return rows.ToArray();
    }

    // ── Standing and walking on long floors ──────────────────────────────────────

    [Fact]
    public void Floor_PlayerStandingStill_StaysOnTheGroundForManyFrames()
    {
        var tiles = TileWorld.Map(Row(30, '.'), Row(30, '#'));
        var rig = Settled(tiles, 100f, 16f);

        for (int i = 0; i < 300; i++)
        {
            rig.Step();
            rig.OnGround.ShouldBeTrue($"frame {i}");
            rig.Y.ShouldBe(16f, Tol, $"frame {i}");
            rig.X.ShouldBe(100f, Tol, $"frame {i}");
        }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void Floor_WalkingAcrossManyTileSeams_NeverSnagsAndStaysGrounded(float direction)
    {
        var tiles = TileWorld.Map(Row(40, '.'), Row(40, '#'));
        var rig = Settled(tiles, direction > 0 ? 40f : 600f, 16f);
        rig.Walk(direction);
        float previousX = rig.X;

        for (int i = 0; i < 250; i++)
        {
            rig.Step();

            rig.OnGround.ShouldBeTrue($"frame {i}");
            rig.Y.ShouldBe(16f, Tol, $"frame {i}");
            (rig.X - previousX).ShouldBe(direction * SpeedPerFrame, 0.01f, $"frame {i}");
            previousX = rig.X;
        }
    }

    [Fact]
    public void Floor_WalkingOnAThickFloor_NeverSnagsAndStaysGrounded()
    {
        var tiles = TileWorld.Map(Row(40, '.'), Row(40, '#'), Row(40, '#'), Row(40, '#'));
        var rig = Settled(tiles, 40f, 48f);
        rig.Walk(1f);
        float previousX = rig.X;

        for (int i = 0; i < 250; i++)
        {
            rig.Step();

            rig.OnGround.ShouldBeTrue($"frame {i}");
            rig.Y.ShouldBe(48f, Tol, $"frame {i}");
            (rig.X - previousX).ShouldBe(SpeedPerFrame, 0.01f, $"frame {i}");
            previousX = rig.X;
        }
    }

    // ── Walls ─────────────────────────────────────────────────────────────────

    // A 4-tile wall on the floor, player walking into it for a long time: stops at the face, stays grounded, no jitter.
    [Fact]
    public void Wall_WalkingIntoIt_StopsAtTheFaceAndStaysStill()
    {
        var tiles = TileWorld.Map("....#...", "....#...", "....#...", "....#...", "########");
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;

        rig.Step(120);
        float stoppedX = rig.X;

        stoppedX.ShouldBe(64f - HalfWidth, Tol);
        for (int i = 0; i < 60; i++)
        {
            rig.Step();
            rig.X.ShouldBe(stoppedX, Tol, $"frame {i}");
            rig.Y.ShouldBe(16f, Tol, $"frame {i}");
            rig.OnGround.ShouldBeTrue($"frame {i}");
        }
    }

    [Fact]
    public void Wall_JumpingWhilePressedAgainstIt_RisesStraightUpAlongTheFaceAndLandsBack()
    {
        var tiles = TileWorld.Map("....#...", "....#...", "....#...", "....#...", "........", "########");
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;
        rig.Step(60);
        float restX = rig.X;

        rig.PressJump();
        for (int i = 0; i < 90; i++)
        {
            rig.Step();
            rig.X.ShouldBe(restX, Tol, $"frame {i}");
            Overlaps(rig, tiles).ShouldBeFalse($"frame {i}");
        }

        (rig.PeakY - 16f).ShouldBeGreaterThan(30f, "the jump rises along the face");
        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
    }

    // ── Ceilings ───────────────────────────────────────────────────────────────

    // Floor top y=16, ceiling bottom y=64 (48 up). A 28-tall player's head reaches the ceiling with feet at y=36.
    [Fact]
    public void Ceiling_JumpingIntoIt_StopsAtTheCeilingThenFallsBackToTheFloor()
    {
        // Ceiling y 64..80, air y 16..64, floor y 0..16.
        var tiles = TileWorld.Map(Row(12, '#'), Row(12, '.'), Row(12, '.'), Row(12, '.'), Row(12, '#'));
        var rig = Settled(tiles, 96f, 16f);

        rig.PressJump();
        for (int i = 0; i < 120; i++)
        {
            rig.Step();
            Overlaps(rig, tiles).ShouldBeFalse($"frame {i}");
            rig.X.ShouldBe(96f, Tol);
        }

        rig.PeakY.ShouldBe(64f - Height, Tol); // head flush against the ceiling
        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
    }

    // Jumping up beside a ceiling block's edge. Whatever the overlap, the player must never end up inside a tile,
    // must come back to the floor, and must never be thrown more than the overlap sideways.
    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(4f)]
    [InlineData(6f)]
    [InlineData(10f)]
    public void CeilingCorner_JumpingUnderTheEdgeOfABlock_NeverEntersItAndLandsOnTheFloor(float overlap)
    {
        // Ceiling block spans x 96..128 at y 64..80 (two tiles); the player's right side overlaps its left edge by `overlap`.
        var tiles = TileWorld.Map("..............", "......##......", "..............", "..............", "..............", "##############");
        float startX = 96f - HalfWidth + overlap;
        var rig = Settled(tiles, startX, 16f);

        rig.PressJump();
        for (int i = 0; i < 150; i++)
        {
            rig.Step();
            Overlaps(rig, tiles).ShouldBeFalse($"overlap {overlap} frame {i}");
        }

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
        MathF.Abs(rig.X - startX).ShouldBeLessThanOrEqualTo(overlap + Tol);
    }

    // ── Ledges and stairs ────────────────────────────────────────────────────────

    [Fact]
    public void Ledge_WalkingIntoAOneTileLedge_StopsAtTheFaceWithoutClimbingIt()
    {
        var tiles = TileWorld.Map(LedgeMap(1));
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;

        rig.Step(120);

        rig.X.ShouldBe(64f - HalfWidth, Tol);
        rig.Y.ShouldBe(16f, Tol);
    }

    [Fact]
    public void Ledge_JumpingOntoAOneTileLedge_LandsOnTopAndKeepsWalkingWithoutSnagging()
    {
        var tiles = TileWorld.Map(LedgeMap(1));
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;
        rig.Step(60); // reach the face
        rig.PressJump();
        rig.Step(60);

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(32f, Tol, "standing on the ledge top");
        rig.X.ShouldBeGreaterThan(64f);

        float previousX = rig.X;
        for (int i = 0; i < 120; i++)
        {
            rig.Step();
            rig.Y.ShouldBe(32f, Tol, $"frame {i}");
            (rig.X - previousX).ShouldBe(SpeedPerFrame, 0.01f, $"frame {i}");
            previousX = rig.X;
        }
    }

    [Fact]
    public void Ledge_JumpingOntoATwoTileLedge_LandsOnTop()
    {
        var tiles = TileWorld.Map(LedgeMap(2));
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;
        rig.Step(60);

        rig.PressJump();
        rig.Step(90);

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(48f, Tol);
        rig.X.ShouldBeGreaterThan(64f);
    }

    [Fact]
    public void Ledge_ATallerWallThanTheJumpCanClear_LeavesThePlayerAtItsBase()
    {
        var tiles = TileWorld.Map(LedgeMap(3));
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;
        rig.Step(60);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            rig.PressJump();
            rig.Step(60);
        }

        rig.Y.ShouldBe(16f, Tol);
        rig.X.ShouldBe(64f - HalfWidth, Tol);
    }

    // Four steps up, jumping at each riser while holding right.
    [Fact]
    public void Stairs_ClimbingByJumpingAtEveryStep_ReachesTheTopAndNeverOverlapsATile()
    {
        var tiles = TileWorld.Map(Extend(new[]
        {
            "............#####",
            "..........#######",
            "........#########",
            "......###########",
            "#################",
        }, 30));
        var rig = Settled(tiles, 20f, 16f);
        rig.Move.X = 1f;
        float topY = 16f + 4f * 16f;

        for (int frame = 0; frame < 600 && !(rig.OnGround && rig.Y >= topY - Tol); frame++)
        {
            if (rig.OnGround) rig.PressJump();
            else rig.Step();
            Overlaps(rig, tiles).ShouldBeFalse($"frame {frame}");
        }

        rig.Y.ShouldBe(topY, Tol);
        rig.OnGround.ShouldBeTrue();
        rig.X.ShouldBeGreaterThan(12f * 16f);
    }

    [Fact]
    public void Stairs_WalkingDownThem_FallsOneStepAtATimeAndEndsOnTheFloorWithoutOverlap()
    {
        var tiles = TileWorld.Map(Extend(new[]
        {
            "#####............",
            "#######..........",
            "#########........",
            "###########......",
            "#################",
        }, 30));
        var rig = Settled(tiles, 40f, 16f + 4f * 16f);
        rig.Move.X = 1f;

        for (int frame = 0; frame < 300; frame++)
        {
            rig.Step();
            Overlaps(rig, tiles).ShouldBeFalse($"frame {frame}");
        }

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
    }

    // ── Running off a ledge ──────────────────────────────────────────────────────

    // Platform top y=48 for x in 0..96, floor top y=16 beyond. The player must keep a constant 2 units per frame
    // all the way off the edge, then land on the lower floor.
    [Fact]
    public void LedgeEdge_RunningOffThePlatform_KeepsConstantHorizontalSpeedAndLandsBelow()
    {
        var tiles = TileWorld.Map(Extend(new[]
        {
            "......................",
            "......................",
            "######................",
            "######................",
            "######################",
        }, 30));
        var rig = Settled(tiles, 40f, 48f);
        rig.Walk(1f);
        float previousX = rig.X;
        bool leftGround = false;

        for (int i = 0; i < 100; i++)
        {
            rig.Step();
            (rig.X - previousX).ShouldBe(SpeedPerFrame, 0.01f, $"frame {i}");
            previousX = rig.X;
            if (!rig.OnGround) leftGround = true;
            Overlaps(rig, tiles).ShouldBeFalse($"frame {i}");
        }

        leftGround.ShouldBeTrue();
        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
    }

    // ── Pits and gaps ───────────────────────────────────────────────────────────

    // Pit occupies cols [4, 4+pitTiles) (x 64.. for one tile), its floor top is y=16 and the lips' tops are y=48.
    private static TileShapes PitMap(int pitTiles, int totalCols)
    {
        string lip(int row) => string.Concat(Enumerable.Range(0, totalCols).Select(c => c >= 4 && c < 4 + pitTiles ? '.' : '#'));
        string air = Row(totalCols, '.');
        return TileWorld.Map(air, air, air, lip(2), lip(1), Row(totalCols, '#'));
    }

    [Fact]
    public void Pit_FallingStraightIntoANarrowPit_LandsOnItsFloorWithoutSidewaysMovement()
    {
        var tiles = PitMap(1, 12); // pit x 64..80, floor top y=16, lips top y=48
        var rig = new Rig(tiles, 72f, 100f);

        rig.Step(120);

        rig.OnGround.ShouldBeTrue();
        rig.X.ShouldBe(72f, Tol);
        rig.Y.ShouldBe(16f, Tol);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(1f)]
    public void Pit_FallingIntoANarrowPitWhileMovingTowardAWall_EndsPressedAgainstThatWallOnTheFloor(float direction)
    {
        var tiles = PitMap(1, 12);
        var rig = new Rig(tiles, 72f, 30f); // already inside the pit (lips are at y=48)
        rig.Move.X = direction;

        rig.Step(150);

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
        rig.X.ShouldBe(direction > 0 ? 80f - HalfWidth : 64f + HalfWidth, Tol);
    }

    [Fact]
    public void Pit_JumpingOutOfANarrowPitTowardTheRight_LandsOnTheLip()
    {
        var tiles = PitMap(1, 12);
        var rig = Settled(tiles, 74f, 16f);
        rig.Move.X = 1f;

        for (int attempt = 0; attempt < 3 && !(rig.OnGround && rig.Y > 40f); attempt++)
        {
            rig.PressJump();
            rig.Step(40);
        }

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(48f, Tol);
        rig.X.ShouldBeGreaterThan(80f - HalfWidth);
    }

    // Platform top y=32 with one missing tile at col 5 (x 80..96); the gap's floor is one tile lower (top y=16).
    private static TileShapes OneTileGapMap() => TileWorld.Map(
        Row(30, '.'), Row(30, '.'), string.Concat(Enumerable.Range(0, 30).Select(c => c == 5 ? '.' : '#')), Row(30, '#'));

    // A body wider than the gap is never fully over it, so it walks across without dipping.
    [Fact]
    public void Gap_WalkingOverAOneTileGapWithABodyWiderThanIt_CrossesWithoutSinkingAndKeepsItsSpeed()
    {
        var tiles = OneTileGapMap();
        var rig = new Rig(tiles, 40f, 32f, bodyHalfWidth: 10f);
        rig.Step(5);
        rig.Walk(1f);
        float previousX = rig.X;

        for (int i = 0; i < 100; i++)
        {
            rig.Step();
            (rig.X - previousX).ShouldBe(SpeedPerFrame, 0.01f, $"frame {i}");
            previousX = rig.X;
            rig.Y.ShouldBe(32f, Tol, $"frame {i}");
        }

        rig.OnGround.ShouldBeTrue();
        rig.X.ShouldBeGreaterThan(96f);
    }

    // A 12-wide body is fully over the 16-wide gap for a few frames at 2 units per frame, starts to fall, and is then
    // held by the far wall of the gap, ending on the gap's floor.
    [Fact]
    public void Gap_WalkingOverAOneTileGapWithABodyNarrowerThanIt_FallsInAndEndsAgainstTheFarWallOnTheGapFloor()
    {
        var tiles = OneTileGapMap();
        var rig = Settled(tiles, 40f, 32f);
        rig.Walk(1f);

        rig.Step(60);

        rig.X.ShouldBe(96f - HalfWidth, Tol);
        rig.Y.ShouldBe(16f, Tol);
        rig.OnGround.ShouldBeTrue();
    }

    [Fact]
    public void Pit_WalkingIntoATwoTileWideGap_FallsIn()
    {
        // Platform top y=32 with a two-tile gap at cols 5-6 (x 80..112) above the floor (top y=16).
        var tiles = TileWorld.Map(Row(14, '.'), Row(14, '.'), string.Concat(Enumerable.Range(0, 14).Select(c => c == 5 || c == 6 ? '.' : '#')), Row(14, '#'));
        var rig = Settled(tiles, 40f, 32f);
        rig.Move.X = 1f;

        rig.Step(60);
        rig.Move.X = 0f;
        rig.Step(120);

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol, "ended on the gap's floor, one tile below the platform");
        Overlaps(rig, tiles).ShouldBeFalse();
    }

    // Gap between two pillars (3 tiles wide): falling between them lands on the floor between.
    [Fact]
    public void Pillars_FallingBetweenTwo_LandsOnTheFloorBetweenThem()
    {
        var tiles = TileWorld.Map(
            "#...#",
            "#...#",
            "#...#",
            "#####");
        var rig = new Rig(tiles, 40f, 200f);

        rig.Step(120);

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
        rig.X.ShouldBe(40f, Tol);
    }

    // ── Landing on a single block ─────────────────────────────────────────────

    // A one-tile block on the floor at x 96..112 (top y=32). The player falls from 48 above its top, at a range of
    // horizontal offsets. A landing with a large horizontal overlap must stay on top; one that only clips the edge
    // may be shoved off; in every case the player ends grounded, outside the block, shifted by at most its overlap.
    [Theory]
    [InlineData(-12f)]
    [InlineData(-9f)]
    [InlineData(-6f)]
    [InlineData(-3f)]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(3f)]
    [InlineData(6f)]
    [InlineData(9f)]
    [InlineData(12f)]
    public void Block_FallingOntoTheEdgeOfOneBlock_EndsGroundedAndOutsideIt(float centerOffsetFromBlockCenter)
    {
        var tiles = TileWorld.Map(Row(14, '.'), Row(14, '.'), Row(14, '.'), "......#.......", Row(14, '#'));
        float startX = 104f + centerOffsetFromBlockCenter;
        var rig = new Rig(tiles, startX, 32f + 48f);

        for (int i = 0; i < 200; i++)
        {
            rig.Step();
            Overlaps(rig, tiles).ShouldBeFalse($"frame {i}");
        }

        rig.OnGround.ShouldBeTrue();
        bool onTop = MathF.Abs(rig.Y - 32f) < Tol;
        bool onFloor = MathF.Abs(rig.Y - 16f) < Tol;
        (onTop || onFloor).ShouldBeTrue($"ended at y={rig.Y}");
        if (MathF.Abs(centerOffsetFromBlockCenter) <= 6f) // overlap >= 8 of 12: well on the block
        {
            onTop.ShouldBeTrue();
            rig.X.ShouldBe(startX, Tol);
        }
    }

    // ── Coming down onto a floor at high speed ─────────────────────────────────

    // Terminal fall speed is 600/s = 10 per frame, below the 14-per-frame a one-tile floor can hold for this player.
    [Fact]
    public void Floor_FallingAtTerminalSpeedOntoAOneTileFloor_Lands()
    {
        var tiles = TileWorld.Map(Row(12, '.'), Row(12, '#'));
        var rig = new Rig(tiles, 96f, 1000f);

        rig.Step(400);

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
    }

    // Determinism: two identical runs through a busy scene agree exactly.
    [Fact]
    public void Scene_TwoIdenticalRuns_ProduceIdenticalTrajectories()
    {
        string[] map =
        {
            "............#####",
            "..........#######",
            "........#########",
            "......###########",
            "#################",
        };
        List<(float, float)> Run()
        {
            var tiles = TileWorld.Map(map);
            var rig = Settled(tiles, 20f, 16f);
            rig.Move.X = 1f;
            var path = new List<(float, float)>();
            for (int i = 0; i < 300; i++)
            {
                if (rig.OnGround && i % 40 == 0) rig.PressJump();
                else rig.Step();
                path.Add((rig.X, rig.Y));
            }
            return path;
        }

        Run().ShouldBe(Run());
    }

    // ── Slopes meeting rectangular tiles (PlatformerFloor mode) ──────────────────────

    // Floor top y=16 for cols 0-3, a right-ascending ramp tile at col 4 (rises 16 -> 32), upper floor top y=32 from col 5.
    private static TileShapes RampMap()
    {
        var tiles = new TileShapes { GridSize = 16f };
        for (int c = 0; c < 30; c++)
        {
            tiles.AddTileAtCell(c, 0);
            if (c >= 5) tiles.AddTileAtCell(c, 1);
        }
        tiles.AddPolygonTileAtCell(4, 1, Polygon.FromPoints(new[] { new Vector2(-8f, -8f), new Vector2(8f, -8f), new Vector2(8f, 8f) }));
        return tiles;
    }

    [Fact]
    public void Ramp_WalkingUpOntoARectangularUpperFloor_ReachesItWithoutStallingOrDipping()
    {
        var tiles = RampMap();
        var rig = new Rig(tiles, 20f, 16f, platformerFloorSlopes: true);
        rig.Step(5);
        rig.Walk(1f);
        float previousX = rig.X, previousY = rig.Y;

        for (int i = 0; i < 100; i++)
        {
            rig.Step();
            (rig.X - previousX).ShouldBe(SpeedPerFrame, 0.05f, $"frame {i}");
            rig.Y.ShouldBeGreaterThanOrEqualTo(previousY - 0.01f, $"frame {i}: dipped");
            previousX = rig.X;
            previousY = rig.Y;
        }

        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(32f, Tol);
    }

    [Fact]
    public void Ramp_WalkingDownFromARectangularUpperFloor_DropsOntoTheRampThenFollowsItToTheLowerFloor()
    {
        var tiles = RampMap();
        var rig = new Rig(tiles, 200f, 32f, platformerFloorSlopes: true);
        rig.Step(5);
        rig.Walk(-1f);
        float previousX = rig.X;
        int airborneFrames = 0;

        for (int i = 0; i < 100; i++)
        {
            rig.Step();
            (rig.X - previousX).ShouldBe(-SpeedPerFrame, 0.05f, $"frame {i}");
            previousX = rig.X;
            if (!rig.OnGround) airborneFrames++;
        }

        // Stepping from the flat upper floor onto the downward ramp is not snapped (snap needs the previous frame to be on
        // a slope), so the player free-falls onto the ramp; pinned as measured.
        airborneFrames.ShouldBe(11);
        rig.OnGround.ShouldBeTrue();
        rig.Y.ShouldBe(16f, Tol);
    }

    [Fact]
    public void Ramp_JumpingStraightUpFromTheUpperFloorBesideTheRampBase_LandsBackOnTheSameSpot()
    {
        var tiles = RampMap();
        var rig = new Rig(tiles, 120f, 32f, platformerFloorSlopes: true);
        rig.Step(5);

        rig.PressJump();
        rig.Step(80);

        rig.OnGround.ShouldBeTrue();
        rig.X.ShouldBe(120f, Tol);
        rig.Y.ShouldBe(32f, Tol);
    }
}
