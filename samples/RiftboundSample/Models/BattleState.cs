namespace RiftboundSample.Models;

public class BattleState
{
    public List<CombatantState> PlayerParty { get; set; } = [];
    public List<CombatantState> Enemies { get; set; } = [];
    public List<CombatantState> AllCombatants => PlayerParty.Concat(Enemies).ToList();
    public bool IsBossBattle { get; set; }
    public bool IsVictory => Enemies.All(e => !e.IsAlive);
    public bool IsDefeat => PlayerParty.All(p => !p.IsAlive);
    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsPaused { get; set; }

    /// <summary>Bench party members who receive 75% XP on victory.</summary>
    public List<CombatantState> BenchParty { get; set; } = [];
}
