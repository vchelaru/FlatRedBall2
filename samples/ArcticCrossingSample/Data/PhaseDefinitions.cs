using ArcticCrossingSample.Entities;
using Microsoft.Xna.Framework;

namespace ArcticCrossingSample.Data;

/// Hand-designed level layouts for all 5 phases.
public static class PhaseDefinitions
{
    public static LevelData GetPhase(int index) => index switch
    {
        1 => Phase1(),
        2 => Phase2(),
        3 => Phase3(),
        4 => Phase4(),
        5 => Phase5(),
        _ => Phase1(),
    };

    public static int TotalPhases => 5;

    /// Phase 1: The Departure — tutorial, easy jumps, large stable platforms
    private static LevelData Phase1()
    {
        return new LevelData(
            PhaseName: "The Departure",
            PhaseIndex: 1,
            BackgroundColor: new Color(40, 100, 160),
            PlayerStartX: -500f,
            PlayerStartY: -100f,
            DeathZoneY: -350f,
            LevelLeftBound: -600f,
            LevelRightBound: 1800f,
            LevelTopBound: 400f,
            Platforms:
            [
                // Starting shore — big stable ground
                new(-500f, -180f, 300f, 30f),
                new(-200f, -180f, 200f, 24f),

                // Easy jumps across ice
                new(50f, -160f, 140f, 22f),
                new(250f, -140f, 130f, 22f),
                new(430f, -150f, 150f, 22f),
                new(620f, -130f, 120f, 22f),

                // Slightly elevated section
                new(800f, -100f, 160f, 22f),
                new(1000f, -80f, 140f, 22f),

                // Gentle moving platform introduction
                new(1200f, -100f, 120f, 22f, PlatformType.Moving, MoveRangeX: 40f, MoveSpeed: 30f),

                // End platform
                new(1450f, -80f, 200f, 24f),
                new(1650f, -80f, 160f, 24f),
            ],
            Checkpoints:
            [
                new(430f, -128f, 0),
                new(1000f, -58f, 1),
            ],
            Npcs:
            [
                new(NpcKind.PolarBear, -500f, -140f, "Jump with Space! Arrow keys to move."),
                new(NpcKind.Penguin, 250f, -118f, WaddleRange: 50f),
                new(NpcKind.Penguin, 800f, -78f, WaddleRange: 40f),
            ],
            Collectibles:
            [
                new(50f, -120f),
                new(430f, -110f),
                new(620f, -90f),
                new(1200f, -60f),
                new(1650f, -40f),
            ]);
    }

    /// Phase 2: Open Water — moving platforms, first crumbling ice
    private static LevelData Phase2()
    {
        return new LevelData(
            PhaseName: "Open Water",
            PhaseIndex: 2,
            BackgroundColor: new Color(25, 75, 145),
            PlayerStartX: -400f,
            PlayerStartY: -80f,
            DeathZoneY: -350f,
            LevelLeftBound: -500f,
            LevelRightBound: 2400f,
            LevelTopBound: 400f,
            Platforms:
            [
                // Start
                new(-400f, -150f, 200f, 24f),

                // Moving platforms section
                new(-150f, -130f, 110f, 20f, PlatformType.Moving, MoveRangeX: 60f, MoveSpeed: 40f),
                new(60f, -120f, 100f, 20f),
                new(230f, -110f, 110f, 20f, PlatformType.Moving, MoveRangeY: 50f, MoveSpeed: 35f),
                new(420f, -120f, 120f, 20f),

                // First crumbling ice!
                new(600f, -100f, 100f, 20f, PlatformType.Crumbling, CrumbleDelay: 2.5f),
                new(760f, -110f, 120f, 20f),

                // More moving + gaps
                new(950f, -90f, 100f, 20f, PlatformType.Moving, MoveRangeX: 80f, MoveSpeed: 50f),
                new(1150f, -100f, 110f, 20f),
                new(1330f, -80f, 100f, 20f, PlatformType.Crumbling, CrumbleDelay: 2f),
                new(1500f, -100f, 130f, 20f),

                // Final stretch
                new(1700f, -80f, 120f, 20f, PlatformType.Moving, MoveRangeX: 50f, MoveSpeed: 45f),
                new(1900f, -90f, 140f, 20f),
                new(2100f, -70f, 200f, 24f),
            ],
            Checkpoints:
            [
                new(420f, -98f, 0),
                new(1150f, -78f, 1),
                new(1900f, -68f, 2),
            ],
            Npcs:
            [
                new(NpcKind.PolarBear, -400f, -118f, "Watch out — some ice blocks crack!"),
                new(NpcKind.Penguin, 60f, -98f, WaddleRange: 40f),
                new(NpcKind.Penguin, 760f, -88f, WaddleRange: 50f),
                new(NpcKind.Seal, 1500f, -70f),
            ],
            Collectibles:
            [
                new(-150f, -90f),
                new(230f, -70f),
                new(600f, -60f, 200),
                new(950f, -50f),
                new(1330f, -40f, 200),
                new(1700f, -40f),
                new(2100f, -30f),
            ]);
    }

    /// Phase 3: The Ice Field — dense platforms, wind gusts, multiple paths
    private static LevelData Phase3()
    {
        return new LevelData(
            PhaseName: "The Ice Field",
            PhaseIndex: 3,
            BackgroundColor: new Color(30, 85, 150),
            PlayerStartX: -400f,
            PlayerStartY: -60f,
            DeathZoneY: -380f,
            LevelLeftBound: -500f,
            LevelRightBound: 2800f,
            LevelTopBound: 500f,
            Platforms:
            [
                // Start
                new(-400f, -130f, 180f, 24f),

                // Dense field — lower path (easier, longer)
                new(-180f, -140f, 80f, 18f),
                new(-60f, -150f, 80f, 18f),
                new(60f, -140f, 90f, 18f),
                new(200f, -130f, 80f, 18f),
                new(340f, -140f, 80f, 18f),

                // Dense field — upper path (harder, shorter, has unicorn)
                new(-160f, -60f, 70f, 16f),
                new(-20f, -30f, 60f, 16f),
                new(120f, -10f, 60f, 16f, PlatformType.Moving, MoveRangeX: 30f, MoveSpeed: 40f),
                new(280f, 10f, 70f, 16f),
                // Unicorn platform (hard to reach)
                new(200f, 80f, 60f, 16f),

                // Converge point
                new(500f, -120f, 140f, 22f),

                // Moving section
                new(680f, -100f, 90f, 18f, PlatformType.Moving, MoveRangeX: 70f, MoveSpeed: 55f),
                new(870f, -90f, 80f, 18f, PlatformType.Crumbling, CrumbleDelay: 1.8f),
                new(1040f, -100f, 100f, 18f),

                // Rapid small jumps
                new(1180f, -80f, 60f, 16f),
                new(1270f, -70f, 50f, 16f),
                new(1350f, -80f, 50f, 16f),
                new(1430f, -60f, 60f, 16f),
                new(1520f, -70f, 70f, 16f),

                // Rest area
                new(1680f, -80f, 160f, 22f),

                // Final push
                new(1880f, -90f, 80f, 18f, PlatformType.Moving, MoveRangeY: 40f, MoveSpeed: 35f),
                new(2050f, -70f, 90f, 18f, PlatformType.Crumbling, CrumbleDelay: 1.5f),
                new(2200f, -80f, 100f, 18f),
                new(2400f, -60f, 120f, 18f, PlatformType.Moving, MoveRangeX: 50f, MoveSpeed: 45f),
                new(2600f, -50f, 200f, 24f),
            ],
            Checkpoints:
            [
                new(500f, -98f, 0),
                new(1040f, -78f, 1),
                new(1680f, -58f, 2),
                new(2200f, -58f, 3),
            ],
            Npcs:
            [
                new(NpcKind.PolarBear, -400f, -98f, "There are two paths — upper one has a secret!"),
                new(NpcKind.Penguin, -60f, -128f, WaddleRange: 30f),
                new(NpcKind.Penguin, 1350f, -58f, WaddleRange: 20f, CanBellySlide: true),
                new(NpcKind.Penguin, 1430f, -38f, WaddleRange: 20f),
                new(NpcKind.Seal, 1040f, -72f),
                new(NpcKind.Unicorn, 200f, 104f),
            ],
            Collectibles:
            [
                new(-60f, -110f),
                new(120f, 30f, 200),
                new(500f, -80f),
                new(680f, -60f),
                new(1180f, -40f),
                new(1270f, -30f),
                new(1350f, -40f),
                new(1680f, -40f),
                new(2050f, -30f),
                new(2600f, -10f),
            ]);
    }

    /// Phase 4: The Storm — darker, faster, tilting platforms, falling icicles
    private static LevelData Phase4()
    {
        return new LevelData(
            PhaseName: "The Storm",
            PhaseIndex: 4,
            BackgroundColor: new Color(20, 45, 80),
            PlayerStartX: -400f,
            PlayerStartY: -60f,
            DeathZoneY: -400f,
            LevelLeftBound: -500f,
            LevelRightBound: 3000f,
            LevelTopBound: 500f,
            Platforms:
            [
                // Start (calm before the storm)
                new(-400f, -140f, 180f, 24f),

                // Storm hits — moving + crumbling combo
                new(-160f, -120f, 90f, 18f, PlatformType.Moving, MoveRangeX: 80f, MoveSpeed: 65f),
                new(40f, -110f, 80f, 18f, PlatformType.Crumbling, CrumbleDelay: 1.5f),
                new(200f, -120f, 100f, 18f),
                new(370f, -100f, 90f, 18f, PlatformType.Moving, MoveRangeY: 60f, MoveSpeed: 50f),

                // Tilting platforms section
                new(550f, -110f, 120f, 18f),
                // Note: tilting is visual/behavioral — we simulate with moving
                new(730f, -90f, 80f, 18f, PlatformType.Moving, MoveRangeY: 30f, MoveSpeed: 70f),
                new(900f, -100f, 90f, 18f, PlatformType.Crumbling, CrumbleDelay: 1.2f),

                // Checkpoint rest
                new(1080f, -80f, 140f, 22f),

                // Intense section — small fast platforms
                new(1260f, -90f, 70f, 16f, PlatformType.Moving, MoveRangeX: 60f, MoveSpeed: 70f),
                new(1420f, -80f, 60f, 16f, PlatformType.Crumbling, CrumbleDelay: 1.0f),
                new(1560f, -90f, 70f, 16f),
                new(1700f, -70f, 60f, 16f, PlatformType.OneShot, OneShotDelay: 0.4f),
                new(1840f, -80f, 80f, 16f),

                // Checkpoint rest
                new(2020f, -60f, 150f, 22f),

                // Final gauntlet
                new(2220f, -70f, 70f, 16f, PlatformType.Moving, MoveRangeX: 90f, MoveSpeed: 75f),
                new(2400f, -60f, 80f, 16f, PlatformType.Crumbling, CrumbleDelay: 1.0f),
                new(2560f, -70f, 60f, 16f, PlatformType.OneShot, OneShotDelay: 0.3f),
                new(2700f, -50f, 90f, 18f, PlatformType.Moving, MoveRangeY: 50f, MoveSpeed: 60f),
                new(2880f, -40f, 200f, 24f),
            ],
            Checkpoints:
            [
                new(550f, -88f, 0),
                new(1080f, -58f, 1),
                new(2020f, -38f, 2),
            ],
            Npcs:
            [
                new(NpcKind.PolarBear, 1080f, -50f, "Almost there — stay strong!"),
                new(NpcKind.PolarBear, 2020f, -30f, "The mountain is close!"),
                new(NpcKind.Seal, 200f, -92f),
                new(NpcKind.Seal, 1560f, -62f),
                new(NpcKind.Seal, 2560f, -42f),
                new(NpcKind.Penguin, 550f, -88f, WaddleRange: 50f),
                new(NpcKind.Penguin, 1840f, -58f, WaddleRange: 30f, CanBellySlide: true),
            ],
            Collectibles:
            [
                new(-160f, -80f),
                new(370f, -60f, 200),
                new(730f, -50f),
                new(1260f, -50f),
                new(1700f, -30f, 300),
                new(2220f, -30f),
                new(2560f, -30f, 300),
                new(2880f, 0f),
            ]);
    }

    /// Phase 5: The Mountain Base — vertical climbing, the final push
    private static LevelData Phase5()
    {
        return new LevelData(
            PhaseName: "The Mountain",
            PhaseIndex: 5,
            BackgroundColor: new Color(35, 55, 90),
            PlayerStartX: -300f,
            PlayerStartY: -80f,
            DeathZoneY: -400f,
            LevelLeftBound: -500f,
            LevelRightBound: 1200f,
            LevelTopBound: 2000f,
            Platforms:
            [
                // Base approach
                new(-300f, -150f, 200f, 24f),
                new(-60f, -130f, 120f, 20f),
                new(120f, -110f, 100f, 20f, PlatformType.Moving, MoveRangeX: 40f, MoveSpeed: 35f),
                new(300f, -90f, 130f, 22f),

                // Start climbing — zigzag upward
                new(150f, 0f, 110f, 20f),
                new(-50f, 80f, 100f, 20f),
                new(100f, 160f, 90f, 18f, PlatformType.Moving, MoveRangeX: 50f, MoveSpeed: 40f),
                new(300f, 220f, 100f, 20f),

                // Mid-climb checkpoint
                new(150f, 320f, 140f, 22f),

                // Harder climbing
                new(-20f, 420f, 80f, 18f, PlatformType.Crumbling, CrumbleDelay: 1.5f),
                new(160f, 500f, 70f, 16f, PlatformType.OneShot, OneShotDelay: 0.5f),
                new(340f, 560f, 90f, 18f),
                new(180f, 650f, 80f, 18f, PlatformType.Moving, MoveRangeX: 60f, MoveSpeed: 50f),

                // Near summit
                new(20f, 750f, 100f, 18f, PlatformType.Crumbling, CrumbleDelay: 1.2f),
                new(200f, 840f, 70f, 16f, PlatformType.OneShot, OneShotDelay: 0.4f),
                new(50f, 920f, 90f, 18f),
                new(250f, 1000f, 80f, 18f, PlatformType.Moving, MoveRangeY: 40f, MoveSpeed: 45f),

                // Summit checkpoint
                new(100f, 1120f, 150f, 22f),

                // Final platforms to peak
                new(-30f, 1240f, 80f, 18f),
                new(150f, 1340f, 70f, 16f, PlatformType.OneShot, OneShotDelay: 0.4f),
                new(30f, 1440f, 90f, 18f),

                // THE SUMMIT
                new(100f, 1560f, 250f, 30f),
            ],
            Checkpoints:
            [
                new(300f, -68f, 0),
                new(150f, 342f, 1),
                new(100f, 1142f, 2),
            ],
            Npcs:
            [
                new(NpcKind.PolarBear, -300f, -118f, "The mountain! Climb to the top!"),
                new(NpcKind.Penguin, 300f, -68f, WaddleRange: 50f),
                new(NpcKind.Seal, 340f, 588f),
                new(NpcKind.Penguin, 50f, 942f, WaddleRange: 30f, CanBellySlide: true),
                new(NpcKind.Unicorn, -30f, 1268f),
                // Summit celebration — all animals!
                new(NpcKind.PolarBear, 40f, 1596f, "You made it! Welcome to the summit!"),
                new(NpcKind.Penguin, 140f, 1582f, WaddleRange: 30f),
                new(NpcKind.Penguin, 180f, 1582f, WaddleRange: 20f),
                new(NpcKind.Seal, 80f, 1588f),
            ],
            Collectibles:
            [
                new(120f, -70f),
                new(-50f, 120f),
                new(300f, 260f, 200),
                new(160f, 540f),
                new(20f, 790f, 200),
                new(250f, 1040f),
                new(150f, 1380f, 300),
                new(100f, 1600f, 500),
            ]);
    }
}
