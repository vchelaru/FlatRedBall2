using Microsoft.Xna.Framework;

namespace Strikers1945Sample.Screens;

/// <summary>
/// Defines the configuration for a single level — background tiles,
/// boss sprites, enemy density, and wave count.
/// </summary>
public record LevelDefinition(
    int LevelNumber,
    string LevelName,
    string[] BackgroundTiles,
    Color BackgroundColor,
    float TileDensity,
    string BossSprite,
    string BossPhase2Sprite,
    int BossHealth,
    int WaveCount,
    float WaveBreathDuration,
    bool HasHeavyEnemies,
    bool HasDiveBombers
)
{
    public static readonly LevelDefinition[] AllLevels =
    {
        new(1, "Pacific Ocean",
            new[] { "tile_0040", "tile_0036", "tile_0054", "tile_0050" },
            new Color(106, 190, 226), 0.15f,
            "ship_0020", "ship_0021", 180,
            WaveCount: 25, WaveBreathDuration: 3.5f,
            HasHeavyEnemies: false, HasDiveBombers: false),

        new(2, "European Countryside",
            new[] { "tile_0040", "tile_0041", "tile_0049", "tile_0050", "tile_0036", "tile_0054" },
            new Color(90, 160, 60), 1f,
            "ship_0021", "ship_0022", 240,
            WaveCount: 30, WaveBreathDuration: 3.0f,
            HasHeavyEnemies: true, HasDiveBombers: false),

        new(3, "North Africa",
            new[] { "tile_0043", "tile_0044", "tile_0045", "tile_0046", "tile_0047", "tile_0116" },
            new Color(200, 150, 80), 1f,
            "ship_0022", "ship_0023", 300,
            WaveCount: 35, WaveBreathDuration: 2.5f,
            HasHeavyEnemies: true, HasDiveBombers: true),

        new(4, "Enemy Airfield",
            new[] { "tile_0040", "tile_0050", "tile_0108", "tile_0109", "tile_0048", "tile_0110" },
            new Color(80, 140, 50), 1f,
            "ship_0023", "ship_0020", 360,
            WaveCount: 40, WaveBreathDuration: 2.2f,
            HasHeavyEnemies: true, HasDiveBombers: true),

        new(5, "Final Assault",
            new[] { "tile_0040", "tile_0043", "tile_0116", "tile_0050", "tile_0045", "tile_0048" },
            new Color(40, 50, 30), 1f,
            "ship_0020", "ship_0023", 450,
            WaveCount: 45, WaveBreathDuration: 2.0f,
            HasHeavyEnemies: true, HasDiveBombers: true),
    };
}
