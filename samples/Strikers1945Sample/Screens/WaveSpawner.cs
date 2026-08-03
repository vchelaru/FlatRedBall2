using System.Numerics;
using FlatRedBall2;

namespace Strikers1945Sample.Screens;

/// <summary>
/// Wave-based enemy spawning for Strikers 1945.
/// Faster pacing than ShmupSample — overlapping waves with shorter breaths.
/// Paths are sized for a 480x720 portrait screen.
/// Enemy variety is restricted in early levels — see LaunchWave for gating logic.
/// </summary>
internal class WaveSpawner
{
    private readonly GameplayScreen _screen;
    private readonly LevelDefinition _levelDef;
    private readonly int _extraFodderPerWave;
    private float _waveTimer;
    private int _waveIndex;

    public int WavesLaunched => _waveIndex;

    private readonly Queue<(Vector2[] waypoints, float speed)> _fodderQueue = new();
    private float _fodderSpawnCooldown;
    private const float FodderSpawnInterval = 0.28f; // slightly faster drip

    // Paths for 480x720 portrait screen (Y+ up, origin center)
    // halfW = 240, halfH = 360

    // Straight columns
    private static readonly Vector2[] PathStraightL =
        { new(-100, 400), new(-100, -400) };
    private static readonly Vector2[] PathStraightC =
        { new(0, 400), new(0, -400) };
    private static readonly Vector2[] PathStraightR =
        { new(100, 400), new(100, -400) };

    // Loop from left: enter left, arc over top-center, sweep down, exit left
    private static readonly Vector2[] PathLoopFromLeft =
    {
        new(-280, 100),
        new(-140, 250),
        new(  40, 280),
        new( 160, 180),
        new( 120, -20),
        new(   0, -200),
        new(-280, -350),
    };

    // Loop from right (mirror)
    private static readonly Vector2[] PathLoopFromRight =
    {
        new( 280, 100),
        new( 140, 250),
        new( -40, 280),
        new(-160, 180),
        new(-120, -20),
        new(   0, -200),
        new( 280, -350),
    };

    // Sweep from left across screen
    private static readonly Vector2[] PathSweepFromLeft =
    {
        new(-280, 160),
        new(-100, 200),
        new( 100, 160),
        new( 200,  20),
        new( 250, -400),
    };

    // Sweep from right
    private static readonly Vector2[] PathSweepFromRight =
    {
        new( 280, 160),
        new( 100, 200),
        new(-100, 160),
        new(-200,  20),
        new(-250, -400),
    };

    // V-formation diving center
    private static readonly Vector2[] PathVDiveLeft =
    {
        new(-160, 400),
        new(-80, 200),
        new(0, 50),
        new(-60, -200),
        new(-160, -400),
    };

    private static readonly Vector2[] PathVDiveRight =
    {
        new(160, 400),
        new(80, 200),
        new(0, 50),
        new(60, -200),
        new(160, -400),
    };

    public WaveSpawner(GameplayScreen screen, LevelDefinition levelDef)
    {
        _screen = screen;
        _levelDef = levelDef;
        _extraFodderPerWave = Math.Max(0, levelDef.LevelNumber - 1); // L1=0, L2=1, L3=2, L4=3, L5=4
        _waveTimer = 1.5f; // brief pause before first wave
    }

    public void Update(FrameTime time)
    {
        // Drip-spawn queued fodder
        if (_fodderQueue.Count > 0)
        {
            _fodderSpawnCooldown -= time.DeltaSeconds;
            if (_fodderSpawnCooldown <= 0f)
            {
                var (waypoints, speed) = _fodderQueue.Dequeue();
                _screen.SpawnFodderOnPath(waypoints, speed);
                _fodderSpawnCooldown = FodderSpawnInterval;
            }
        }

        // Next wave
        _waveTimer -= time.DeltaSeconds;
        if (_waveTimer <= 0f)
        {
            LaunchWave(_waveIndex % 8);
            _waveIndex++;
            _waveTimer = _levelDef.WaveBreathDuration;
        }
    }

    private void QueueFodder(Vector2[] path, float speed, int count)
    {
        for (int i = 0; i < count; i++)
            _fodderQueue.Enqueue((path, speed));
    }

    /// <summary>
    /// Spawn a V-formation of fodder along a path. Each enemy maintains its X offset.
    /// </summary>
    private void SpawnFormation(Vector2[] path, float speed, int count, float spacing)
    {
        for (int i = 0; i < count; i++)
        {
            // Alternate left/right offsets to create a V shape
            float offset = (i / 2 + 1) * spacing * (i % 2 == 0 ? -1f : 1f);
            if (i == 0) offset = 0f; // leader in center
            _screen.SpawnFodderFormation(path, speed, offset);
        }
    }

    /// <summary>
    /// Whether shooters are allowed at the current wave index.
    /// Level 1: shooters only after wave 15. Level 2+: always allowed.
    /// </summary>
    private bool CanSpawnShooters => _levelDef.LevelNumber >= 2 || _waveIndex >= 15;

    /// <summary>
    /// Whether heavy enemies are allowed at the current wave index.
    /// Level 2: heavies only after wave 15. Level 3+: follows HasHeavyEnemies flag.
    /// </summary>
    private bool CanSpawnHeavies => _levelDef.HasHeavyEnemies &&
        (_levelDef.LevelNumber >= 3 || _waveIndex >= 15);

    /// <summary>
    /// Turrets appear in levels 3+ after wave 20.
    /// </summary>
    private bool CanSpawnTurrets => _levelDef.LevelNumber >= 3 && _waveIndex >= 20;

    private static readonly Vector2[][] ExtraPaths =
        { PathStraightL, PathStraightC, PathStraightR };

    private void LaunchWave(int index)
    {
        switch (index)
        {
            case 0: // Straight center column
                QueueFodder(PathStraightC, 180f, 6);
                break;

            case 1: // Loop from left
                QueueFodder(PathLoopFromLeft, 200f, 5);
                break;

            case 2: // Loop from right + flanking shooters (if allowed)
                QueueFodder(PathLoopFromRight, 200f, 5);
                if (CanSpawnShooters)
                {
                    _screen.SpawnShooter(-160f, 400f, -140f, 160f);
                    _screen.SpawnShooter( 160f, 400f, -140f, 160f);
                }
                break;

            case 3: // Sweep from left + heavy center (if allowed) + turrets
                QueueFodder(PathSweepFromLeft, 190f, 6);
                if (CanSpawnHeavies)
                    _screen.SpawnHeavy(0f, 400f);
                if (CanSpawnTurrets)
                {
                    _screen.SpawnTurret(-160f, 280f);
                    _screen.SpawnTurret( 160f, 280f);
                }
                break;

            case 4: // V-formation dive + center shooter (if allowed)
                SpawnFormation(PathStraightC, 200f, 5, 30f);
                QueueFodder(PathVDiveLeft, 200f, 3);
                QueueFodder(PathVDiveRight, 200f, 3);
                if (CanSpawnShooters)
                    _screen.SpawnShooter(0f, 400f, -140f, 200f);
                break;

            case 5: // Sweep from right + dive bombers (if allowed)
                QueueFodder(PathSweepFromRight, 190f, 6);
                if (_levelDef.HasDiveBombers)
                {
                    _screen.SpawnDiveBomber(-120f, 400f);
                    _screen.SpawnDiveBomber( 120f, 400f);
                }
                break;

            case 6: // Double loops — interleaved from both sides
                for (int i = 0; i < 4; i++)
                {
                    _fodderQueue.Enqueue((PathLoopFromLeft, 210f));
                    _fodderQueue.Enqueue((PathLoopFromRight, 210f));
                }
                break;

            case 7: // All three straight columns + shooters (if allowed) + turret
                QueueFodder(PathStraightL, 170f, 4);
                QueueFodder(PathStraightC, 170f, 4);
                QueueFodder(PathStraightR, 170f, 4);
                if (CanSpawnShooters)
                {
                    _screen.SpawnShooter(-180f, 400f, -120f, 180f);
                    _screen.SpawnShooter( 180f, 400f, -120f, 180f);
                }
                if (CanSpawnTurrets)
                    _screen.SpawnTurret(0f, 300f);
                break;
        }

        // Difficulty scaling: add extra fodder in later levels
        for (int i = 0; i < _extraFodderPerWave; i++)
        {
            var path = ExtraPaths[(_waveIndex + i) % ExtraPaths.Length];
            _fodderQueue.Enqueue((path, 190f));
        }
    }
}
