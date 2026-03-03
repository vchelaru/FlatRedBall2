using System.Collections.Generic;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using KoalaPickleSample.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace KoalaPickleSample.Screens;

/// <summary>
/// The Great Koala vs Pickle Journey — main gameplay screen.
/// Set <see cref="LevelIndex"/> via the <c>MoveToScreen</c> configure callback before
/// <see cref="Screen.CustomInitialize"/> runs.
/// </summary>
public class GameScreen : Screen
{
    // ------------------------------------------------------------------ Level data

    private record LevelData(
        // Player spawn
        float PlayerX, float PlayerY,
        // Enemy position and config
        float EnemyX, float EnemyY,
        int EnemyHealth, float PaceRange, float PaceSpeed,
        float PaceTime, float HopTime, float ShootDelay, int BurstCount,
        // Platform layout: (x, y, width)
        (float x, float y, float width)[] Platforms
    );

    private static readonly LevelData[] Levels = new LevelData[]
    {
        // Level 1 — simple staircase, relaxed enemy
        new(
            PlayerX: -480f, PlayerY: -220f,
            EnemyX:   380f, EnemyY:   240f,
            EnemyHealth: 5, PaceRange: 40f, PaceSpeed: 55f,
            PaceTime: 2.6f, HopTime: 0.7f, ShootDelay: 0.30f, BurstCount: 2,
            Platforms: new[]
            {
                // Ground floor
                (-200f, -260f, 1200f),
                // Staircase steps going up-right
                (-320f, -160f, 180f),
                (-100f,  -60f, 180f),
                ( 120f,   40f, 180f),
                ( 340f,  140f, 200f),
                // Enemy platform
                ( 340f,  220f, 200f),
            }
        ),

        // Level 2 — mixed heights, slightly tougher enemy
        new(
            PlayerX: -480f, PlayerY: -220f,
            EnemyX:   320f, EnemyY:   180f,
            EnemyHealth: 6, PaceRange: 55f, PaceSpeed: 70f,
            PaceTime: 2.2f, HopTime: 0.65f, ShootDelay: 0.26f, BurstCount: 3,
            Platforms: new[]
            {
                // Ground
                (-200f, -260f, 1200f),
                // Mixed platform layout
                (-380f, -120f, 160f),
                (-160f,  -40f, 120f),
                (  60f, -100f, 140f),
                ( 240f,   20f, 160f),
                (  60f,  120f, 120f),
                ( 280f,  100f, 180f),
            }
        ),

        // Level 3 — tighter gaps, faster enemy
        new(
            PlayerX: -500f, PlayerY: -220f,
            EnemyX:   420f, EnemyY:   160f,
            EnemyHealth: 7, PaceRange: 60f, PaceSpeed: 85f,
            PaceTime: 1.9f, HopTime: 0.6f, ShootDelay: 0.22f, BurstCount: 3,
            Platforms: new[]
            {
                // Ground
                (-200f, -260f, 1200f),
                // More complex layout with narrower platforms
                (-440f, -140f, 120f),
                (-260f,  -60f, 100f),
                ( -80f, -140f, 120f),
                ( 100f,  -20f, 100f),
                ( 280f,  -80f, 100f),
                ( 100f,   80f, 120f),
                ( 320f,   60f, 160f),
                ( 380f,  140f, 180f),
            }
        ),

        // Level 4 — non-linear, backtracking required, aggressive enemy
        new(
            PlayerX: -500f, PlayerY: -220f,
            EnemyX:    60f, EnemyY:   260f,
            EnemyHealth: 8, PaceRange: 65f, PaceSpeed: 95f,
            PaceTime: 1.6f, HopTime: 0.55f, ShootDelay: 0.20f, BurstCount: 4,
            Platforms: new[]
            {
                // Ground
                (-200f, -260f, 1200f),
                // Zigzag pattern
                (-440f,  -80f, 130f),
                (-200f, -160f, 110f),
                (   0f,  -80f, 110f),
                (-180f,   40f, 130f),
                (  80f,   -0f, 110f),
                ( -60f,  120f, 120f),
                ( 160f,  140f, 130f),
                (  20f,  240f, 180f),
            }
        ),

        // Level 5 — hardest, narrow platforms, fast burst fire
        new(
            PlayerX: -500f, PlayerY: -220f,
            EnemyX:   300f, EnemyY:   280f,
            EnemyHealth: 10, PaceRange: 70f, PaceSpeed: 110f,
            PaceTime: 1.4f, HopTime: 0.5f, ShootDelay: 0.18f, BurstCount: 4,
            Platforms: new[]
            {
                // Ground
                (-200f, -260f, 1200f),
                // Demanding layout
                (-420f, -120f, 100f),
                (-240f,  -40f,  90f),
                ( -80f, -120f, 100f),
                (  80f,  -40f,  90f),
                (-120f,   80f, 100f),
                (  60f,   40f,  90f),
                ( 220f,   80f, 100f),
                ( 100f,  180f, 100f),
                ( 260f,  180f, 120f),
                ( 260f,  260f, 160f),
            }
        ),
    };

    // ------------------------------------------------------------------ Properties

    /// <summary>
    /// 0-based index of the level to play. Set via the <c>MoveToScreen</c> configure
    /// callback before <see cref="Screen.CustomInitialize"/> runs.
    /// </summary>
    public int LevelIndex { get; set; } = 0;

    // ------------------------------------------------------------------ Fields

    private Factory<Player>       _playerFactory       = null!;
    private Factory<Enemy>        _enemyFactory        = null!;
    private Factory<Platform>     _platformFactory     = null!;
    private Factory<PlayerBullet> _playerBulletFactory = null!;
    private Factory<EnemyBullet>  _enemyBulletFactory  = null!;

    private Player _player = null!;
    private Enemy  _enemy  = null!;

    // ------------------------------------------------------------------ Init

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(18, 18, 28);

        _playerFactory       = new Factory<Player>(this);
        _enemyFactory        = new Factory<Enemy>(this);
        _platformFactory     = new Factory<Platform>(this);
        _playerBulletFactory = new Factory<PlayerBullet>(this);
        _enemyBulletFactory  = new Factory<EnemyBullet>(this);

        var data = Levels[LevelIndex];

        SpawnPlatforms(data);
        SpawnPlayer(data);
        SpawnEnemy(data);
        SetupCollision();

        // Snap camera to the player so the lerp follow starts from the correct
        // position rather than flying in from (0, 0).
        Camera.X = _player.X;
        Camera.Y = _player.Y;
    }

    private void SpawnPlatforms(LevelData data)
    {
        foreach (var (px, py, width) in data.Platforms)
        {
            var platform = _platformFactory.Create();
            platform.X = px;
            platform.Y = py;
            platform.Rectangle.Width = width;
            platform.Rectangle.Height = 24f;
            platform.Rectangle.RepositionDirections = RepositionDirections.All;
        }
    }

    private void SpawnPlayer(LevelData data)
    {
        _player   = _playerFactory.Create();
        _player.X = data.PlayerX;
        _player.Y = data.PlayerY;
    }

    private void SpawnEnemy(LevelData data)
    {
        _enemy   = _enemyFactory.Create();
        _enemy.X = data.EnemyX;
        _enemy.Y = data.EnemyY;

        var d = data;
        _enemy.Configure(
            health:      d.EnemyHealth,
            paceRange:   d.PaceRange,
            paceSpeed:   d.PaceSpeed,
            paceTime:    d.PaceTime,
            hopTime:     d.HopTime,
            shootDelay:  d.ShootDelay,
            burstCount:  d.BurstCount
        );

        _enemy.SetPlayer(_player);
    }

    private void SetupCollision()
    {
        // Player vs platforms — solid platformer collision
        AddCollisionRelationship<Player, Platform>(_playerFactory, _platformFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);

        // Player bullets vs enemy — bullet destroyed, enemy takes damage
        AddCollisionRelationship<PlayerBullet, Enemy>(_playerBulletFactory, _enemyFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                bullet.Destroy();
                enemy.TakeHit();
            };

        // Enemy bullets vs player — bullet destroyed, player takes damage
        // NOTE: Enemy bullets do NOT collide with platforms (they pass through).
        AddCollisionRelationship<EnemyBullet, Player>(_enemyBulletFactory, _playerFactory)
            .CollisionOccurred += (bullet, player) =>
            {
                bullet.Destroy();
                player.TakeHit();
            };

        // Player bullets vs platforms — bullet destroyed on hitting a wall/floor
        AddCollisionRelationship<PlayerBullet, Platform>(_playerBulletFactory, _platformFactory)
            .CollisionOccurred += (bullet, _) =>
            {
                bullet.Destroy();
            };
    }

    // ------------------------------------------------------------------ Activity

    public override void CustomActivity(FrameTime time)
    {
        // Smooth camera follow: lerp toward the player position each frame
        const float CameraLerpFactor = 6f;
        float t = MathF.Min(1f, CameraLerpFactor * time.DeltaSeconds);
        Camera.X += (_player.X - Camera.X) * t;
        Camera.Y += (_player.Y - Camera.Y) * t;

        // Win condition: enemy dead
        if (_enemy.DiedThisFrame)
        {
            int nextLevel = LevelIndex + 1;
            if (nextLevel >= Levels.Length)
                MoveToScreen<WinScreen>();
            else
                MoveToScreen<LevelAnnounceScreen>(s => s.LevelIndex = nextLevel);
            return;
        }

        // Lose condition: player dead — restart the same level
        if (_player.DiedThisFrame)
            MoveToScreen<GameScreen>(s => s.LevelIndex = LevelIndex);
    }

    public override void CustomDestroy()
    {
        // Factories and all their entities are destroyed automatically.
    }
}
