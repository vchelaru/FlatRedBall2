using System.Numerics;
using FlatRedBall2;

namespace ShmupSample.Screens;

/// <summary>
/// Manages wave-based enemy spawning. Fodder enemies follow predefined waypoint paths
/// and are drip-spawned one after another so they trail each other along the same route.
/// </summary>
internal class WaveSpawner
{
    private readonly GameplayScreen _screen;
    private float _waveTimer;
    private int _waveIndex;
    private const float WaveBreathDuration = 4.0f;

    // Drip-spawn queue: fodder enemies are added here and released one at a time
    private readonly Queue<(Vector2[] waypoints, float speed)> _fodderQueue = new();
    private float _fodderSpawnCooldown;
    private const float FodderSpawnInterval = 0.35f;

    // ── Path definitions (world-space, Y-up, screen ~1280×720) ──────────────
    //
    //  Straight columns: enter top, exit bottom
    private static readonly Vector2[] PathStraightL =
        { new(-220, 420), new(-220, -420) };
    private static readonly Vector2[] PathStraightC =
        { new(0, 420), new(0, -420) };
    private static readonly Vector2[] PathStraightR =
        { new(220, 420), new(220, -420) };

    //  Big loop from left side: enter left mid-screen, arc over the top, sweep
    //  down through center, exit off left-bottom
    private static readonly Vector2[] PathLoopFromLeft =
    {
        new(-700,  100),
        new(-350,  290),
        new(  50,  310),
        new( 360,  190),
        new( 310,  -60),
        new(   0, -260),
        new(-700, -380),
    };

    //  Mirror of the above, entering from the right
    private static readonly Vector2[] PathLoopFromRight =
    {
        new( 700,  100),
        new( 350,  290),
        new( -50,  310),
        new(-360,  190),
        new(-310,  -60),
        new(   0, -260),
        new( 700, -380),
    };

    //  Enter from left, sweep right across the upper half, then turn and dive off bottom-right
    private static readonly Vector2[] PathSweepFromLeft =
    {
        new(-700, 160),
        new(-200, 200),
        new( 250, 160),
        new( 460,  20),
        new( 520, -420),
    };

    //  Mirror: enter from right, sweep left, dive off bottom-left
    private static readonly Vector2[] PathSweepFromRight =
    {
        new( 700, 160),
        new( 200, 200),
        new(-250, 160),
        new(-460,  20),
        new(-520, -420),
    };

    public WaveSpawner(GameplayScreen screen)
    {
        _screen = screen;
        _waveTimer = 1.0f; // brief pause before first wave
    }

    public void Update(FrameTime time)
    {
        // Release one queued fodder enemy per interval
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

        // Trigger next wave after breath
        _waveTimer -= time.DeltaSeconds;
        if (_waveTimer <= 0f)
        {
            LaunchWave(_waveIndex % 6);
            _waveIndex++;
            _waveTimer = WaveBreathDuration;
        }
    }

    // Adds 'count' fodder enemies to the drip queue, all following the same path.
    // They will be spawned FodderSpawnInterval seconds apart, trailing each other.
    private void QueueFodder(Vector2[] path, float speed, int count)
    {
        for (int i = 0; i < count; i++)
            _fodderQueue.Enqueue((path, speed));
    }

    private void LaunchWave(int index)
    {
        switch (index)
        {
            case 0:
                // Straight column down the center
                QueueFodder(PathStraightC, 150f, 7);
                break;

            case 1:
                // Big loop entering from the left
                QueueFodder(PathLoopFromLeft, 175f, 6);
                break;

            case 2:
                // Big loop entering from the right + two flanking shooters
                QueueFodder(PathLoopFromRight, 175f, 6);
                _screen.SpawnShooter(-450f, 420f, -120f, 180f);
                _screen.SpawnShooter( 450f, 420f, -120f, 180f);
                break;

            case 3:
                // Sweep from the left side, heavy comes straight down center
                QueueFodder(PathSweepFromLeft, 165f, 6);
                _screen.SpawnHeavy(0f, 420f, -55f);
                break;

            case 4:
                // Sweep from right + shooters holding center
                QueueFodder(PathSweepFromRight, 165f, 6);
                _screen.SpawnShooter(-200f, 420f, -130f, 200f);
                _screen.SpawnShooter( 200f, 420f, -130f, 200f);
                break;

            case 5:
                // Simultaneous loops from both sides — interleaved so they drip in together.
                // Also two straight columns on the outer flanks for pressure.
                for (int i = 0; i < 4; i++)
                {
                    _fodderQueue.Enqueue((PathLoopFromLeft,  190f));
                    _fodderQueue.Enqueue((PathLoopFromRight, 190f));
                }
                QueueFodder(PathStraightL, 155f, 3);
                QueueFodder(PathStraightR, 155f, 3);
                break;
        }
    }
}
