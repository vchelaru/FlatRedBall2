using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ShmupSpace;

// Mirror of shmupspace.game.json. Game code reads from the live instance exposed on GameScreen;
// hot-reload re-parses the file and copies fields onto that same instance, so entities
// referencing GameScreen.Config pick up changes on their next frame.
//
// Why reflection for CopyFrom: a hand-written field-by-field copy is one more place that has to
// be edited every time a new config field is added (JSON + POCO + CopyFrom — miss the third and
// the field silently stops hot-reloading). Reflection walks the tree automatically. Suitable for
// plain POCO configs like this one; would need more care for classes with non-trivial state.
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

    public void CopyFrom(GameConfig other) => CopyProperties(this, other);

    private static void CopyProperties(object dest, object src)
    {
        foreach (var prop in dest.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            var srcVal = prop.GetValue(src);
            if (srcVal == null) continue;

            if (IsLeaf(prop.PropertyType))
            {
                if (prop.CanWrite) prop.SetValue(dest, srcVal);
            }
            else
            {
                // Recurse into nested sections (PlayerSection, EnemySection, ...) to preserve
                // the dest instance — entities hold references to these nested objects too.
                var destVal = prop.GetValue(dest);
                if (destVal != null) CopyProperties(destVal, srcVal);
            }
        }
    }

    private static bool IsLeaf(Type t)
        => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
