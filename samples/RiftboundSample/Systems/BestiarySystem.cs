using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class BestiarySystem
{
    private readonly Dictionary<string, BestiaryEntry> _entries = [];
    private readonly Dictionary<string, EnemyData> _enemyLookup;
    private List<BestiaryReward> _rewards = [];
    private readonly HashSet<int> _claimedRewards = [];

    public IReadOnlySet<int> ClaimedRewards => _claimedRewards;

    public BestiarySystem(List<EnemyData> enemies)
    {
        _enemyLookup = enemies.ToDictionary(e => e.Id);
    }

    public void SetRewards(List<BestiaryReward> rewards)
    {
        _rewards = rewards;
    }

    /// <summary>Total unique enemies in the lookup (denominator for completion %).</summary>
    public int TotalEnemyCount => _enemyLookup.Count;

    /// <summary>Unique enemies encountered so far (numerator for completion %).</summary>
    public int EncounteredCount => _entries.Count;

    /// <summary>
    /// Returns newly earned, unclaimed rewards based on current bestiary progress.
    /// Each reward is identified by its index in the rewards list.
    /// </summary>
    public List<(int Index, BestiaryReward Reward)> CheckRewards()
    {
        var earned = new List<(int, BestiaryReward)>();
        for (int i = 0; i < _rewards.Count; i++)
        {
            if (_claimedRewards.Contains(i)) continue;
            if (_entries.Count >= _rewards[i].RequiredEntries)
                earned.Add((i, _rewards[i]));
        }
        return earned;
    }

    public bool ClaimReward(int index)
    {
        if (index < 0 || index >= _rewards.Count) return false;
        if (_claimedRewards.Contains(index)) return false;
        if (_entries.Count < _rewards[index].RequiredEntries) return false;
        return _claimedRewards.Add(index);
    }

    public List<BestiaryReward> GetAllRewards() => _rewards;

    /// <summary>
    /// Registers an enemy as encountered (first sighting). Called when battle starts.
    /// </summary>
    public void RecordEncounter(string enemyId)
    {
        if (_entries.ContainsKey(enemyId)) return;
        if (!_enemyLookup.TryGetValue(enemyId, out var data)) return;

        _entries[enemyId] = new BestiaryEntry
        {
            EnemyId = enemyId,
            Name = data.Name,
        };
    }

    /// <summary>
    /// Increments the defeat count for an enemy, unlocking info at thresholds.
    /// </summary>
    public void RecordDefeat(string enemyId)
    {
        RecordEncounter(enemyId);
        if (_entries.TryGetValue(enemyId, out var entry))
            entry.TimesDefeated++;
    }

    public List<BestiaryEntry> GetEntries() => _entries.Values.ToList();

    public BestiaryEntry? GetEntry(string enemyId) =>
        _entries.TryGetValue(enemyId, out var entry) ? entry : null;

    public EnemyData? GetEnemyData(string enemyId) =>
        _enemyLookup.TryGetValue(enemyId, out var data) ? data : null;
}
