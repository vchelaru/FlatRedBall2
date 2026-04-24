using System.IO;
using System.Text.Json;

namespace ShmupSpace;

// Mirror of shmupspace.game.json. Game code reads from the live instance exposed on GameScreen;
// hot-reload re-parses the file and copies fields onto that same instance, so entities
// referencing GameScreen.Config pick up changes on their next frame.
public class GameConfig
{
    public PlayerSection Player { get; set; } = new();
    public EnemySection Enemy { get; set; } = new();
    public SpawnSection Spawn { get; set; } = new();
    public ScoringSection Scoring { get; set; } = new();

    public class PlayerSection
    {
        public float FireInterval { get; set; } = 0.22f;
        public float BulletSpeed { get; set; } = 260f;
        public float EdgeMargin { get; set; } = 8f;
    }

    public class EnemySection
    {
        public float FallSpeed { get; set; } = -60f;
        public float ZigSpeed { get; set; } = 45f;
        public float ZigInterval { get; set; } = 0.9f;
    }

    public class SpawnSection
    {
        public float EnemyInterval { get; set; } = 1.1f;
        public float EnemyEdgeMargin { get; set; } = 50f;
        public float PlayerRespawnDelay { get; set; } = 1.25f;
    }

    public class ScoringSection
    {
        public int PerEnemyKill { get; set; } = 100;
    }

    public static GameConfig FromJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GameConfig>(json, Options) ?? new GameConfig();
    }

    // Copy each field from another instance onto this one — lets live entities keep their
    // reference to a single Config object across hot-reloads.
    public void CopyFrom(GameConfig other)
    {
        Player.FireInterval = other.Player.FireInterval;
        Player.BulletSpeed = other.Player.BulletSpeed;
        Player.EdgeMargin = other.Player.EdgeMargin;
        Enemy.FallSpeed = other.Enemy.FallSpeed;
        Enemy.ZigSpeed = other.Enemy.ZigSpeed;
        Enemy.ZigInterval = other.Enemy.ZigInterval;
        Spawn.EnemyInterval = other.Spawn.EnemyInterval;
        Spawn.EnemyEdgeMargin = other.Spawn.EnemyEdgeMargin;
        Spawn.PlayerRespawnDelay = other.Spawn.PlayerRespawnDelay;
        Scoring.PerEnemyKill = other.Scoring.PerEnemyKill;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
