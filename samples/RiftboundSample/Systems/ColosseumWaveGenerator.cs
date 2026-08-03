using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class ColosseumWaveGenerator
{
    private readonly List<EnemyData> _allEnemies;
    private readonly Random _random;

    // Categorized enemy pools
    private readonly List<EnemyData> _overworldEnemies;
    private readonly List<EnemyData> _etherealEnemies;
    private readonly List<EnemyData> _nexusEnemies;
    private readonly List<EnemyData> _bossEnemies;

    public ColosseumWaveGenerator(List<EnemyData> allEnemies, Random? random = null)
    {
        _allEnemies = allEnemies;
        _random = random ?? Random.Shared;

        // Overworld enemies: low-tier non-boss
        var overworldIds = new HashSet<string>
        {
            "gear_golem", "steam_rat", "rust_beetle"
        };
        _overworldEnemies = allEnemies.Where(e => !e.IsBoss && overworldIds.Contains(e.Id)).ToList();

        // Ethereal enemies
        var etherealIds = new HashSet<string>
        {
            "wisp", "crystal_crawler", "dream_eater", "phantom_knight",
            "rift_hound", "spore_shade"
        };
        _etherealEnemies = allEnemies.Where(e => !e.IsBoss && etherealIds.Contains(e.Id)).ToList();

        // Nexus enemies
        var nexusIds = new HashSet<string>
        {
            "bit_bug", "firewall_drone", "corrupted_process", "data_wraith",
            "security_bot", "virus_cluster"
        };
        _nexusEnemies = allEnemies.Where(e => !e.IsBoss && nexusIds.Contains(e.Id)).ToList();

        _bossEnemies = allEnemies.Where(e => e.IsBoss).ToList();
    }

    /// <summary>
    /// Generates a list of enemies for the given wave number.
    /// Boss waves occur every 5 waves.
    /// Stats scale +10% per wave past 20.
    /// </summary>
    public List<EnemyData> GenerateWave(int waveNumber)
    {
        bool isBossWave = waveNumber % 5 == 0;

        if (isBossWave)
            return GenerateBossWave(waveNumber);

        var pool = GetEnemyPool(waveNumber);
        int count = GetEnemyCount(waveNumber);

        var enemies = new List<EnemyData>();
        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;
            var template = pool[_random.Next(pool.Count)];
            enemies.Add(ScaleEnemy(template, waveNumber));
        }

        return enemies;
    }

    /// <summary>Returns XP reward for completing a wave.</summary>
    public int GetWaveXP(int waveNumber) => 20 + waveNumber * 10;

    /// <summary>Returns gold reward for completing a wave.</summary>
    public int GetWaveGold(int waveNumber) => 10 + waveNumber * 5;

    private List<EnemyData> GetEnemyPool(int wave) => wave switch
    {
        <= 5 => _overworldEnemies,
        <= 10 => Combine(_overworldEnemies, _etherealEnemies),
        <= 15 => Combine(_etherealEnemies, _nexusEnemies),
        _ => Combine(_overworldEnemies, _etherealEnemies, _nexusEnemies),
    };

    private int GetEnemyCount(int wave) => wave switch
    {
        <= 5 => 2 + _random.Next(2),   // 2-3
        <= 10 => 3 + _random.Next(2),  // 3-4
        <= 15 => 3 + _random.Next(2),  // 3-4
        _ => 4 + _random.Next(2),      // 4-5
    };

    private List<EnemyData> GenerateBossWave(int wave)
    {
        if (_bossEnemies.Count == 0) return [];
        var boss = _bossEnemies[_random.Next(_bossEnemies.Count)];
        return [ScaleEnemy(boss, wave)];
    }

    private EnemyData ScaleEnemy(EnemyData template, int wave)
    {
        if (wave <= 20) return template;

        float scale = 1f + (wave - 20) * 0.1f;
        return new EnemyData
        {
            Id = template.Id,
            Name = template.Name,
            HP = (int)(template.HP * scale),
            MP = template.MP,
            STR = (int)(template.STR * scale),
            MAG = (int)(template.MAG * scale),
            DEF = (int)(template.DEF * scale),
            RES = (int)(template.RES * scale),
            SPD = template.SPD,
            IsBoss = template.IsBoss,
            XPReward = (int)(template.XPReward * scale),
            AbilityIds = template.AbilityIds,
            ElementAffinities = template.ElementAffinities,
            DropTable = template.DropTable,
        };
    }

    private static List<EnemyData> Combine(params List<EnemyData>[] pools) =>
        pools.SelectMany(p => p).ToList();
}
